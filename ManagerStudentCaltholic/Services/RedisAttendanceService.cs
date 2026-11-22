using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class RedisAttendanceService : IRedisAttendanceService
    {
        private readonly ParishDbContext _context;
        private readonly ICacheService _cache;
        private readonly IAttendanceFlushSignal _flushSignal;
        private readonly ILogger<RedisAttendanceService> _logger;

        private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(18);
        private static readonly TimeSpan CurrentYearCacheTtl = TimeSpan.FromHours(12);
        private static readonly TimeSpan ActiveCampaignCacheTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan NegativeQrLookupTtl = TimeSpan.FromSeconds(30);
        private const int RedisWriteBatchSize = 20;

        // TASK-INF-08: Striped locks cố định dung lượng RAM để tránh race condition read-modify-write trên Redis
        // Thứ tự khóa luôn là: ClassFlushLocks (ngoài) -> ClassSheetLocks (trong); DirtyIndexLocks luôn tách rời (leaf) -> không bao giờ deadlock.
        private const int ClassLockStripes = 64;
        private const int DirtyIndexLockStripes = 16;

        private static readonly SemaphoreSlim[] ClassFlushLocks = CreateStripedLocks(ClassLockStripes);
        private static readonly SemaphoreSlim[] ClassSheetLocks = CreateStripedLocks(ClassLockStripes);
        private static readonly SemaphoreSlim[] DirtyIndexLocks = CreateStripedLocks(DirtyIndexLockStripes);

        private static SemaphoreSlim[] CreateStripedLocks(int count)
        {
            var locks = new SemaphoreSlim[count];
            for (int i = 0; i < count; i++)
            {
                locks[i] = new SemaphoreSlim(1, 1);
            }
            return locks;
        }

        private static SemaphoreSlim GetClassFlushLock(int classId, DateTime dateUtc)
        {
            var idx = (uint)HashCode.Combine(classId, dateUtc.Date) % ClassLockStripes;
            return ClassFlushLocks[idx];
        }

        private static SemaphoreSlim GetClassSheetLock(int classId, DateTime dateUtc)
        {
            var idx = (uint)HashCode.Combine(classId, dateUtc.Date) % ClassLockStripes;
            return ClassSheetLocks[idx];
        }

        private static SemaphoreSlim GetDirtyIndexLock(DateTime dateUtc)
        {
            var idx = (uint)dateUtc.Date.GetHashCode() % DirtyIndexLockStripes;
            return DirtyIndexLocks[idx];
        }

        private static DateTime GetVietnamNow() => DateTime.UtcNow.AddHours(7);

        public RedisAttendanceService(
            ParishDbContext context,
            ICacheService cache,
            IAttendanceFlushSignal flushSignal,
            ILogger<RedisAttendanceService> logger)
        {
            _context = context;
            _cache = cache;
            _flushSignal = flushSignal;
            _logger = logger;
        }

        /// <summary>
        /// 1. TASK-P3-12.2 / TASK-INF-08: Hangfire Job nạp sẵn toàn bộ danh sách học sinh, alias thẻ năm cũ, chiến dịch & thông tin giờ học lên Redis
        /// </summary>
        public async Task<int> PreWarmDailyAttendanceAndTasksAsync(DateTime? targetDate = null)
        {
            // Dùng ngày theo múi giờ Việt Nam (UTC+7) để tránh lệch ngày trong container Linux UTC lúc 05:30 sáng CN
            var dateUtc = DateTime.SpecifyKind((targetDate ?? GetVietnamNow().Date).Date, DateTimeKind.Utc);
            var currentYear = await GetCurrentAcademicYearCachedAsync();

            if (currentYear == null)
            {
                _logger.LogWarning("[PreWarmJob] Không tìm thấy niên khóa hiện hành để nạp bộ đệm điểm danh.");
                return 0;
            }

            var classes = await _context.Classes
                .AsNoTracking()
                .Where(c => c.AcademicYearId == currentYear.Id)
                .OrderBy(c => c.Id)
                .Select(c => new ClassRoom
                {
                    Id = c.Id,
                    Name = c.Name,
                    GradeLevel = c.GradeLevel,
                    Session = c.Session,
                    RoomName = c.RoomName,
                    AcademicYearId = c.AcademicYearId
                })
                .ToListAsync();

            if (!classes.Any()) return 0;

            var classIds = classes.Select(c => c.Id).ToList();

            var allEnrollments = await _context.Enrollments
                .AsNoTracking()
                .Where(e => classIds.Contains(e.ClassRoomId) && e.Student.IsActive)
                .OrderBy(e => e.Student.StudentCode)
                .ThenBy(e => e.Student.LastName)
                .Select(e => new
                {
                    e.Id,
                    e.StudentId,
                    e.ClassRoomId,
                    e.Student.StudentCode,
                    e.Student.ChristianName,
                    e.Student.FirstName,
                    e.Student.LastName,
                    e.Student.Gender
                })
                .ToListAsync();

            var enrollmentIds = allEnrollments.Select(e => e.Id).ToList();
            var studentIds = allEnrollments.Select(e => e.StudentId).Distinct().ToList();

            var existingAttendances = await _context.Attendances
                .AsNoTracking()
                .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.AttendanceDate == dateUtc)
                .Select(a => new
                {
                    a.EnrollmentId,
                    a.AttendedMass,
                    a.MassStatus,
                    a.MassCheckInTime,
                    a.ClassAttended,
                    a.ClassStatus,
                    a.ClassCheckInTime,
                    a.IsMakeUp,
                    a.Note
                })
                .ToDictionaryAsync(a => a.EnrollmentId);

            // TASK-INF-08: Pre-warm luôn alias mã thẻ năm cũ (StudentCodeHistories) để quét thẻ cũ cũng đạt O(1) 0-DB-query
            var historicalCodeRows = studentIds.Count > 0
                ? await _context.StudentCodeHistories
                    .AsNoTracking()
                    .Where(h => studentIds.Contains(h.StudentId))
                    .Select(h => new { h.StudentId, h.OriginalCode, h.ArchivedYearCode })
                    .ToListAsync()
                : new();

            var historicalCodesByStudent = historicalCodeRows
                .GroupBy(h => h.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.OriginalCode?.Trim().ToUpperInvariant() ?? string.Empty)
                          .Concat(g.Select(x => x.ArchivedYearCode?.Trim().ToUpperInvariant() ?? string.Empty))
                          .Where(c => !string.IsNullOrEmpty(c))
                          .Distinct()
                          .ToList());

            // Pre-warm trạng thái chiến dịch đi lễ đang mở theo lớp cho ngày dateUtc
            var activeCampaignsByClass = await _context.MassCampaigns
                .AsNoTracking()
                .Where(c => classIds.Contains(c.ClassRoomId) &&
                            c.Status == "ACTIVE" &&
                            dateUtc >= c.StartDate.Date &&
                            dateUtc <= c.EndDate.Date)
                .Select(c => new { c.ClassRoomId, c.Id })
                .ToListAsync();

            var activeCampaignMap = activeCampaignsByClass
                .GroupBy(c => c.ClassRoomId)
                .ToDictionary(g => g.Key, g => g.First().Id);

            var schedules = await _context.ClassSchedules
                .AsNoTracking()
                .Include(s => s.ClassRoomLocation)
                .Where(s => classIds.Contains(s.ClassRoomId))
                .ToListAsync();

            var pendingAbsencesByClass = await _context.AbsenceRequests
                .AsNoTracking()
                .Where(r => r.Status == "PENDING" && classIds.Contains(r.Enrollment.ClassRoomId))
                .GroupBy(r => r.Enrollment.ClassRoomId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count);

            var pinnedAnnouncements = await _context.Announcements
                .AsNoTracking()
                .Where(a => a.IsActive && a.IsPinned)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new { a.Title, a.TargetClassRoomId, a.Scope, a.TargetGradeLevel })
                .ToListAsync();

            var enrollmentsByClass = allEnrollments.ToLookup(e => e.ClassRoomId);
            var schedulesByClass = schedules.GroupBy(s => s.ClassRoomId).ToDictionary(g => g.Key, g => g.First());

            var qrCacheEntries = new List<(string Key, RedisQrStudentLookupItem Value)>(allEnrollments.Count + historicalCodeRows.Count);
            var sheetCacheEntries = new List<(string Key, RedisClassAttendancePayload Value)>(classes.Count);
            var sessionCacheEntries = new List<(string Key, ClassSessionContextDto Value)>(classes.Count);
            var campaignCacheEntries = new List<(string Key, RedisActiveCampaignCacheItem Value)>(classes.Count);

            int totalStudentsCached = 0;

            foreach (var cls in classes)
            {
                var clsEnrollments = enrollmentsByClass[cls.Id];
                var studentRows = new List<StudentAttendanceRowItem>();

                foreach (var e in clsEnrollments)
                {
                    existingAttendances.TryGetValue(e.Id, out var att);
                    var fullName = $"{e.FirstName} {e.LastName}".Trim();
                    var officialCode = e.StudentCode.Trim().ToUpperInvariant();

                    studentRows.Add(new StudentAttendanceRowItem
                    {
                        EnrollmentId = e.Id,
                        StudentId = e.StudentId,
                        StudentCode = e.StudentCode,
                        ChristianName = e.ChristianName,
                        FullName = fullName,
                        Gender = e.Gender ?? "Nam",
                        AttendedMass = att?.AttendedMass ?? false,
                        MassStatus = att?.MassStatus ?? AttendanceStatus.AbsentUnpermitted,
                        MassCheckInTime = att?.MassCheckInTime,
                        ClassAttended = att?.ClassAttended ?? false,
                        ClassStatus = att?.ClassStatus ?? AttendanceStatus.AbsentUnpermitted,
                        ClassCheckInTime = att?.ClassCheckInTime,
                        IsMakeUp = att?.IsMakeUp ?? false,
                        Note = att?.Note
                    });

                    var lookupItem = new RedisQrStudentLookupItem
                    {
                        EnrollmentId = e.Id,
                        StudentId = e.StudentId,
                        StudentCode = officialCode,
                        ChristianName = e.ChristianName,
                        FullName = fullName,
                        Gender = e.Gender ?? "Nam",
                        ClassId = cls.Id,
                        ClassName = cls.Name,
                        GradeLevel = cls.GradeLevel
                    };

                    qrCacheEntries.Add((RedisAttendanceKeys.QrStudentLookup(dateUtc, officialCode), lookupItem));

                    if (historicalCodesByStudent.TryGetValue(e.StudentId, out var oldCodes))
                    {
                        foreach (var oldCode in oldCodes)
                        {
                            if (!string.Equals(oldCode, officialCode, StringComparison.OrdinalIgnoreCase))
                            {
                                qrCacheEntries.Add((RedisAttendanceKeys.QrStudentLookup(dateUtc, oldCode), lookupItem));
                            }
                        }
                    }

                    totalStudentsCached++;
                }

                var sheetKey = RedisAttendanceKeys.ClassSheet(cls.Id, dateUtc);
                sheetCacheEntries.Add((sheetKey, new RedisClassAttendancePayload
                {
                    ClassId = cls.Id,
                    ClassName = cls.Name,
                    GradeLevel = cls.GradeLevel,
                    AttendanceDate = dateUtc,
                    CachedAtUtc = DateTime.UtcNow,
                    IsDirty = false,
                    Revision = 0,
                    Students = studentRows
                }));

                schedulesByClass.TryGetValue(cls.Id, out var schedule);
                pendingAbsencesByClass.TryGetValue(cls.Id, out int pendingAbsenceCount);
                var pinnedAnn = pinnedAnnouncements.FirstOrDefault(a =>
                    a.TargetClassRoomId == cls.Id ||
                    (a.Scope == "GRADE" && a.TargetGradeLevel == cls.GradeLevel) ||
                    a.Scope == "ALL");

                var sessionCtx = BuildSessionContextObject(cls, dateUtc, schedule, pendingAbsenceCount, pinnedAnn?.Title, studentRows);
                sessionCacheEntries.Add((RedisAttendanceKeys.ClassSessionInfo(cls.Id, dateUtc), sessionCtx));

                bool hasActiveCampaign = activeCampaignMap.TryGetValue(cls.Id, out long campId);
                campaignCacheEntries.Add((
                    RedisAttendanceKeys.ActiveCampaignByClass(cls.Id, dateUtc),
                    new RedisActiveCampaignCacheItem
                    {
                        HasActiveCampaign = hasActiveCampaign,
                        CampaignId = hasActiveCampaign ? campId : null
                    }));
            }

            // Đẩy dữ liệu lên Redis theo lô song song có kiểm soát (tăng tốc Pre-warm ~10x)
            await SetManyAsync(qrCacheEntries, DefaultCacheTtl);
            await SetManyAsync(sheetCacheEntries, DefaultCacheTtl);
            await SetManyAsync(sessionCacheEntries, DefaultCacheTtl);
            await SetManyAsync(campaignCacheEntries, ActiveCampaignCacheTtl);

            _logger.LogInformation(
                "[PreWarmJob] Đã nạp thành công {StudentCount} học sinh ({QrKeyCount} khóa QR gồm cả thẻ năm cũ) của {ClassCount} lớp lên Redis cho ngày {Date:dd/MM/yyyy}.",
                totalStudentsCached, qrCacheEntries.Count, classes.Count, dateUtc);

            return totalStudentsCached;
        }

        /// <summary>
        /// 2. TASK-P3-12.3: Đọc Sổ điểm danh của lớp từ Redis (Nếu Cache Miss thì nạp từ DB và lưu lên Redis)
        /// </summary>
        public async Task<ClassAttendanceSheetViewModel> GetOrLoadClassSheetAsync(int classId, DateTime date)
        {
            var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var sheetKey = RedisAttendanceKeys.ClassSheet(classId, dateUtc);

            var cachedSheet = await _cache.GetAsync<RedisClassAttendancePayload>(sheetKey);
            if (cachedSheet != null)
            {
                return ToSheetViewModel(cachedSheet);
            }

            // Single-flight theo lớp để chống cache stampede khi nhiều GLV cùng mở/quét 1 lớp chưa pre-warm
            var sheetLock = GetClassSheetLock(classId, dateUtc);
            await sheetLock.WaitAsync();
            try
            {
                var payload = await GetOrLoadClassSheetPayloadLockedAsync(classId, dateUtc);
                return payload != null
                    ? ToSheetViewModel(payload)
                    : new ClassAttendanceSheetViewModel { ClassId = classId, AttendanceDate = dateUtc };
            }
            finally
            {
                sheetLock.Release();
            }
        }

        /// <summary>
        /// 3. TASK-P3-12.3: Lấy thông tin Giờ học & Task của GLV từ Redis
        /// </summary>
        public async Task<ClassSessionContextDto> GetOrLoadSessionContextAsync(int classId, DateTime date)
        {
            var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var sessionKey = RedisAttendanceKeys.ClassSessionInfo(classId, dateUtc);

            var cachedSession = await _cache.GetAsync<ClassSessionContextDto>(sessionKey);
            var cachedSheet = await _cache.GetAsync<RedisClassAttendancePayload>(RedisAttendanceKeys.ClassSheet(classId, dateUtc));

            if (cachedSession != null)
            {
                cachedSession.IsServedFromRedis = true;
                cachedSession.HasUnsyncedChanges = cachedSheet?.IsDirty ?? false;
                return cachedSession;
            }

            var targetClass = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Id == classId)
                .Select(c => new ClassRoom
                {
                    Id = c.Id,
                    Name = c.Name,
                    GradeLevel = c.GradeLevel,
                    Session = c.Session,
                    RoomName = c.RoomName,
                    AcademicYearId = c.AcademicYearId
                })
                .FirstOrDefaultAsync();

            if (targetClass == null)
            {
                return new ClassSessionContextDto { ClassId = classId, SessionDate = dateUtc };
            }

            var schedule = await _context.ClassSchedules
                .AsNoTracking()
                .Include(s => s.ClassRoomLocation)
                .FirstOrDefaultAsync(s => s.ClassRoomId == classId);

            int pendingAbsenceCount = await _context.AbsenceRequests
                .AsNoTracking()
                .CountAsync(r => r.Status == "PENDING" && r.Enrollment.ClassRoomId == classId);

            var pinnedAnn = await _context.Announcements
                .AsNoTracking()
                .Where(a => a.IsActive && a.IsPinned &&
                            (a.TargetClassRoomId == classId ||
                             (a.Scope == "GRADE" && a.TargetGradeLevel == targetClass.GradeLevel) ||
                             a.Scope == "ALL"))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.Title)
                .FirstOrDefaultAsync();

            var latestLessonDesc = await _context.ClassLessonDocuments
                .AsNoTracking()
                .Where(d => d.ClassRoomId == classId && d.AcademicYearId == targetClass.AcademicYearId)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => d.Description)
                .FirstOrDefaultAsync();

            var ctx = BuildSessionContextObject(
                targetClass,
                dateUtc,
                schedule,
                pendingAbsenceCount,
                pinnedAnn,
                cachedSheet?.Students ?? new List<StudentAttendanceRowItem>(),
                latestLessonDesc);

            ctx.HasUnsyncedChanges = cachedSheet?.IsDirty ?? false;
            await _cache.SetAsync(sessionKey, ctx, DefaultCacheTtl);
            return ctx;
        }

        /// <summary>
        /// 4. TASK-P3-12.3 / TASK-INF-08: Lưu Sổ điểm danh hàng loạt lên bộ đệm Redis (AttendanceController.SaveBatch gọi FlushClassToDatabaseAsync ngay sau đó).
        /// </summary>
        public async Task<(bool Success, string Message)> SaveBatchToRedisAsync(AttendanceBatchSubmitDto request)
        {
            var dateUtc = DateTime.SpecifyKind(request.AttendanceDate.Date, DateTimeKind.Utc);
            var sheetKey = RedisAttendanceKeys.ClassSheet(request.ClassId, dateUtc);
            var modifiedBy = SafeTruncate(request.ModifiedBy, 100) ?? "GLV";
            var clientIp = SafeTruncate(request.IpAddress, 45);

            var sheetLock = GetClassSheetLock(request.ClassId, dateUtc);
            await sheetLock.WaitAsync();
            try
            {
                var payload = await GetOrLoadClassSheetPayloadLockedAsync(request.ClassId, dateUtc);
                if (payload == null)
                {
                    return (false, "Không tìm thấy thông tin lớp học để lưu sổ điểm danh.");
                }

                var studentDict = payload.Students.ToDictionary(s => s.EnrollmentId, s => s);
                payload.DirtyEnrollmentIds ??= new HashSet<long>();
                payload.DirtyAuditMetaByEnrollment ??= new Dictionary<long, RedisDirtyAuditMeta>();

                foreach (var item in request.Items)
                {
                    if (studentDict.TryGetValue(item.EnrollmentId, out var row))
                    {
                        row.AttendedMass = item.AttendedMass;
                        row.MassStatus = NormalizeAttendanceStatus(item.AttendedMass, item.MassStatus, allowLinedUp: true);
                        if (!item.AttendedMass)
                        {
                            row.MassCheckInTime = null;
                        }

                        row.ClassAttended = item.ClassAttended;
                        row.ClassStatus = NormalizeAttendanceStatus(item.ClassAttended, item.ClassStatus, allowLinedUp: false);
                        if (!item.ClassAttended)
                        {
                            row.ClassCheckInTime = null;
                        }

                        row.Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim();

                        payload.DirtyEnrollmentIds.Add(item.EnrollmentId);
                        payload.DirtyAuditMetaByEnrollment[item.EnrollmentId] = new RedisDirtyAuditMeta
                        {
                            ActionType = "BATCH_SAVE",
                            ModifiedBy = modifiedBy,
                            IpAddress = clientIp
                        };
                    }
                }

                payload.LastModifiedAtUtc = DateTime.UtcNow;
                payload.LastModifiedBy = modifiedBy;
                payload.LastModifiedIp = clientIp;
                payload.LastActionType = "BATCH_SAVE";
                payload.IsDirty = true;
                payload.Revision++;

                await _cache.SetAsync(sheetKey, payload, DefaultCacheTtl);
            }
            finally
            {
                sheetLock.Release();
            }

            await MarkClassDirtyAsync(request.ClassId, dateUtc);
            await _cache.RemoveAsync(RedisAttendanceKeys.ClassSessionInfo(request.ClassId, dateUtc));

            return (true, $"Đã lưu nhanh sổ điểm danh ngày {dateUtc:dd/MM/yyyy} lên bộ đệm Redis ({request.Items.Count} em)! Hệ thống đang đồng bộ bền vững xuống CSDL.");
        }

        /// <summary>
        /// 4B. Cập nhật tức thì (Write-Through Redis + PostgreSQL) trạng thái điểm danh và ghi chú lý do của 1 học sinh
        /// </summary>
        public async Task<(bool Success, string Message, StudentAttendanceRowItem? UpdatedRow)> QuickUpdateSingleAttendanceAsync(AttendanceSingleUpdateDto request)
        {
            var dateUtc = DateTime.SpecifyKind(request.AttendanceDate.Date, DateTimeKind.Utc);
            var sheetKey = RedisAttendanceKeys.ClassSheet(request.ClassId, dateUtc);

            var updateTarget = (request.UpdateTarget ?? "ALL").Trim().ToUpperInvariant();
            if (updateTarget != "CLASS" && updateTarget != "MASS" && updateTarget != "NOTE")
            {
                updateTarget = "ALL";
            }

            bool updateMass = updateTarget == "MASS" || updateTarget == "ALL";
            bool updateClass = updateTarget == "CLASS" || updateTarget == "ALL";
            bool updateNote = updateTarget == "NOTE" || updateTarget == "ALL";

            var normalizedMassStatus = NormalizeAttendanceStatus(request.AttendedMass, request.MassStatus, allowLinedUp: true);
            var normalizedClassStatus = NormalizeAttendanceStatus(request.ClassAttended, request.ClassStatus, allowLinedUp: false);
            var normalizedNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

            // TASK-INF-08: Nếu lớp đang có lượt quét QR chưa flush xuống DB thì đồng bộ trước để bảo toàn dữ liệu khi QuickUpdateSingle đọc bảng Attendances
            var existingCachedSheet = await _cache.GetAsync<RedisClassAttendancePayload>(sheetKey);
            if (existingCachedSheet?.IsDirty == true)
            {
                await FlushClassToDatabaseAsync(request.ClassId, dateUtc);
            }

            var modifiedBy = SafeTruncate(request.ModifiedBy, 100) ?? "GLV";
            var clientIp = SafeTruncate(request.IpAddress, 45);

            StudentAttendanceRowItem updatedRowSnapshot;
            var flushLock = GetClassFlushLock(request.ClassId, dateUtc);
            var sheetLock = GetClassSheetLock(request.ClassId, dateUtc);

            // Tuần tự hóa ghi DB của lớp theo thứ tự khóa chuẩn: ClassFlushLock (ngoài) -> ClassSheetLock (trong)
            await flushLock.WaitAsync();
            try
            {
                StudentAttendanceRowItem? initialRow;
                long preTxnRevision;
                bool studentWasDirtyPreTxn;

                await sheetLock.WaitAsync();
                try
                {
                    var payload = await GetOrLoadClassSheetPayloadLockedAsync(request.ClassId, dateUtc);
                    var foundRow = payload?.Students.FirstOrDefault(s => s.EnrollmentId == request.EnrollmentId);
                    if (payload == null || foundRow == null)
                    {
                        return (false, "Không tìm thấy học sinh trong danh sách lớp này.", null);
                    }

                    initialRow = CloneRow(foundRow);
                    preTxnRevision = payload.Revision;
                    studentWasDirtyPreTxn = payload.IsDirty &&
                                            (payload.DirtyEnrollmentIds == null || payload.DirtyEnrollmentIds.Contains(request.EnrollmentId));
                }
                finally
                {
                    sheetLock.Release();
                }

                bool finalMassAtt = initialRow.AttendedMass;
                string finalMassSt = initialRow.MassStatus;
                TimeSpan? finalMassTime = initialRow.MassCheckInTime;
                bool finalClassAtt = initialRow.ClassAttended;
                string finalClassSt = initialRow.ClassStatus;
                TimeSpan? finalClassTime = initialRow.ClassCheckInTime;
                string? finalNote = initialRow.Note;

                // Write-Through tức thì cho duy nhất bản ghi của thiếu nhi này xuống PostgreSQL + Ghi Audit Log
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    var existing = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.EnrollmentId == request.EnrollmentId && a.AttendanceDate == dateUtc);

                    bool isNew = existing == null;
                    bool oldMassAtt = existing?.AttendedMass ?? false;
                    string oldMassSt = existing?.MassStatus ?? AttendanceStatus.AbsentUnpermitted;
                    bool oldClassAtt = existing?.ClassAttended ?? false;
                    string oldClassSt = existing?.ClassStatus ?? AttendanceStatus.AbsentUnpermitted;
                    string? oldNote = existing?.Note;

                    // Bảo toàn dữ liệu hiện hành trên DB/Redis đối với các cột không nằm trong UpdateTarget
                    finalMassAtt = updateMass ? request.AttendedMass : (existing?.AttendedMass ?? initialRow.AttendedMass);
                    finalMassSt = updateMass ? normalizedMassStatus : (existing?.MassStatus ?? initialRow.MassStatus);
                    finalMassTime = updateMass
                        ? (request.AttendedMass ? (existing?.MassCheckInTime ?? initialRow.MassCheckInTime) : null)
                        : (existing != null ? existing.MassCheckInTime : initialRow.MassCheckInTime);

                    finalClassAtt = updateClass ? request.ClassAttended : (existing?.ClassAttended ?? initialRow.ClassAttended);
                    finalClassSt = updateClass ? normalizedClassStatus : (existing?.ClassStatus ?? initialRow.ClassStatus);
                    finalClassTime = updateClass
                        ? (request.ClassAttended ? (existing?.ClassCheckInTime ?? initialRow.ClassCheckInTime) : null)
                        : (existing != null ? existing.ClassCheckInTime : initialRow.ClassCheckInTime);

                    finalNote = updateNote ? normalizedNote : (existing != null ? existing.Note : initialRow.Note);

                    bool hasChanges = isNew || HasAttendanceStateChanged(
                        oldMassAtt, oldMassSt, finalMassAtt, finalMassSt,
                        oldClassAtt, oldClassSt, finalClassAtt, finalClassSt,
                        oldNote, finalNote);

                    Attendance trackedRecord;
                    if (existing != null)
                    {
                        if (updateMass)
                        {
                            existing.AttendedMass = finalMassAtt;
                            existing.MassStatus = finalMassSt;
                            existing.MassCheckInTime = finalMassTime;
                        }

                        if (updateClass)
                        {
                            existing.ClassAttended = finalClassAtt;
                            existing.ClassStatus = finalClassSt;
                            existing.ClassCheckInTime = finalClassTime;
                        }

                        if (updateNote)
                        {
                            existing.Note = finalNote;
                        }

                        trackedRecord = existing;
                    }
                    else
                    {
                        trackedRecord = new Attendance
                        {
                            EnrollmentId = request.EnrollmentId,
                            AttendanceDate = dateUtc,
                            DayOfWeek = dateUtc.DayOfWeek,
                            AttendedMass = finalMassAtt,
                            MassStatus = finalMassSt,
                            MassCheckInTime = finalMassTime,
                            ClassAttended = finalClassAtt,
                            ClassStatus = finalClassSt,
                            ClassCheckInTime = finalClassTime,
                            Note = finalNote,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Attendances.Add(trackedRecord);
                    }

                    await _context.SaveChangesAsync();

                    if (hasChanges)
                    {
                        bool classChanged = isNew
                            ? (finalClassAtt || finalClassSt != AttendanceStatus.AbsentUnpermitted)
                            : (oldClassAtt != finalClassAtt || !string.Equals(oldClassSt, finalClassSt, StringComparison.OrdinalIgnoreCase));
                        bool massChanged = isNew
                            ? (finalMassAtt || finalMassSt != AttendanceStatus.AbsentUnpermitted)
                            : (oldMassAtt != finalMassAtt || !string.Equals(oldMassSt, finalMassSt, StringComparison.OrdinalIgnoreCase));
                        bool noteChanged = !string.Equals(
                            string.IsNullOrWhiteSpace(oldNote) ? null : oldNote.Trim(),
                            finalNote,
                            StringComparison.Ordinal);

                        string actionType = (classChanged, massChanged, noteChanged) switch
                        {
                            (true, false, false) => "QUICK_UPDATE_CLASS",
                            (false, true, false) => "QUICK_UPDATE_MASS",
                            (false, false, true) => "QUICK_UPDATE_NOTE",
                            _ => "QUICK_UPDATE"
                        };

                        var reasonSummary = BuildAuditReasonSummary(
                            "Cập nhật trực tiếp",
                            isNew,
                            oldMassAtt, oldMassSt, finalMassAtt, finalMassSt,
                            oldClassAtt, oldClassSt, finalClassAtt, finalClassSt,
                            oldNote, finalNote);

                        _context.AttendanceAuditLogs.Add(new AttendanceAuditLog
                        {
                            AttendanceId = trackedRecord.Id,
                            ActionType = actionType,
                            OldValues = FormatStateSnapshot(oldMassAtt, oldMassSt, oldClassAtt, oldClassSt, oldNote, isNewRecord: isNew),
                            NewValues = FormatStateSnapshot(finalMassAtt, finalMassSt, finalClassAtt, finalClassSt, finalNote),
                            ModifiedBy = modifiedBy,
                            Reason = reasonSummary,
                            IpAddress = clientIp,
                            CreatedAt = DateTime.UtcNow
                        });

                        await _context.SaveChangesAsync();

                        _logger.LogInformation(
                            "AUDIT {ActionType}: StudentCode={StudentCode}, Date={Date:yyyy-MM-dd}, Target={Target}, By={User}, IP={IP}, Reason={Reason}",
                            actionType, initialRow.StudentCode, dateUtc, updateTarget, modifiedBy, clientIp ?? "unknown", reasonSummary);
                    }

                    await transaction.CommitAsync();
                });

                // Đồng bộ an toàn dưới per-class lock:
                // - Chỉ ghi đè đúng nhóm cột theo UpdateTarget (không ghi đè lượt quét QR song song cho cột khác trong lúc DB txn chạy).
                // - Chỉ gỡ EnrollmentId khỏi DirtyEnrollmentIds nếu Revision không đổi trong suốt DB txn.
                bool hasConcurrentDirtyMutation = false;
                await sheetLock.WaitAsync();
                try
                {
                    var latestPayload = await GetOrLoadClassSheetPayloadLockedAsync(request.ClassId, dateUtc);
                    var latestRow = latestPayload?.Students.FirstOrDefault(s => s.EnrollmentId == request.EnrollmentId);

                    if (latestPayload != null && latestRow != null)
                    {
                        if (updateMass)
                        {
                            latestRow.AttendedMass = finalMassAtt;
                            latestRow.MassStatus = finalMassSt;
                            latestRow.MassCheckInTime = finalMassTime;
                        }

                        if (updateClass)
                        {
                            latestRow.ClassAttended = finalClassAtt;
                            latestRow.ClassStatus = finalClassSt;
                            latestRow.ClassCheckInTime = finalClassTime;
                        }

                        if (updateNote)
                        {
                            latestRow.Note = finalNote;
                        }

                        if (!studentWasDirtyPreTxn && latestPayload.Revision == preTxnRevision)
                        {
                            latestPayload.DirtyEnrollmentIds?.Remove(request.EnrollmentId);
                            latestPayload.DirtyAuditMetaByEnrollment?.Remove(request.EnrollmentId);

                            if ((latestPayload.DirtyEnrollmentIds == null || latestPayload.DirtyEnrollmentIds.Count == 0) &&
                                (latestPayload.PendingCampaignCheckIns == null || latestPayload.PendingCampaignCheckIns.Count == 0))
                            {
                                latestPayload.IsDirty = false;
                            }
                        }
                        else
                        {
                            // Có lượt quét QR đến song song trước/trong lúc đang ghi DB -> giữ nguyên trạng thái dirty để Write-Behind tiếp tục flush
                            hasConcurrentDirtyMutation = latestPayload.IsDirty;
                        }

                        latestPayload.LastModifiedAtUtc = DateTime.UtcNow;
                        latestPayload.LastModifiedBy = modifiedBy;
                        latestPayload.LastModifiedIp = clientIp;

                        await _cache.SetAsync(sheetKey, latestPayload, DefaultCacheTtl);
                        updatedRowSnapshot = CloneRow(latestRow);
                    }
                    else
                    {
                        initialRow.AttendedMass = finalMassAtt;
                        initialRow.MassStatus = finalMassSt;
                        initialRow.MassCheckInTime = finalMassTime;
                        initialRow.ClassAttended = finalClassAtt;
                        initialRow.ClassStatus = finalClassSt;
                        initialRow.ClassCheckInTime = finalClassTime;
                        initialRow.Note = finalNote;
                        updatedRowSnapshot = CloneRow(initialRow);
                    }
                }
                finally
                {
                    sheetLock.Release();
                }

                if (hasConcurrentDirtyMutation && _cache.IsRedisAvailable)
                {
                    _flushSignal.NotifyDirty(dateUtc, request.ClassId);
                }
            }
            finally
            {
                flushLock.Release();
            }

            await _cache.RemoveAsync(RedisAttendanceKeys.ClassSessionInfo(request.ClassId, dateUtc));
            await _cache.RemoveAsync($"staff_lookup:student_360:{updatedRowSnapshot.StudentId}");

            var displayName = $"{updatedRowSnapshot.ChristianName} {updatedRowSnapshot.FullName}".Trim();
            return (true, $"Đã lưu điểm danh: {displayName}", updatedRowSnapshot);
        }

        /// <summary>
        /// 5. TASK-P3-12.3 / TASK-INF-08: Quét mã QR tốc độ cao ưu tiên 100% trên Redis (0 truy vấn PostgreSQL khi Cache Hit).
        /// - Khi Redis khỏe: cập nhật Redis ClassSheet (IsDirty = true) + báo tín hiệu gom nhóm Write-Behind ~5s xuống PostgreSQL.
        /// - Khi Redis sự cố (Circuit Breaker mở): tự động Fallback ghi trực tiếp 1 bản ghi duy nhất xuống PostgreSQL để không mất dữ liệu.
        /// </summary>
        public async Task<QrScanResponseDto> ProcessQrCheckInViaRedisAsync(QrScanRequestDto request)
        {
            var cleanCode = request.StudentCode.Trim().ToUpperInvariant();
            var scanTime = request.ScanTimestamp ?? GetVietnamNow();
            var todayUtc = DateTime.SpecifyKind(scanTime.Date, DateTimeKind.Utc);

            var qrKey = RedisAttendanceKeys.QrStudentLookup(todayUtc, cleanCode);
            var lookupItem = await _cache.GetAsync<RedisQrStudentLookupItem>(qrKey);

            bool isHistoricalCode = false;
            string? oldCode = null;

            // Nếu chưa có trên Redis (Cache Miss) -> kiểm tra Negative Cache 30s trước, sau đó mới truy vấn PostgreSQL với projection gọn
            if (lookupItem == null)
            {
                var negativeKey = RedisAttendanceKeys.QrNegativeLookup(todayUtc, cleanCode);
                var recentMiss = await _cache.GetAsync<string>(negativeKey);
                if (!string.IsNullOrEmpty(recentMiss))
                {
                    return new QrScanResponseDto
                    {
                        Success = false,
                        StudentCode = cleanCode,
                        Message = recentMiss
                    };
                }

                var currentYear = await GetCurrentAcademicYearCachedAsync();
                if (currentYear == null)
                {
                    return new QrScanResponseDto { Success = false, Message = "Chưa có niên khóa nào được kích hoạt!" };
                }

                var enrollmentMeta = await _context.Enrollments
                    .AsNoTracking()
                    .Where(e => e.Student.StudentCode.ToUpper() == cleanCode &&
                                e.ClassRoom.AcademicYearId == currentYear.Id &&
                                e.Student.IsActive)
                    .Select(e => new
                    {
                        e.Id,
                        e.StudentId,
                        e.ClassRoomId,
                        StudentCode = e.Student.StudentCode,
                        e.Student.ChristianName,
                        e.Student.FirstName,
                        e.Student.LastName,
                        e.Student.Gender,
                        ClassName = e.ClassRoom.Name,
                        e.ClassRoom.GradeLevel
                    })
                    .FirstOrDefaultAsync();

                // TASK-P5-04.2: Nếu không khớp mã hiện hành, tra cứu trong bảng lịch sử StudentCodeHistories
                if (enrollmentMeta == null)
                {
                    var historyStudentId = await _context.StudentCodeHistories
                        .AsNoTracking()
                        .Where(h => (h.OriginalCode.ToUpper() == cleanCode || h.ArchivedYearCode.ToUpper() == cleanCode) &&
                                    h.Student != null && h.Student.IsActive)
                        .OrderByDescending(h => h.ChangedAt)
                        .Select(h => (long?)h.StudentId)
                        .FirstOrDefaultAsync();

                    if (historyStudentId.HasValue)
                    {
                        enrollmentMeta = await _context.Enrollments
                            .AsNoTracking()
                            .Where(e => e.StudentId == historyStudentId.Value &&
                                        e.ClassRoom.AcademicYearId == currentYear.Id &&
                                        e.Student.IsActive)
                            .Select(e => new
                            {
                                e.Id,
                                e.StudentId,
                                e.ClassRoomId,
                                StudentCode = e.Student.StudentCode,
                                e.Student.ChristianName,
                                e.Student.FirstName,
                                e.Student.LastName,
                                e.Student.Gender,
                                ClassName = e.ClassRoom.Name,
                                e.ClassRoom.GradeLevel
                            })
                            .FirstOrDefaultAsync();

                        if (enrollmentMeta != null)
                        {
                            isHistoricalCode = true;
                            oldCode = cleanCode;
                        }
                    }
                }

                if (enrollmentMeta == null)
                {
                    var notFoundMsg = $"Không tìm thấy thiếu nhi có mã '{cleanCode}' trong niên khóa {currentYear.Name}!";
                    await _cache.SetAsync(negativeKey, notFoundMsg, NegativeQrLookupTtl);
                    return new QrScanResponseDto
                    {
                        Success = false,
                        StudentCode = cleanCode,
                        Message = notFoundMsg
                    };
                }

                var officialCode = enrollmentMeta.StudentCode.Trim().ToUpperInvariant();
                lookupItem = new RedisQrStudentLookupItem
                {
                    EnrollmentId = enrollmentMeta.Id,
                    StudentId = enrollmentMeta.StudentId,
                    StudentCode = officialCode,
                    ChristianName = enrollmentMeta.ChristianName,
                    FullName = $"{enrollmentMeta.FirstName} {enrollmentMeta.LastName}".Trim(),
                    Gender = enrollmentMeta.Gender ?? "Nam",
                    ClassId = enrollmentMeta.ClassRoomId,
                    ClassName = enrollmentMeta.ClassName,
                    GradeLevel = enrollmentMeta.GradeLevel
                };

                // Đảm bảo sổ lớp & các khóa QR của lớp đã nạp lên Redis (single-flight)
                await GetOrLoadClassSheetAsync(enrollmentMeta.ClassRoomId, todayUtc);

                // Đảm bảo khóa chính và alias thẻ năm cũ luôn có trong Redis cho lần quét kế tiếp đạt O(1)
                await _cache.SetAsync(RedisAttendanceKeys.QrStudentLookup(todayUtc, officialCode), lookupItem, DefaultCacheTtl);
                if (isHistoricalCode && !string.IsNullOrEmpty(oldCode))
                {
                    await _cache.SetAsync(qrKey, lookupItem, DefaultCacheTtl);
                }
            }
            else
            {
                // Kiểm tra nếu lookupItem có mã chính thức khác với mã vừa quét -> thẻ quét là mã năm cũ
                if (!string.Equals(lookupItem.StudentCode, cleanCode, StringComparison.OrdinalIgnoreCase))
                {
                    isHistoricalCode = true;
                    oldCode = cleanCode;
                }
            }

            // TASK-P5-04.2: Xử lý theo SessionMode được chọn nhanh trên giao diện Camera
            bool isMass = false;
            bool isClass = false;
            string status = AttendanceStatus.Present;
            var timeOfDay = scanTime.TimeOfDay;
            string sessionMode = (request.SessionMode ?? "AUTO").ToUpperInvariant();
            long? activeCampaignId = null;

            switch (sessionMode)
            {
                case "SUNDAY_MASS":
                    isMass = true;
                    isClass = false;
                    if (timeOfDay < new TimeSpan(6, 35, 0)) status = AttendanceStatus.LinedUp;
                    else if (timeOfDay < new TimeSpan(7, 0, 0)) status = AttendanceStatus.Present;
                    else status = AttendanceStatus.Late;
                    break;

                case "THURSDAY_MASS":
                    isMass = true;
                    isClass = false;
                    status = AttendanceStatus.Present;
                    break;

                case "CATECHISM_CLASS":
                    isMass = false;
                    isClass = true;
                    bool isMorning = !GradeLevel.IsAfternoonGrade(lookupItem.GradeLevel);
                    if (isMorning)
                    {
                        status = timeOfDay < new TimeSpan(9, 30, 0) ? AttendanceStatus.Present : AttendanceStatus.Late;
                    }
                    else
                    {
                        status = timeOfDay < new TimeSpan(15, 15, 0) ? AttendanceStatus.Present : AttendanceStatus.Late;
                    }
                    break;

                case "CAMPAIGN_MONTH":
                    isMass = true;
                    isClass = false;
                    status = AttendanceStatus.Present;
                    activeCampaignId = request.CampaignId.HasValue && request.CampaignId.Value > 0
                        ? request.CampaignId.Value
                        : await GetActiveCampaignIdCachedAsync(lookupItem.ClassId, todayUtc);
                    break;

                default: // "AUTO"
                    var eval = AttendanceTimeHelper.EvaluateCheckIn(scanTime, lookupItem.GradeLevel);
                    isMass = eval.isMass;
                    isClass = eval.isClass;
                    status = eval.calculatedStatus;
                    if (!isMass && !isClass)
                    {
                        isClass = true;
                        status = AttendanceStatus.Present;
                    }
                    break;
            }

            string activityTitle = sessionMode switch
            {
                "CAMPAIGN_MONTH" => "CHIẾN DỊCH 1 THÁNG",
                "THURSDAY_MASS" => "THÁNH LỄ THỨ NĂM",
                "SUNDAY_MASS" => "THÁNH LỄ CHÚA NHẬT",
                "CATECHISM_CLASS" => "GIỜ HỌC GIÁO LÝ",
                _ => (isMass ? "THÁNH LỄ" : "GIỜ HỌC GIÁO LÝ")
            };

            var modifiedBy = SafeTruncate(request.ModifiedBy, 100) ?? "Camera QR HUD";
            var clientIp = SafeTruncate(request.IpAddress, 45);
            var sheetKey = RedisAttendanceKeys.ClassSheet(lookupItem.ClassId, todayUtc);

            bool isDuplicate = false;
            bool shouldPersistState = false;
            bool shouldQueueCampaign = false;
            StudentAttendanceRowItem? updatedRowSnapshot = null;

            // Kiểm tra dedup chiến dịch (nếu ở chế độ CAMPAIGN_MONTH có chiến dịch đang mở)
            if (activeCampaignId.HasValue)
            {
                var campaignScanKey = RedisAttendanceKeys.CampaignScanMarker(activeCampaignId.Value, lookupItem.EnrollmentId, todayUtc);
                var alreadyCampaignMarked = await _cache.GetAsync<string>(campaignScanKey);
                if (string.IsNullOrEmpty(alreadyCampaignMarked))
                {
                    shouldQueueCampaign = true;
                }
            }

            // Cập nhật Redis ClassSheet an toàn dưới per-class striped lock (không mất lượt quét khi nhiều GLV quét cùng lớp)
            var sheetLock = GetClassSheetLock(lookupItem.ClassId, todayUtc);
            await sheetLock.WaitAsync();
            try
            {
                var payload = await GetOrLoadClassSheetPayloadLockedAsync(lookupItem.ClassId, todayUtc);
                var targetRow = payload?.Students.FirstOrDefault(s => s.EnrollmentId == lookupItem.EnrollmentId);

                if (payload == null || targetRow == null)
                {
                    return new QrScanResponseDto
                    {
                        Success = false,
                        StudentCode = cleanCode,
                        Message = "Học sinh chưa có trong danh sách bộ đệm của lớp."
                    };
                }

                if (isMass)
                {
                    if (targetRow.AttendedMass)
                    {
                        isDuplicate = true;
                    }
                    else
                    {
                        targetRow.AttendedMass = true;
                        targetRow.MassStatus = status;
                        targetRow.MassCheckInTime = timeOfDay;
                        shouldPersistState = true;
                    }
                }

                if (isClass)
                {
                    if (targetRow.ClassAttended)
                    {
                        isDuplicate = true;
                    }
                    else
                    {
                        targetRow.ClassAttended = true;
                        targetRow.ClassStatus = status;
                        targetRow.ClassCheckInTime = timeOfDay;
                        shouldPersistState = true;
                    }
                }

                if (shouldPersistState || shouldQueueCampaign)
                {
                    payload.DirtyEnrollmentIds ??= new HashSet<long>();
                    payload.DirtyAuditMetaByEnrollment ??= new Dictionary<long, RedisDirtyAuditMeta>();

                    if (shouldPersistState)
                    {
                        payload.DirtyEnrollmentIds.Add(lookupItem.EnrollmentId);
                        payload.DirtyAuditMetaByEnrollment[lookupItem.EnrollmentId] = new RedisDirtyAuditMeta
                        {
                            ActionType = "QR_SCAN",
                            ModifiedBy = modifiedBy,
                            IpAddress = clientIp
                        };
                    }

                    if (shouldQueueCampaign && activeCampaignId.HasValue)
                    {
                        payload.PendingCampaignCheckIns ??= new List<RedisPendingCampaignCheckIn>();
                        bool existsInQueue = payload.PendingCampaignCheckIns.Any(c =>
                            c.CampaignId == activeCampaignId.Value &&
                            c.EnrollmentId == lookupItem.EnrollmentId &&
                            c.AttendanceDate == todayUtc);

                        if (!existsInQueue)
                        {
                            payload.PendingCampaignCheckIns.Add(new RedisPendingCampaignCheckIn
                            {
                                CampaignId = activeCampaignId.Value,
                                StudentId = lookupItem.StudentId,
                                EnrollmentId = lookupItem.EnrollmentId,
                                AttendanceDate = todayUtc,
                                ModifiedBy = modifiedBy,
                                CheckInTimeUtc = DateTime.UtcNow
                            });
                        }
                    }

                    payload.LastModifiedAtUtc = DateTime.UtcNow;
                    payload.LastModifiedBy = modifiedBy;
                    payload.LastModifiedIp = clientIp;
                    payload.LastActionType = "QR_SCAN";
                    payload.IsDirty = true;
                    payload.Revision++;

                    updatedRowSnapshot = CloneRow(targetRow);
                    await _cache.SetAsync(sheetKey, payload, DefaultCacheTtl);
                }
            }
            finally
            {
                sheetLock.Release();
            }

            if (shouldQueueCampaign && activeCampaignId.HasValue)
            {
                var campaignScanKey = RedisAttendanceKeys.CampaignScanMarker(activeCampaignId.Value, lookupItem.EnrollmentId, todayUtc);
                await _cache.SetAsync(campaignScanKey, "1", DefaultCacheTtl);
            }

            if (shouldPersistState || shouldQueueCampaign)
            {
                if (_cache.IsRedisAvailable)
                {
                    // Đường nóng (Hot Path): Redis AOF khỏe -> đánh dấu phân vùng dirty + báo tín hiệu gom nhóm Write-Behind (~5s)
                    // Không chặn Request bằng DB transaction => độ trễ ~10-25ms, 0 truy vấn PostgreSQL trên mỗi lượt quét!
                    await MarkClassDirtyAsync(lookupItem.ClassId, todayUtc);
                    _flushSignal.NotifyDirty(todayUtc, lookupItem.ClassId);
                }
                else
                {
                    // Fallback an toàn khi Redis sự cố / Circuit Breaker mở: ghi thẳng 1 bản ghi duy nhất xuống PostgreSQL để không mất điểm danh!
                    await PersistSingleQrScanFallbackAsync(
                        lookupItem.ClassId,
                        todayUtc,
                        updatedRowSnapshot,
                        shouldPersistState,
                        shouldQueueCampaign ? activeCampaignId : null,
                        lookupItem.StudentId,
                        lookupItem.EnrollmentId,
                        modifiedBy,
                        clientIp);
                }
            }

            string resultMsg;
            if (isDuplicate)
            {
                resultMsg = $"Thiếu nhi đã được điểm danh {activityTitle} trước đó!";
            }
            else if (isHistoricalCode)
            {
                resultMsg = $"[Thẻ Cũ] Điểm danh thành công: {lookupItem.FullName} - Nhớ in đổi thẻ mới!";
            }
            else
            {
                resultMsg = $"[Redis Fast Check-in] Điểm danh thành công: {lookupItem.FullName} ({status})";
            }

            return new QrScanResponseDto
            {
                Success = true,
                IsDuplicate = isDuplicate,
                StudentCode = lookupItem.StudentCode,
                ChristianName = lookupItem.ChristianName,
                FullName = lookupItem.FullName,
                ClassName = lookupItem.ClassName,
                GradeLevel = lookupItem.GradeLevel,
                ActivityType = activityTitle,
                Status = status,
                CheckInTimeStr = timeOfDay.ToString(@"hh\:mm\:ss"),
                IsHistoricalCode = isHistoricalCode,
                OldCode = isHistoricalCode ? oldCode : null,
                CurrentCode = lookupItem.StudentCode,
                WarningMessage = isHistoricalCode
                    ? $"CẢNH BÁO: Thẻ mã năm cũ ({oldCode})! Mã mới: {lookupItem.StudentCode} - Cần in lại thẻ mới!"
                    : null,
                EnrollmentId = lookupItem.EnrollmentId,
                ClassId = lookupItem.ClassId,
                SessionMode = sessionMode,
                ScanDate = todayUtc,
                Message = resultMsg
            };
        }

        /// <summary>
        /// TASK-P5-04.3: Hoàn tác lượt quét QR vừa thực hiện trong vòng 5 giây
        /// </summary>
        public async Task<(bool Success, string Message)> UndoScanCheckInAsync(UndoScanRequestDto dto)
        {
            if (dto.EnrollmentId <= 0 || dto.ClassId <= 0)
            {
                return (false, "Dữ liệu yêu cầu hoàn tác không hợp lệ.");
            }

            var date = dto.AttendanceDate ?? GetVietnamNow().Date;
            var todayUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var sheetKey = RedisAttendanceKeys.ClassSheet(dto.ClassId, todayUtc);
            var undoOperator = SafeTruncate(dto.ModifiedBy, 100) ?? "Camera QR HUD";
            var undoIp = SafeTruncate(dto.IpAddress, 45);

            bool undoMass = string.IsNullOrWhiteSpace(dto.ActivityType) ||
                            dto.ActivityType.Contains("LỄ") ||
                            dto.ActivityType.Contains("MASS") ||
                            dto.ActivityType.Contains("CHIẾN DỊCH");

            bool undoClass = string.IsNullOrWhiteSpace(dto.ActivityType) ||
                             dto.ActivityType.Contains("GIÁO LÝ") ||
                             dto.ActivityType.Contains("CLASS");

            bool undoCampaign = !string.IsNullOrWhiteSpace(dto.ActivityType) &&
                                dto.ActivityType.Contains("CHIẾN DỊCH");

            string studentName = dto.StudentCode ?? "Thiếu nhi";
            long? targetStudentId = null;
            List<long>? removedCampaignIds = null;

            // 1. Thu hồi trạng thái trên Redis ClassSheet dưới per-class lock (ngăn race với luồng quét/flush)
            var sheetLock = GetClassSheetLock(dto.ClassId, todayUtc);
            await sheetLock.WaitAsync();
            try
            {
                var payload = await GetOrLoadClassSheetPayloadLockedAsync(dto.ClassId, todayUtc);
                var targetRow = payload?.Students.FirstOrDefault(s => s.EnrollmentId == dto.EnrollmentId);

                if (payload != null && targetRow != null)
                {
                    studentName = targetRow.FullName;
                    targetStudentId = targetRow.StudentId;

                    if (undoMass)
                    {
                        targetRow.AttendedMass = false;
                        targetRow.MassStatus = AttendanceStatus.AbsentUnpermitted;
                        targetRow.MassCheckInTime = null;
                    }

                    if (undoClass)
                    {
                        targetRow.ClassAttended = false;
                        targetRow.ClassStatus = AttendanceStatus.AbsentUnpermitted;
                        targetRow.ClassCheckInTime = null;
                    }

                    if (undoCampaign && payload.PendingCampaignCheckIns != null)
                    {
                        removedCampaignIds = payload.PendingCampaignCheckIns
                            .Where(c => c.EnrollmentId == dto.EnrollmentId && c.AttendanceDate == todayUtc)
                            .Select(c => c.CampaignId)
                            .Distinct()
                            .ToList();

                        payload.PendingCampaignCheckIns.RemoveAll(c =>
                            c.EnrollmentId == dto.EnrollmentId && c.AttendanceDate == todayUtc);
                    }

                    payload.DirtyEnrollmentIds ??= new HashSet<long>();
                    payload.DirtyEnrollmentIds.Add(dto.EnrollmentId);
                    payload.DirtyAuditMetaByEnrollment ??= new Dictionary<long, RedisDirtyAuditMeta>();
                    payload.DirtyAuditMetaByEnrollment[dto.EnrollmentId] = new RedisDirtyAuditMeta
                    {
                        ActionType = "QR_UNDO",
                        ModifiedBy = undoOperator,
                        IpAddress = undoIp
                    };

                    payload.LastModifiedAtUtc = DateTime.UtcNow;
                    payload.LastModifiedBy = undoOperator;
                    payload.LastModifiedIp = undoIp;
                    payload.LastActionType = "QR_UNDO";
                    payload.IsDirty = true;
                    payload.Revision++;

                    await _cache.SetAsync(sheetKey, payload, DefaultCacheTtl);
                }
            }
            finally
            {
                sheetLock.Release();
            }

            if (removedCampaignIds != null)
            {
                foreach (var campId in removedCampaignIds)
                {
                    await _cache.RemoveAsync(RedisAttendanceKeys.CampaignScanMarker(campId, dto.EnrollmentId, todayUtc));
                }
            }

            // 2. Nếu bản ghi đã tồn tại trong PostgreSQL (hoặc thuộc chiến dịch), cập nhật thu hồi trong Transaction an toàn
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var att = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.EnrollmentId == dto.EnrollmentId && a.AttendanceDate == todayUtc);

                if (att != null)
                {
                    bool oldMassAtt = att.AttendedMass;
                    string oldMassSt = att.MassStatus;
                    bool oldClassAtt = att.ClassAttended;
                    string oldClassSt = att.ClassStatus;
                    string? oldNote = att.Note;

                    if (undoMass)
                    {
                        att.AttendedMass = false;
                        att.MassStatus = AttendanceStatus.AbsentUnpermitted;
                        att.MassCheckInTime = null;
                    }

                    if (undoClass)
                    {
                        att.ClassAttended = false;
                        att.ClassStatus = AttendanceStatus.AbsentUnpermitted;
                        att.ClassCheckInTime = null;
                    }

                    await _context.SaveChangesAsync();

                    var undoReason = SafeTruncate($"Hoàn tác lượt quét QR ({dto.ActivityType ?? "Điểm danh"}) trong 5 giây", 255);

                    _context.AttendanceAuditLogs.Add(new AttendanceAuditLog
                    {
                        AttendanceId = att.Id,
                        ActionType = "QR_UNDO",
                        OldValues = FormatStateSnapshot(oldMassAtt, oldMassSt, oldClassAtt, oldClassSt, oldNote),
                        NewValues = FormatStateSnapshot(att.AttendedMass, att.MassStatus, att.ClassAttended, att.ClassStatus, att.Note),
                        ModifiedBy = undoOperator,
                        Reason = undoReason,
                        IpAddress = undoIp,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "AUDIT QR_UNDO: EnrollmentId={EnrollmentId}, Date={Date:yyyy-MM-dd}, By={User}, IP={IP}",
                        dto.EnrollmentId, todayUtc, undoOperator, undoIp ?? "unknown");
                }

                if (undoCampaign)
                {
                    var campRecord = await _context.MassCampaignRecords
                        .FirstOrDefaultAsync(r => r.EnrollmentId == dto.EnrollmentId && r.AttendanceDate == todayUtc);
                    if (campRecord != null)
                    {
                        campRecord.Status = "ABSENT";
                        campRecord.LastModifiedBy = "Camera QR HUD (Undo)";
                        campRecord.LastModifiedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();
            });

            if (_cache.IsRedisAvailable)
            {
                await MarkClassDirtyAsync(dto.ClassId, todayUtc);
                _flushSignal.NotifyDirty(todayUtc, dto.ClassId);
            }

            await _cache.RemoveAsync(RedisAttendanceKeys.ClassSessionInfo(dto.ClassId, todayUtc));
            if (targetStudentId.HasValue)
            {
                await _cache.RemoveAsync($"staff_lookup:student_360:{targetStudentId.Value}");
            }

            return (true, $"Đã hoàn tác điểm danh thành công cho thiếu nhi {studentName}!");
        }

        /// <summary>
        /// 6. TASK-P3-12.4 / TASK-INF-08: Batch Upsert dữ liệu điểm danh còn cờ IsDirty từ Redis xuống PostgreSQL
        /// (chạy bởi Write-Behind BackgroundService, Graceful Shutdown, Flush thủ công và Hangfire safety-net mỗi 2 phút).
        /// </summary>
        public async Task<(int ClassesSynced, int RecordsUpserted)> SyncDirtyAttendancesToDatabaseAsync(DateTime? targetDate = null)
        {
            var datesToSync = ResolveSyncDatesUtc(targetDate);

            int classesSynced = 0;
            int recordsUpserted = 0;

            foreach (var dt in datesToSync)
            {
                var dirtyKey = RedisAttendanceKeys.DirtyPartitionsIndex(dt);
                var dirtyClassIds = await _cache.GetAsync<HashSet<int>>(dirtyKey);

                if (dirtyClassIds == null || dirtyClassIds.Count == 0)
                    continue;

                var cleanedClassIds = new HashSet<int>();

                foreach (var classId in dirtyClassIds.ToList())
                {
                    try
                    {
                        var (affected, stillDirty) = await FlushSingleClassByIdAsync(classId, dt);
                        if (affected > 0)
                        {
                            recordsUpserted += affected;
                            classesSynced++;
                        }

                        if (!stillDirty)
                        {
                            cleanedClassIds.Add(classId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SyncBackJob] Lỗi khi đồng bộ điểm danh từ Redis về DB cho Lớp ID={ClassId}, Ngày={Date:yyyy-MM-dd}", classId, dt);
                    }
                }

                if (cleanedClassIds.Count > 0)
                {
                    await RemoveCleanClassesFromDirtyIndexAsync(dt, cleanedClassIds);
                }
            }

            if (classesSynced > 0)
            {
                _logger.LogInformation("[SyncBackJob] Đã đồng bộ thành công {Records} bản ghi của {Classes} lớp từ Redis xuống PostgreSQL.",
                    recordsUpserted, classesSynced);
            }

            return (classesSynced, recordsUpserted);
        }

        /// <summary>
        /// TASK-INF-08: Đồng bộ tức thì duy nhất 1 lớp (dùng cho SaveBatch để không chặn các lớp khác).
        /// </summary>
        public async Task<(int ClassesSynced, int RecordsUpserted)> FlushClassToDatabaseAsync(int classId, DateTime targetDate)
        {
            if (classId <= 0) return (0, 0);

            var dt = DateTime.SpecifyKind(targetDate.Date, DateTimeKind.Utc);
            var (affected, stillDirty) = await FlushSingleClassByIdAsync(classId, dt);

            if (!stillDirty)
            {
                await RemoveCleanClassesFromDirtyIndexAsync(dt, new[] { classId });
            }

            return (affected > 0 ? 1 : 0, affected);
        }

        /// <summary>
        /// TASK-INF-08: Đồng bộ theo lô một tập phân vùng (ClassId, DateUtc) cụ thể.
        /// </summary>
        public async Task<(int ClassesSynced, int RecordsUpserted)> SyncDirtyClassesForPartitionsAsync(IEnumerable<(int ClassId, DateTime DateUtc)> partitions)
        {
            var groupedByDate = partitions
                .Where(p => p.ClassId > 0)
                .Select(p => (p.ClassId, DateUtc: DateTime.SpecifyKind(p.DateUtc.Date, DateTimeKind.Utc)))
                .Distinct()
                .GroupBy(p => p.DateUtc);

            int classesSynced = 0;
            int recordsUpserted = 0;

            foreach (var group in groupedByDate)
            {
                var dt = group.Key;
                var cleanedClassIds = new HashSet<int>();

                foreach (var (classId, _) in group)
                {
                    try
                    {
                        var (affected, stillDirty) = await FlushSingleClassByIdAsync(classId, dt);
                        if (affected > 0)
                        {
                            recordsUpserted += affected;
                            classesSynced++;
                        }

                        if (!stillDirty)
                        {
                            cleanedClassIds.Add(classId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[SyncPartitions] Lỗi đồng bộ điểm danh cho Lớp ID={ClassId}, Ngày={Date:yyyy-MM-dd}",
                            classId, dt);
                    }
                }

                if (cleanedClassIds.Count > 0)
                {
                    await RemoveCleanClassesFromDirtyIndexAsync(dt, cleanedClassIds);
                }
            }

            if (classesSynced > 0)
            {
                _logger.LogInformation(
                    "[WriteBehindFlush] Đã đồng bộ theo lô {Records} bản ghi của {Classes} lớp từ Redis xuống PostgreSQL.",
                    recordsUpserted, classesSynced);
            }

            return (classesSynced, recordsUpserted);
        }

        public async Task<int> AutoMarkAbsentUncheckedStudentsAsync(DateTime? targetDate = null)
        {
            var vnNow = GetVietnamNow();
            var rawDate = targetDate?.Date ?? vnNow.Date;
            var dateUtc = DateTime.SpecifyKind(rawDate, DateTimeKind.Utc);

            // Chỉ tự động đánh vắng cho ngày sinh hoạt định kỳ: Thứ Năm (Lễ T5) và Chúa Nhật (Lễ CN + Học GL)
            if (dateUtc.DayOfWeek != DayOfWeek.Thursday && dateUtc.DayOfWeek != DayOfWeek.Sunday)
            {
                return 0;
            }

            // 1. Đồng bộ toàn bộ dữ liệu đang chờ trên Redis xuống DB trước khi rà soát
            await SyncDirtyAttendancesToDatabaseAsync(dateUtc);

            var currentYear = await GetCurrentAcademicYearCachedAsync();
            if (currentYear == null) return 0;

            // 2. Lấy toàn bộ danh sách Enrollment đang theo học trong niên khóa hiện hành
            var activeEnrollmentIds = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.ClassRoom.AcademicYearId == currentYear.Id && e.Student.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

            if (!activeEnrollmentIds.Any()) return 0;

            // 3. Lấy danh sách EnrollmentId đã có bản ghi Attendance trong ngày (dùng HashSet O(1))
            var recordedEnrollmentIds = (await _context.Attendances
                .AsNoTracking()
                .Where(a => a.AttendanceDate == dateUtc && activeEnrollmentIds.Contains(a.EnrollmentId))
                .Select(a => a.EnrollmentId)
                .ToListAsync()).ToHashSet();

            // 4. Kiểm tra các đơn xin phép nghỉ đã được duyệt (APPROVED) cho ngày này
            var approvedAbsences = await _context.AbsenceRequests
                .AsNoTracking()
                .Where(r => r.AbsenceDate == dateUtc && r.Status == "APPROVED" && activeEnrollmentIds.Contains(r.EnrollmentId))
                .Select(r => new { r.EnrollmentId, r.SessionType })
                .ToListAsync();

            var approvedAbsenceMap = approvedAbsences
                .GroupBy(r => r.EnrollmentId)
                .ToDictionary(g => g.Key, g => g.First().SessionType);

            var missingEnrollmentIds = activeEnrollmentIds
                .Where(id => !recordedEnrollmentIds.Contains(id))
                .ToList();

            if (!missingEnrollmentIds.Any()) return 0;

            bool isSunday = dateUtc.DayOfWeek == DayOfWeek.Sunday;
            var newAbsentRecords = new List<Attendance>(missingEnrollmentIds.Count);

            foreach (var enrollmentId in missingEnrollmentIds)
            {
                approvedAbsenceMap.TryGetValue(enrollmentId, out var permittedSession);

                bool massPermitted = permittedSession == "MASS" || permittedSession == "ALL";
                bool classPermitted = permittedSession == "CLASS" || permittedSession == "ALL";

                newAbsentRecords.Add(new Attendance
                {
                    EnrollmentId = enrollmentId,
                    AttendanceDate = dateUtc,
                    DayOfWeek = dateUtc.DayOfWeek,
                    AttendedMass = false,
                    MassStatus = massPermitted ? AttendanceStatus.AbsentPermitted : AttendanceStatus.AbsentUnpermitted,
                    ClassAttended = false,
                    ClassStatus = isSunday
                        ? (classPermitted ? AttendanceStatus.AbsentPermitted : AttendanceStatus.AbsentUnpermitted)
                        : AttendanceStatus.AbsentUnpermitted,
                    IsMakeUp = false,
                    Note = (massPermitted || classPermitted)
                        ? "[Hệ thống]: Vắng có phép theo đơn đã duyệt"
                        : "[Hệ thống]: Tự động ghi nhận vắng cuối ngày",
                    CreatedAt = DateTime.UtcNow
                });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                await _context.Attendances.AddRangeAsync(newAbsentRecords);
                await _context.SaveChangesAsync();

                var autoAuditLogs = newAbsentRecords.Select(rec => new AttendanceAuditLog
                {
                    AttendanceId = rec.Id,
                    ActionType = "AUTO_ABSENT",
                    OldValues = "Chưa điểm danh",
                    NewValues = FormatStateSnapshot(rec.AttendedMass, rec.MassStatus, rec.ClassAttended, rec.ClassStatus, rec.Note),
                    ModifiedBy = "SYSTEM (Hangfire)",
                    Reason = SafeTruncate(rec.Note, 255),
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                if (autoAuditLogs.Any())
                {
                    await _context.AttendanceAuditLogs.AddRangeAsync(autoAuditLogs);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            });

            _logger.LogInformation(
                "[Hangfire AutoAbsent] Đã tự động ghi nhận vắng xuống DB cho {Count} thiếu nhi chưa điểm danh ngày {Date:dd/MM/yyyy}.",
                newAbsentRecords.Count, dateUtc);

            return newAbsentRecords.Count;
        }

        #region Helpers Nội bộ
        private async Task<AcademicYear?> GetCurrentAcademicYearCachedAsync()
        {
            var cached = await _cache.GetAsync<AcademicYear>(CacheKeys.CurrentAcademicYear);
            if (cached != null && cached.IsCurrent)
            {
                return cached;
            }

            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            if (currentYear != null)
            {
                await _cache.SetAsync(CacheKeys.CurrentAcademicYear, currentYear, CurrentYearCacheTtl);
            }

            return currentYear;
        }

        private async Task<long?> GetActiveCampaignIdCachedAsync(int classId, DateTime dateUtc)
        {
            var cacheKey = RedisAttendanceKeys.ActiveCampaignByClass(classId, dateUtc);
            var cached = await _cache.GetAsync<RedisActiveCampaignCacheItem>(cacheKey);
            if (cached != null)
            {
                return cached.HasActiveCampaign ? cached.CampaignId : null;
            }

            var activeCampaignId = await _context.MassCampaigns
                .AsNoTracking()
                .Where(c => c.ClassRoomId == classId &&
                            c.Status == "ACTIVE" &&
                            dateUtc >= c.StartDate.Date &&
                            dateUtc <= c.EndDate.Date)
                .Select(c => (long?)c.Id)
                .FirstOrDefaultAsync();

            await _cache.SetAsync(cacheKey, new RedisActiveCampaignCacheItem
            {
                HasActiveCampaign = activeCampaignId.HasValue,
                CampaignId = activeCampaignId
            }, ActiveCampaignCacheTtl);

            return activeCampaignId;
        }

        /// <summary>
        /// Chỉ được gọi khi đã nắm giữ ClassSheetLock(classId, dateUtc).
        /// </summary>
        private async Task<RedisClassAttendancePayload?> GetOrLoadClassSheetPayloadLockedAsync(int classId, DateTime dateUtc)
        {
            var sheetKey = RedisAttendanceKeys.ClassSheet(classId, dateUtc);
            var cachedSheet = await _cache.GetAsync<RedisClassAttendancePayload>(sheetKey);
            if (cachedSheet != null)
            {
                return cachedSheet;
            }

            var targetClass = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Id == classId)
                .Select(c => new { c.Id, c.Name, c.GradeLevel })
                .FirstOrDefaultAsync();

            if (targetClass == null)
            {
                return null;
            }

            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.ClassRoomId == classId && e.Student.IsActive)
                .OrderBy(e => e.Student.StudentCode)
                .ThenBy(e => e.Student.LastName)
                .Select(e => new
                {
                    e.Id,
                    e.StudentId,
                    e.Student.StudentCode,
                    e.Student.ChristianName,
                    e.Student.FirstName,
                    e.Student.LastName,
                    e.Student.Gender
                })
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

            var attendances = enrollmentIds.Count > 0
                ? await _context.Attendances
                    .AsNoTracking()
                    .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.AttendanceDate == dateUtc)
                    .Select(a => new
                    {
                        a.EnrollmentId,
                        a.AttendedMass,
                        a.MassStatus,
                        a.MassCheckInTime,
                        a.ClassAttended,
                        a.ClassStatus,
                        a.ClassCheckInTime,
                        a.IsMakeUp,
                        a.Note
                    })
                    .ToDictionaryAsync(a => a.EnrollmentId)
                : new();

            var historicalCodeRows = studentIds.Count > 0
                ? await _context.StudentCodeHistories
                    .AsNoTracking()
                    .Where(h => studentIds.Contains(h.StudentId))
                    .Select(h => new { h.StudentId, h.OriginalCode, h.ArchivedYearCode })
                    .ToListAsync()
                : new();

            var historicalMap = historicalCodeRows
                .GroupBy(h => h.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.OriginalCode?.Trim().ToUpperInvariant() ?? string.Empty)
                          .Concat(g.Select(x => x.ArchivedYearCode?.Trim().ToUpperInvariant() ?? string.Empty))
                          .Where(c => !string.IsNullOrEmpty(c))
                          .Distinct()
                          .ToList());

            var studentRows = new List<StudentAttendanceRowItem>(enrollments.Count);
            var qrEntries = new List<(string Key, RedisQrStudentLookupItem Value)>(enrollments.Count + historicalCodeRows.Count);

            foreach (var e in enrollments)
            {
                attendances.TryGetValue(e.Id, out var att);
                var fullName = $"{e.FirstName} {e.LastName}".Trim();
                var officialCode = e.StudentCode.Trim().ToUpperInvariant();

                studentRows.Add(new StudentAttendanceRowItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.StudentCode,
                    ChristianName = e.ChristianName,
                    FullName = fullName,
                    Gender = e.Gender ?? "Nam",
                    AttendedMass = att?.AttendedMass ?? false,
                    MassStatus = att?.MassStatus ?? AttendanceStatus.AbsentUnpermitted,
                    MassCheckInTime = att?.MassCheckInTime,
                    ClassAttended = att?.ClassAttended ?? false,
                    ClassStatus = att?.ClassStatus ?? AttendanceStatus.AbsentUnpermitted,
                    ClassCheckInTime = att?.ClassCheckInTime,
                    IsMakeUp = att?.IsMakeUp ?? false,
                    Note = att?.Note
                });

                var lookupItem = new RedisQrStudentLookupItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = officialCode,
                    ChristianName = e.ChristianName,
                    FullName = fullName,
                    Gender = e.Gender ?? "Nam",
                    ClassId = targetClass.Id,
                    ClassName = targetClass.Name,
                    GradeLevel = targetClass.GradeLevel
                };

                qrEntries.Add((RedisAttendanceKeys.QrStudentLookup(dateUtc, officialCode), lookupItem));

                if (historicalMap.TryGetValue(e.StudentId, out var oldCodes))
                {
                    foreach (var oldCode in oldCodes)
                    {
                        if (!string.Equals(oldCode, officialCode, StringComparison.OrdinalIgnoreCase))
                        {
                            qrEntries.Add((RedisAttendanceKeys.QrStudentLookup(dateUtc, oldCode), lookupItem));
                        }
                    }
                }
            }

            var payload = new RedisClassAttendancePayload
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AttendanceDate = dateUtc,
                CachedAtUtc = DateTime.UtcNow,
                IsDirty = false,
                Revision = 0,
                Students = studentRows
            };

            await _cache.SetAsync(sheetKey, payload, DefaultCacheTtl);
            await SetManyAsync(qrEntries, DefaultCacheTtl);

            return payload;
        }

        private static ClassAttendanceSheetViewModel ToSheetViewModel(RedisClassAttendancePayload payload) =>
            new()
            {
                ClassId = payload.ClassId,
                ClassName = payload.ClassName,
                GradeLevel = payload.GradeLevel,
                AttendanceDate = payload.AttendanceDate,
                Students = payload.Students
            };

        private static StudentAttendanceRowItem CloneRow(StudentAttendanceRowItem row) =>
            new()
            {
                EnrollmentId = row.EnrollmentId,
                StudentId = row.StudentId,
                StudentCode = row.StudentCode,
                ChristianName = row.ChristianName,
                FullName = row.FullName,
                Gender = row.Gender,
                AttendedMass = row.AttendedMass,
                MassStatus = row.MassStatus,
                MassCheckInTime = row.MassCheckInTime,
                ClassAttended = row.ClassAttended,
                ClassStatus = row.ClassStatus,
                ClassCheckInTime = row.ClassCheckInTime,
                IsMakeUp = row.IsMakeUp,
                Note = row.Note
            };

        private static bool HasMeaningfulAttendanceState(StudentAttendanceRowItem item) =>
            item.AttendedMass ||
            !string.Equals(item.MassStatus, AttendanceStatus.AbsentUnpermitted, StringComparison.OrdinalIgnoreCase) ||
            item.ClassAttended ||
            !string.Equals(item.ClassStatus, AttendanceStatus.AbsentUnpermitted, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(item.Note);

        private static DateTime[] ResolveSyncDatesUtc(DateTime? targetDate)
        {
            if (targetDate.HasValue)
            {
                return new[] { DateTime.SpecifyKind(targetDate.Value.Date, DateTimeKind.Utc) };
            }

            var vnToday = DateTime.SpecifyKind(GetVietnamNow().Date, DateTimeKind.Utc);
            var dates = new HashSet<DateTime>
            {
                vnToday,
                vnToday.AddDays(-1),
                DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc)
            };

            return dates.ToArray();
        }

        private async Task SetManyAsync<T>(IReadOnlyList<(string Key, T Value)> entries, TimeSpan ttl)
        {
            if (entries.Count == 0) return;

            for (int offset = 0; offset < entries.Count; offset += RedisWriteBatchSize)
            {
                int batchLen = Math.Min(RedisWriteBatchSize, entries.Count - offset);
                var tasks = new Task[batchLen];
                for (int i = 0; i < batchLen; i++)
                {
                    var entry = entries[offset + i];
                    tasks[i] = _cache.SetAsync(entry.Key, entry.Value, ttl);
                }
                await Task.WhenAll(tasks);
            }
        }

        private async Task RemoveManyKeysAsync(IReadOnlyList<string> keys)
        {
            if (keys.Count == 0) return;

            for (int offset = 0; offset < keys.Count; offset += RedisWriteBatchSize)
            {
                int batchLen = Math.Min(RedisWriteBatchSize, keys.Count - offset);
                var tasks = new Task[batchLen];
                for (int i = 0; i < batchLen; i++)
                {
                    tasks[i] = _cache.RemoveAsync(keys[offset + i]);
                }
                await Task.WhenAll(tasks);
            }
        }

        private async Task MarkClassDirtyAsync(int classId, DateTime dateUtc)
        {
            var dirtyKey = RedisAttendanceKeys.DirtyPartitionsIndex(dateUtc);
            var dirtyLock = GetDirtyIndexLock(dateUtc);

            await dirtyLock.WaitAsync();
            try
            {
                var dirtySet = await _cache.GetAsync<HashSet<int>>(dirtyKey);
                if (dirtySet != null && dirtySet.Contains(classId))
                {
                    // Lớp đã có mặt trong tập dirty của ngày -> bỏ qua lượt Redis SET dư thừa
                    return;
                }

                dirtySet ??= new HashSet<int>();
                dirtySet.Add(classId);
                await _cache.SetAsync(dirtyKey, dirtySet, DefaultCacheTtl);
            }
            finally
            {
                dirtyLock.Release();
            }
        }

        private async Task RemoveCleanClassesFromDirtyIndexAsync(DateTime dateUtc, IEnumerable<int> cleanedClassIds)
        {
            var dirtyKey = RedisAttendanceKeys.DirtyPartitionsIndex(dateUtc);
            var dirtyLock = GetDirtyIndexLock(dateUtc);

            await dirtyLock.WaitAsync();
            try
            {
                var currentDirtySet = await _cache.GetAsync<HashSet<int>>(dirtyKey);
                if (currentDirtySet == null || currentDirtySet.Count == 0)
                {
                    return;
                }

                bool removedAny = false;
                foreach (var id in cleanedClassIds)
                {
                    if (currentDirtySet.Remove(id))
                    {
                        removedAny = true;
                    }
                }

                if (!removedAny) return;

                if (currentDirtySet.Count == 0)
                {
                    await _cache.RemoveAsync(dirtyKey);
                }
                else
                {
                    await _cache.SetAsync(dirtyKey, currentDirtySet, DefaultCacheTtl);
                }
            }
            finally
            {
                dirtyLock.Release();
            }
        }

        /// <summary>
        /// Fallback an toàn khi Redis sự cố: ghi thẳng 1 học sinh duy nhất xuống PostgreSQL (không đụng 44 học sinh còn lại của lớp).
        /// </summary>
        private async Task PersistSingleQrScanFallbackAsync(
            int classId,
            DateTime targetDateUtc,
            StudentAttendanceRowItem? rowSnapshot,
            bool persistAttendance,
            long? activeCampaignId,
            long studentId,
            long enrollmentId,
            string modifiedBy,
            string? clientIp)
        {
            var rows = persistAttendance && rowSnapshot != null
                ? new List<StudentAttendanceRowItem> { rowSnapshot }
                : new List<StudentAttendanceRowItem>();

            var auditMeta = new Dictionary<long, RedisDirtyAuditMeta>
            {
                [enrollmentId] = new RedisDirtyAuditMeta
                {
                    ActionType = "QR_SCAN",
                    ModifiedBy = modifiedBy,
                    IpAddress = clientIp
                }
            };

            var pendingCampaigns = activeCampaignId.HasValue
                ? new List<RedisPendingCampaignCheckIn>
                {
                    new()
                    {
                        CampaignId = activeCampaignId.Value,
                        StudentId = studentId,
                        EnrollmentId = enrollmentId,
                        AttendanceDate = targetDateUtc,
                        ModifiedBy = modifiedBy,
                        CheckInTimeUtc = DateTime.UtcNow
                    }
                }
                : new List<RedisPendingCampaignCheckIn>();

            var flushLock = GetClassFlushLock(classId, targetDateUtc);
            await flushLock.WaitAsync();
            try
            {
                await FlushSnapshotToDbAsync(
                    rows,
                    auditMeta,
                    pendingCampaigns,
                    targetDateUtc,
                    fallbackActionType: "QR_SCAN",
                    fallbackModifiedBy: modifiedBy,
                    fallbackModifiedIp: clientIp);

                // Cập nhật cờ sạch cho riêng học sinh này trên bộ đệm fallback
                var sheetKey = RedisAttendanceKeys.ClassSheet(classId, targetDateUtc);
                var sheetLock = GetClassSheetLock(classId, targetDateUtc);
                await sheetLock.WaitAsync();
                try
                {
                    var latestPayload = await _cache.GetAsync<RedisClassAttendancePayload>(sheetKey);
                    if (latestPayload != null)
                    {
                        latestPayload.DirtyEnrollmentIds?.Remove(enrollmentId);
                        latestPayload.DirtyAuditMetaByEnrollment?.Remove(enrollmentId);
                        if (activeCampaignId.HasValue)
                        {
                            latestPayload.PendingCampaignCheckIns?.RemoveAll(c =>
                                c.CampaignId == activeCampaignId.Value &&
                                c.EnrollmentId == enrollmentId &&
                                c.AttendanceDate == targetDateUtc);
                        }

                        if ((latestPayload.DirtyEnrollmentIds == null || latestPayload.DirtyEnrollmentIds.Count == 0) &&
                            (latestPayload.PendingCampaignCheckIns == null || latestPayload.PendingCampaignCheckIns.Count == 0))
                        {
                            latestPayload.IsDirty = false;
                        }

                        await _cache.SetAsync(sheetKey, latestPayload, DefaultCacheTtl);
                    }
                }
                finally
                {
                    sheetLock.Release();
                }

                await _cache.RemoveAsync(RedisAttendanceKeys.ClassSessionInfo(classId, targetDateUtc));
                await _cache.RemoveAsync($"staff_lookup:student_360:{studentId}");
            }
            finally
            {
                flushLock.Release();
            }
        }

        /// <summary>
        /// Snapshot-and-Flush không chặn luồng quét QR:
        /// - Chỉ giữ ClassSheetLock trong vài millisecond để sao chép các dòng dirty.
        /// - Toàn bộ Transaction PostgreSQL chạy NGOÀI ClassSheetLock và được tuần tự hóa theo ClassFlushLock.
        /// - Sau khi commit DB, kiểm tra Revision để không xóa cờ IsDirty nếu có lượt quét mới vừa đến trong lúc đang ghi DB.
        /// </summary>
        private async Task<(int Affected, bool StillDirty)> FlushSingleClassByIdAsync(int classId, DateTime targetDateUtc)
        {
            var sheetKey = RedisAttendanceKeys.ClassSheet(classId, targetDateUtc);
            var flushLock = GetClassFlushLock(classId, targetDateUtc);

            await flushLock.WaitAsync();
            try
            {
                long flushedRevision;
                List<StudentAttendanceRowItem> rowsToFlush;
                Dictionary<long, RedisDirtyAuditMeta> auditMetaSnapshot;
                List<RedisPendingCampaignCheckIn> pendingCampaignSnapshot;
                string fallbackActionType;
                string fallbackModifiedBy;
                string? fallbackModifiedIp;

                var sheetLock = GetClassSheetLock(classId, targetDateUtc);
                await sheetLock.WaitAsync();
                try
                {
                    var payload = await _cache.GetAsync<RedisClassAttendancePayload>(sheetKey);
                    if (payload == null || !payload.IsDirty || payload.Students.Count == 0)
                    {
                        return (0, false);
                    }

                    flushedRevision = payload.Revision;
                    fallbackActionType = SafeTruncate(payload.LastActionType, 50) ?? "BATCH_SAVE";
                    fallbackModifiedBy = SafeTruncate(payload.LastModifiedBy, 100) ?? "SYSTEM";
                    fallbackModifiedIp = SafeTruncate(payload.LastModifiedIp, 45);

                    if (payload.DirtyEnrollmentIds != null && payload.DirtyEnrollmentIds.Count > 0)
                    {
                        rowsToFlush = payload.Students
                            .Where(s => payload.DirtyEnrollmentIds.Contains(s.EnrollmentId))
                            .Select(CloneRow)
                            .ToList();
                    }
                    else if (string.Equals(fallbackActionType, "BATCH_SAVE", StringComparison.OrdinalIgnoreCase))
                    {
                        rowsToFlush = payload.Students.Select(CloneRow).ToList();
                    }
                    else
                    {
                        // Tương thích ngược với payload Redis cũ trước khi nâng cấp DirtyEnrollmentIds
                        rowsToFlush = payload.Students
                            .Where(HasMeaningfulAttendanceState)
                            .Select(CloneRow)
                            .ToList();
                    }

                    auditMetaSnapshot = payload.DirtyAuditMetaByEnrollment != null
                        ? new Dictionary<long, RedisDirtyAuditMeta>(payload.DirtyAuditMetaByEnrollment)
                        : new Dictionary<long, RedisDirtyAuditMeta>();

                    pendingCampaignSnapshot = payload.PendingCampaignCheckIns != null && payload.PendingCampaignCheckIns.Count > 0
                        ? payload.PendingCampaignCheckIns.Select(c => new RedisPendingCampaignCheckIn
                        {
                            CampaignId = c.CampaignId,
                            StudentId = c.StudentId,
                            EnrollmentId = c.EnrollmentId,
                            AttendanceDate = c.AttendanceDate,
                            ModifiedBy = c.ModifiedBy,
                            CheckInTimeUtc = c.CheckInTimeUtc
                        }).ToList()
                        : new List<RedisPendingCampaignCheckIn>();
                }
                finally
                {
                    sheetLock.Release();
                }

                int affected = 0;
                if (rowsToFlush.Count > 0 || pendingCampaignSnapshot.Count > 0)
                {
                    affected = await FlushSnapshotToDbAsync(
                        rowsToFlush,
                        auditMetaSnapshot,
                        pendingCampaignSnapshot,
                        targetDateUtc,
                        fallbackActionType,
                        fallbackModifiedBy,
                        fallbackModifiedIp);
                }

                bool stillDirty = false;
                await sheetLock.WaitAsync();
                try
                {
                    var latestPayload = await _cache.GetAsync<RedisClassAttendancePayload>(sheetKey);
                    if (latestPayload != null)
                    {
                        if (latestPayload.Revision == flushedRevision)
                        {
                            latestPayload.IsDirty = false;
                            latestPayload.DirtyEnrollmentIds = null;
                            latestPayload.DirtyAuditMetaByEnrollment = null;
                            latestPayload.PendingCampaignCheckIns = null;
                            await _cache.SetAsync(sheetKey, latestPayload, DefaultCacheTtl);
                            stillDirty = false;
                        }
                        else
                        {
                            // Có lượt quét QR mới đến trong lúc đang ghi DB -> giữ IsDirty = true để chu kỳ kế tiếp đồng bộ tiếp
                            if (pendingCampaignSnapshot.Count > 0 && latestPayload.PendingCampaignCheckIns != null)
                            {
                                var flushedCampKeys = pendingCampaignSnapshot
                                    .Select(c => (c.CampaignId, c.EnrollmentId, c.AttendanceDate))
                                    .ToHashSet();

                                latestPayload.PendingCampaignCheckIns.RemoveAll(c =>
                                    flushedCampKeys.Contains((c.CampaignId, c.EnrollmentId, c.AttendanceDate)));
                                await _cache.SetAsync(sheetKey, latestPayload, DefaultCacheTtl);
                            }

                            stillDirty = true;
                        }
                    }
                }
                finally
                {
                    sheetLock.Release();
                }

                if (stillDirty && _cache.IsRedisAvailable)
                {
                    _flushSignal.NotifyDirty(targetDateUtc, classId);
                }

                // Xóa cache phiên học và hồ sơ 360 của đúng các em vừa đồng bộ
                var keysToInvalidate = new List<string>(rowsToFlush.Count + 1)
                {
                    RedisAttendanceKeys.ClassSessionInfo(classId, targetDateUtc)
                };
                foreach (var studentId in rowsToFlush.Select(r => r.StudentId).Distinct())
                {
                    keysToInvalidate.Add($"staff_lookup:student_360:{studentId}");
                }
                await RemoveManyKeysAsync(keysToInvalidate);

                return (affected, stillDirty);
            }
            finally
            {
                flushLock.Release();
            }
        }

        private async Task<int> FlushSnapshotToDbAsync(
            List<StudentAttendanceRowItem> rowsToFlush,
            Dictionary<long, RedisDirtyAuditMeta> auditMetaSnapshot,
            List<RedisPendingCampaignCheckIn> pendingCampaigns,
            DateTime targetDateUtc,
            string fallbackActionType,
            string fallbackModifiedBy,
            string? fallbackModifiedIp)
        {
            var enrollmentIds = rowsToFlush.Select(s => s.EnrollmentId).Distinct().ToList();
            var strategy = _context.Database.CreateExecutionStrategy();
            int affected = 0;

            await strategy.ExecuteAsync(async () =>
            {
                affected = 0;
                await using var transaction = await _context.Database.BeginTransactionAsync();

                if (enrollmentIds.Count > 0)
                {
                    var existingRecords = await _context.Attendances
                        .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.AttendanceDate == targetDateUtc)
                        .ToDictionaryAsync(a => a.EnrollmentId);

                    var newRecords = new List<Attendance>();
                    var pendingAudits = new List<(
                        Attendance Record,
                        bool IsNew,
                        string ActionType,
                        string ModifiedBy,
                        string? IpAddress,
                        bool OldMassAtt,
                        string OldMassSt,
                        bool OldClassAtt,
                        string OldClassSt,
                        string? OldNote)>();

                    bool hasAnyUpdate = false;

                    foreach (var item in rowsToFlush)
                    {
                        auditMetaSnapshot.TryGetValue(item.EnrollmentId, out var rowAuditMeta);
                        var actionType = SafeTruncate(rowAuditMeta?.ActionType, 50) ?? fallbackActionType;
                        var modifiedBy = SafeTruncate(rowAuditMeta?.ModifiedBy, 100) ?? fallbackModifiedBy;
                        var clientIp = SafeTruncate(rowAuditMeta?.IpAddress, 45) ?? fallbackModifiedIp;

                        if (existingRecords.TryGetValue(item.EnrollmentId, out var existing))
                        {
                            bool oldMassAtt = existing.AttendedMass;
                            string oldMassSt = existing.MassStatus;
                            bool oldClassAtt = existing.ClassAttended;
                            string oldClassSt = existing.ClassStatus;
                            string? oldNote = existing.Note;

                            bool changed = HasAttendanceStateChanged(
                                oldMassAtt, oldMassSt, item.AttendedMass, item.MassStatus,
                                oldClassAtt, oldClassSt, item.ClassAttended, item.ClassStatus,
                                oldNote, item.Note);

                            bool massTimeChanged = item.MassCheckInTime.HasValue
                                ? existing.MassCheckInTime != item.MassCheckInTime
                                : (!item.AttendedMass && existing.MassCheckInTime.HasValue);

                            bool classTimeChanged = item.ClassCheckInTime.HasValue
                                ? existing.ClassCheckInTime != item.ClassCheckInTime
                                : (!item.ClassAttended && existing.ClassCheckInTime.HasValue);

                            if (changed || massTimeChanged || classTimeChanged)
                            {
                                existing.AttendedMass = item.AttendedMass;
                                existing.MassStatus = item.MassStatus;
                                if (item.MassCheckInTime.HasValue)
                                {
                                    existing.MassCheckInTime = item.MassCheckInTime;
                                }
                                else if (!item.AttendedMass)
                                {
                                    existing.MassCheckInTime = null;
                                }

                                existing.ClassAttended = item.ClassAttended;
                                existing.ClassStatus = item.ClassStatus;
                                if (item.ClassCheckInTime.HasValue)
                                {
                                    existing.ClassCheckInTime = item.ClassCheckInTime;
                                }
                                else if (!item.ClassAttended)
                                {
                                    existing.ClassCheckInTime = null;
                                }

                                existing.Note = item.Note;
                                hasAnyUpdate = true;
                                affected++;
                            }

                            if (changed)
                            {
                                pendingAudits.Add((existing, false, actionType, modifiedBy, clientIp, oldMassAtt, oldMassSt, oldClassAtt, oldClassSt, oldNote));
                            }
                        }
                        else
                        {
                            bool meaningfulNew = HasMeaningfulAttendanceState(item);
                            bool isBatchSave = string.Equals(actionType, "BATCH_SAVE", StringComparison.OrdinalIgnoreCase);

                            // Chỉ tạo dòng mới khi có trạng thái thực (quét QR / ghi chú) hoặc thao tác Lưu sổ lớp thủ công
                            // Tránh chèn 44 dòng ABSENT_UNPERMITTED thừa khi chỉ quét QR cho 1 học sinh
                            if (meaningfulNew || isBatchSave)
                            {
                                var newRecord = new Attendance
                                {
                                    EnrollmentId = item.EnrollmentId,
                                    AttendanceDate = targetDateUtc,
                                    DayOfWeek = targetDateUtc.DayOfWeek,
                                    AttendedMass = item.AttendedMass,
                                    MassStatus = item.MassStatus,
                                    MassCheckInTime = item.MassCheckInTime,
                                    ClassAttended = item.ClassAttended,
                                    ClassStatus = item.ClassStatus,
                                    ClassCheckInTime = item.ClassCheckInTime,
                                    Note = item.Note,
                                    CreatedAt = DateTime.UtcNow
                                };
                                newRecords.Add(newRecord);
                                affected++;

                                if (meaningfulNew)
                                {
                                    pendingAudits.Add((newRecord, true, actionType, modifiedBy, clientIp, false, AttendanceStatus.AbsentUnpermitted, false, AttendanceStatus.AbsentUnpermitted, null));
                                }
                            }
                        }
                    }

                    if (newRecords.Count > 0)
                    {
                        await _context.Attendances.AddRangeAsync(newRecords);
                    }

                    if (newRecords.Count > 0 || hasAnyUpdate)
                    {
                        await _context.SaveChangesAsync();
                    }

                    if (pendingAudits.Count > 0)
                    {
                        var auditLogs = pendingAudits.Select(p =>
                        {
                            var prefix = p.ActionType switch
                            {
                                "QR_SCAN" => "Quét mã QR",
                                "QR_UNDO" => "Hoàn tác quét QR",
                                _ => "Lưu sổ điểm danh"
                            };

                            return new AttendanceAuditLog
                            {
                                AttendanceId = p.Record.Id,
                                ActionType = p.ActionType,
                                OldValues = FormatStateSnapshot(p.OldMassAtt, p.OldMassSt, p.OldClassAtt, p.OldClassSt, p.OldNote, isNewRecord: p.IsNew),
                                NewValues = FormatStateSnapshot(p.Record.AttendedMass, p.Record.MassStatus, p.Record.ClassAttended, p.Record.ClassStatus, p.Record.Note),
                                ModifiedBy = p.ModifiedBy,
                                Reason = BuildAuditReasonSummary(
                                    prefix, p.IsNew,
                                    p.OldMassAtt, p.OldMassSt, p.Record.AttendedMass, p.Record.MassStatus,
                                    p.OldClassAtt, p.OldClassSt, p.Record.ClassAttended, p.Record.ClassStatus,
                                    p.OldNote, p.Record.Note),
                                IpAddress = p.IpAddress,
                                CreatedAt = DateTime.UtcNow
                            };
                        }).ToList();

                        await _context.AttendanceAuditLogs.AddRangeAsync(auditLogs);
                        await _context.SaveChangesAsync();
                    }
                }

                // Đồng bộ theo lô các lượt quét Chiến dịch đi lễ 1 tháng (nếu có) trong cùng 1 transaction
                if (pendingCampaigns.Count > 0)
                {
                    var distinctCampaignItems = pendingCampaigns
                        .GroupBy(c => (c.CampaignId, c.EnrollmentId, Date: DateTime.SpecifyKind(c.AttendanceDate.Date, DateTimeKind.Utc)))
                        .Select(g => g.OrderBy(x => x.CheckInTimeUtc).First())
                        .ToList();

                    var campaignIds = distinctCampaignItems.Select(c => c.CampaignId).Distinct().ToList();
                    var campEnrollmentIds = distinctCampaignItems.Select(c => c.EnrollmentId).Distinct().ToList();

                    var existingCampRecords = await _context.MassCampaignRecords
                        .Where(r => campaignIds.Contains(r.CampaignId) &&
                                    campEnrollmentIds.Contains(r.EnrollmentId) &&
                                    r.AttendanceDate == targetDateUtc)
                        .ToDictionaryAsync(r => (r.CampaignId, r.EnrollmentId));

                    var newCampRecords = new List<MassCampaignRecord>();
                    bool campUpdated = false;

                    foreach (var campItem in distinctCampaignItems)
                    {
                        var campModifiedBy = SafeTruncate(campItem.ModifiedBy, 100) ?? "Camera QR HUD";
                        if (existingCampRecords.TryGetValue((campItem.CampaignId, campItem.EnrollmentId), out var existingCamp))
                        {
                            existingCamp.Status = "PRESENT";
                            existingCamp.CheckInMethod = "QR_SCAN";
                            existingCamp.CheckInTime = campItem.CheckInTimeUtc;
                            existingCamp.LastModifiedBy = campModifiedBy;
                            existingCamp.LastModifiedAt = DateTime.UtcNow;
                            campUpdated = true;
                        }
                        else
                        {
                            newCampRecords.Add(new MassCampaignRecord
                            {
                                CampaignId = campItem.CampaignId,
                                StudentId = campItem.StudentId,
                                EnrollmentId = campItem.EnrollmentId,
                                AttendanceDate = targetDateUtc,
                                Status = "PRESENT",
                                CheckInMethod = "QR_SCAN",
                                CheckInTime = campItem.CheckInTimeUtc,
                                LastModifiedBy = campModifiedBy,
                                LastModifiedAt = DateTime.UtcNow
                            });
                        }
                        affected++;
                    }

                    if (newCampRecords.Count > 0)
                    {
                        await _context.MassCampaignRecords.AddRangeAsync(newCampRecords);
                    }

                    if (newCampRecords.Count > 0 || campUpdated)
                    {
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();
            });

            return affected;
        }

        internal static string? SafeTruncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        internal static string FormatStatusVietnamese(string? status) =>
            (status ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "LINED_UP" => "Xếp hàng",
                "PRESENT" => "Có mặt",
                "LATE" => "Đi trễ",
                "ABSENT_PERMITTED" or "EXCUSED" => "Vắng có phép",
                "ABSENT_UNPERMITTED" or "ABSENT" => "Vắng không phép",
                "NONE" or "" => "Chưa ghi nhận",
                var other => other
            };

        internal static string FormatStateSnapshot(
            bool attendedMass,
            string? massStatus,
            bool classAttended,
            string? classStatus,
            string? note,
            bool isNewRecord = false)
        {
            if (isNewRecord)
            {
                return "Chưa có bản ghi điểm danh trước đó";
            }

            var massText = $"{FormatStatusVietnamese(massStatus)} ({(attendedMass ? "Có" : "Vắng")})";
            var classText = $"{FormatStatusVietnamese(classStatus)} ({(classAttended ? "Có" : "Vắng")})";
            var noteText = string.IsNullOrWhiteSpace(note) ? "(trống)" : note.Trim();
            return $"Lễ: {massText} | Học: {classText} | Ghi chú: {noteText}";
        }

        internal static bool HasAttendanceStateChanged(
            bool oldMassAtt,
            string? oldMassSt,
            bool newMassAtt,
            string? newMassSt,
            bool oldClassAtt,
            string? oldClassSt,
            bool newClassAtt,
            string? newClassSt,
            string? oldNote,
            string? newNote)
        {
            var oldMassNorm = (oldMassSt ?? AttendanceStatus.AbsentUnpermitted).Trim().ToUpperInvariant();
            var newMassNorm = (newMassSt ?? AttendanceStatus.AbsentUnpermitted).Trim().ToUpperInvariant();
            var oldClassNorm = (oldClassSt ?? AttendanceStatus.AbsentUnpermitted).Trim().ToUpperInvariant();
            var newClassNorm = (newClassSt ?? AttendanceStatus.AbsentUnpermitted).Trim().ToUpperInvariant();
            var oldNoteNorm = string.IsNullOrWhiteSpace(oldNote) ? null : oldNote.Trim();
            var newNoteNorm = string.IsNullOrWhiteSpace(newNote) ? null : newNote.Trim();

            return oldMassAtt != newMassAtt ||
                   oldMassNorm != newMassNorm ||
                   oldClassAtt != newClassAtt ||
                   oldClassNorm != newClassNorm ||
                   !string.Equals(oldNoteNorm, newNoteNorm, StringComparison.Ordinal);
        }

        internal static string BuildAuditReasonSummary(
            string prefix,
            bool isNew,
            bool oldMassAtt,
            string? oldMassSt,
            bool newMassAtt,
            string? newMassSt,
            bool oldClassAtt,
            string? oldClassSt,
            bool newClassAtt,
            string? newClassSt,
            string? oldNote,
            string? newNote)
        {
            var changes = new List<string>();
            var oldMassNorm = (oldMassSt ?? AttendanceStatus.AbsentUnpermitted).Trim().ToUpperInvariant();
            var newMassNorm = (newMassSt ?? AttendanceStatus.AbsentUnpermitted).Trim().ToUpperInvariant();
            var oldClassNorm = (oldClassSt ?? AttendanceStatus.AbsentUnpermitted).Trim().ToUpperInvariant();
            var newClassNorm = (newClassSt ?? AttendanceStatus.AbsentUnpermitted).Trim().ToUpperInvariant();
            var oldNoteNorm = string.IsNullOrWhiteSpace(oldNote) ? null : oldNote.Trim();
            var newNoteNorm = string.IsNullOrWhiteSpace(newNote) ? null : newNote.Trim();

            if (isNew)
            {
                if (newClassAtt || newClassNorm != AttendanceStatus.AbsentUnpermitted)
                {
                    changes.Add($"Đi học ({FormatStatusVietnamese(newClassNorm)})");
                }
                if (newMassAtt || newMassNorm != AttendanceStatus.AbsentUnpermitted)
                {
                    changes.Add($"Đi lễ ({FormatStatusVietnamese(newMassNorm)})");
                }
                if (newNoteNorm != null)
                {
                    changes.Add($"Ghi chú: \"{newNoteNorm}\"");
                }
                if (changes.Count == 0)
                {
                    changes.Add("Khởi tạo trạng thái vắng");
                }
            }
            else
            {
                if (oldClassAtt != newClassAtt || oldClassNorm != newClassNorm)
                {
                    changes.Add($"Đi học ({FormatStatusVietnamese(oldClassNorm)} → {FormatStatusVietnamese(newClassNorm)})");
                }
                if (oldMassAtt != newMassAtt || oldMassNorm != newMassNorm)
                {
                    changes.Add($"Đi lễ ({FormatStatusVietnamese(oldMassNorm)} → {FormatStatusVietnamese(newMassNorm)})");
                }
                if (!string.Equals(oldNoteNorm, newNoteNorm, StringComparison.Ordinal))
                {
                    changes.Add($"Ghi chú: \"{oldNoteNorm ?? "(trống)"}\" → \"{newNoteNorm ?? "(trống)"}\"");
                }
            }

            var summary = changes.Count > 0 ? $"{prefix}: {string.Join("; ", changes)}" : prefix;
            return SafeTruncate(summary, 255) ?? prefix;
        }

        private static string NormalizeAttendanceStatus(bool isAttended, string? status, bool allowLinedUp)
        {
            var normalized = (status ?? string.Empty).Trim().ToUpperInvariant();

            if (isAttended)
            {
                if (normalized == AttendanceStatus.Present ||
                    normalized == AttendanceStatus.Late ||
                    (allowLinedUp && normalized == AttendanceStatus.LinedUp))
                {
                    return normalized;
                }

                return AttendanceStatus.Present;
            }

            if (normalized == AttendanceStatus.AbsentPermitted ||
                normalized == AttendanceStatus.AbsentUnpermitted)
            {
                return normalized;
            }

            return AttendanceStatus.AbsentUnpermitted;
        }

        private static ClassSessionContextDto BuildSessionContextObject(
            ClassRoom cls,
            DateTime dateUtc,
            ClassSchedule? schedule,
            int pendingAbsenceCount,
            string? pinnedAnnouncementTitle,
            List<StudentAttendanceRowItem> students,
            string? lessonDocDescription = null)
        {
            bool isMorning = !GradeLevel.IsAfternoonSession(cls.Session, cls.GradeLevel);
            string roomName = schedule?.ClassRoomLocation?.RoomName
                              ?? (string.IsNullOrWhiteSpace(cls.RoomName) ? "Chưa bố trí phòng" : cls.RoomName);
            string timeSlot = schedule != null
                ? $"{schedule.StartTime:hh\\:mm} - {schedule.EndTime:hh\\:mm}"
                : (isMorning ? "09:00 - 10:30" : "15:00 - 16:30");

            bool hasAnyMassMarked = students.Any(s => s.AttendedMass || s.MassStatus != AttendanceStatus.AbsentUnpermitted);
            bool hasAnyClassMarked = students.Any(s => s.ClassAttended || s.ClassStatus != AttendanceStatus.AbsentUnpermitted);

            var tasks = new List<InClassTaskChecklistItem>
            {
                new InClassTaskChecklistItem
                {
                    TaskCode = "TASK_MASS_ATT",
                    Title = dateUtc.DayOfWeek == DayOfWeek.Thursday
                        ? "Điểm danh Thánh Lễ Thứ Năm (17h45)"
                        : "Điểm danh Thánh Lễ Thiếu Nhi (06h30)",
                    BadgeText = hasAnyMassMarked ? "Đã ghi nhận" : "Cần thực hiện",
                    BadgeColor = hasAnyMassMarked ? "bg-success" : "bg-warning text-dark",
                    IsCompleted = hasAnyMassMarked
                }
            };

            if (dateUtc.DayOfWeek == DayOfWeek.Sunday)
            {
                tasks.Add(new InClassTaskChecklistItem
                {
                    TaskCode = "TASK_CLASS_ATT",
                    Title = $"Điểm danh Giờ học Giáo lý tại phòng {roomName} ({timeSlot})",
                    BadgeText = hasAnyClassMarked ? "Đã điểm danh" : "Đang chờ",
                    BadgeColor = hasAnyClassMarked ? "bg-success" : "bg-primary",
                    IsCompleted = hasAnyClassMarked
                });
            }

            if (pendingAbsenceCount > 0)
            {
                tasks.Add(new InClassTaskChecklistItem
                {
                    TaskCode = "TASK_ABSENCE_REVIEW",
                    Title = $"Duyệt {pendingAbsenceCount} đơn xin phép nghỉ học của Phụ huynh",
                    BadgeText = $"{pendingAbsenceCount} đơn chờ",
                    BadgeColor = "bg-danger",
                    ActionUrl = $"/Absence/Manage?classId={cls.Id}",
                    IsCompleted = false
                });
            }

            tasks.Add(new InClassTaskChecklistItem
            {
                TaskCode = "TASK_LESSON_PLAN",
                Title = !string.IsNullOrWhiteSpace(lessonDocDescription)
                    ? $"Triển khai bài dạy: {lessonDocDescription}"
                    : "Kiểm tra bài cũ & giảng bài Giáo lý theo phân phối chương trình",
                BadgeText = "Giáo án",
                BadgeColor = "bg-info text-dark",
                ActionUrl = $"/LessonPlans/Index?classRoomId={cls.Id}",
                IsCompleted = !string.IsNullOrWhiteSpace(lessonDocDescription)
            });

            return new ClassSessionContextDto
            {
                ClassId = cls.Id,
                ClassName = cls.Name,
                GradeLevel = cls.GradeLevel,
                SessionDate = dateUtc,
                RoomName = roomName,
                ShiftName = isMorning ? "Khối Sáng" : "Khối Chiều",
                TimeSlotDisplay = timeSlot,
                HasLessonPlan = !string.IsNullOrWhiteSpace(lessonDocDescription),
                TopicTitle = lessonDocDescription ?? $"Bài học Giáo lý {cls.GradeLevel} - Ngày {dateUtc:dd/MM/yyyy}",
                PendingAbsenceRequestsCount = pendingAbsenceCount,
                LatestPinnedAnnouncementTitle = pinnedAnnouncementTitle,
                IsServedFromRedis = true,
                Tasks = tasks
            };
        }
        #endregion
    }
}
