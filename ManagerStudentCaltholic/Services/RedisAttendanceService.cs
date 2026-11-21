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
        private readonly ILogger<RedisAttendanceService> _logger;
        private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(18);

        public RedisAttendanceService(
            ParishDbContext context,
            ICacheService cache,
            ILogger<RedisAttendanceService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// 1. TASK-P3-12.2: Hangfire Job nạp sẵn toàn bộ danh sách học sinh & thông tin giờ học lên Redis lúc 05:30 sáng CN
        /// </summary>
        public async Task<int> PreWarmDailyAttendanceAndTasksAsync(DateTime? targetDate = null)
        {
            var dateUtc = DateTime.SpecifyKind((targetDate ?? DateTime.Today).Date, DateTimeKind.Utc);
            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            if (currentYear == null)
            {
                _logger.LogWarning("[PreWarmJob] Không tìm thấy niên khóa hiện hành để nạp bộ đệm điểm danh.");
                return 0;
            }

            var classes = await _context.Classes
                .Where(c => c.AcademicYearId == currentYear.Id)
                .OrderBy(c => c.Id)
                .AsNoTracking()
                .ToListAsync();

            if (!classes.Any()) return 0;

            var classIds = classes.Select(c => c.Id).ToList();

            var allEnrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => classIds.Contains(e.ClassRoomId) && e.Student.IsActive)
                .OrderBy(e => e.Student.StudentCode)
                .ThenBy(e => e.Student.LastName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = allEnrollments.Select(e => e.Id).ToList();

            var existingAttendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.AttendanceDate == dateUtc)
                .AsNoTracking()
                .ToDictionaryAsync(a => a.EnrollmentId, a => a);

            var schedules = await _context.ClassSchedules
                .Include(s => s.ClassRoomLocation)
                .Where(s => classIds.Contains(s.ClassRoomId))
                .AsNoTracking()
                .ToListAsync();

            var pendingAbsencesByClass = await _context.AbsenceRequests
                .Include(r => r.Enrollment)
                .Where(r => r.Status == "PENDING" && classIds.Contains(r.Enrollment.ClassRoomId))
                .GroupBy(r => r.Enrollment.ClassRoomId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count);

            var pinnedAnnouncements = await _context.Announcements
                .Where(a => a.IsActive && a.IsPinned)
                .OrderByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            int totalStudentsCached = 0;

            foreach (var cls in classes)
            {
                var clsEnrollments = allEnrollments.Where(e => e.ClassRoomId == cls.Id).ToList();
                var studentRows = new List<StudentAttendanceRowItem>();

                foreach (var e in clsEnrollments)
                {
                    existingAttendances.TryGetValue(e.Id, out var att);
                    var fullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim();

                    studentRows.Add(new StudentAttendanceRowItem
                    {
                        EnrollmentId = e.Id,
                        StudentId = e.StudentId,
                        StudentCode = e.Student.StudentCode,
                        ChristianName = e.Student.ChristianName,
                        FullName = fullName,
                        Gender = e.Student.Gender ?? "Nam",
                        AttendedMass = att?.AttendedMass ?? false,
                        MassStatus = att?.MassStatus ?? AttendanceStatus.AbsentUnpermitted,
                        MassCheckInTime = att?.MassCheckInTime,
                        ClassAttended = att?.ClassAttended ?? false,
                        ClassStatus = att?.ClassStatus ?? AttendanceStatus.AbsentUnpermitted,
                        ClassCheckInTime = att?.ClassCheckInTime,
                        IsMakeUp = att?.IsMakeUp ?? false,
                        Note = att?.Note
                    });

                    // Đẩy khóa tra cứu nhanh O(1) cho từng Mã QR
                    var qrKey = RedisAttendanceKeys.QrStudentLookup(dateUtc, e.Student.StudentCode);
                    await _cache.SetAsync(qrKey, new RedisQrStudentLookupItem
                    {
                        EnrollmentId = e.Id,
                        StudentId = e.StudentId,
                        StudentCode = e.Student.StudentCode.ToUpperInvariant(),
                        ChristianName = e.Student.ChristianName,
                        FullName = fullName,
                        Gender = e.Student.Gender ?? "Nam",
                        ClassId = cls.Id,
                        ClassName = cls.Name,
                        GradeLevel = cls.GradeLevel
                    }, DefaultCacheTtl);

                    totalStudentsCached++;
                }

                // Đẩy Sổ điểm danh của lớp lên Redis
                var sheetKey = RedisAttendanceKeys.ClassSheet(cls.Id, dateUtc);
                await _cache.SetAsync(sheetKey, new RedisClassAttendancePayload
                {
                    ClassId = cls.Id,
                    ClassName = cls.Name,
                    GradeLevel = cls.GradeLevel,
                    AttendanceDate = dateUtc,
                    CachedAtUtc = DateTime.UtcNow,
                    IsDirty = false,
                    Students = studentRows
                }, DefaultCacheTtl);

                // Đẩy thông tin Giờ học & Task của lớp lên Redis
                var schedule = schedules.FirstOrDefault(s => s.ClassRoomId == cls.Id);
                pendingAbsencesByClass.TryGetValue(cls.Id, out int pendingAbsenceCount);
                var pinnedAnn = pinnedAnnouncements.FirstOrDefault(a =>
                    a.TargetClassRoomId == cls.Id ||
                    (a.Scope == "GRADE" && a.TargetGradeLevel == cls.GradeLevel) ||
                    a.Scope == "ALL");

                var sessionCtx = BuildSessionContextObject(cls, dateUtc, schedule, pendingAbsenceCount, pinnedAnn?.Title, studentRows);
                await _cache.SetAsync(RedisAttendanceKeys.ClassSessionInfo(cls.Id, dateUtc), sessionCtx, DefaultCacheTtl);
            }

            _logger.LogInformation(
                "[PreWarmJob] Đã nạp thành công {StudentCount} học sinh của {ClassCount} lớp lên Redis cho ngày {Date:dd/MM/yyyy}.",
                totalStudentsCached, classes.Count, dateUtc);

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
                return new ClassAttendanceSheetViewModel
                {
                    ClassId = cachedSheet.ClassId,
                    ClassName = cachedSheet.ClassName,
                    GradeLevel = cachedSheet.GradeLevel,
                    AttendanceDate = cachedSheet.AttendanceDate,
                    Students = cachedSheet.Students
                };
            }

            // Cache Miss -> Truy vấn từ PostgreSQL và đẩy lên Redis
            var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
            if (targetClass == null)
            {
                return new ClassAttendanceSheetViewModel { ClassId = classId, AttendanceDate = dateUtc };
            }

            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassRoomId == classId && e.Student.IsActive)
                .OrderBy(e => e.Student.StudentCode)
                .ThenBy(e => e.Student.LastName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.AttendanceDate == dateUtc)
                .AsNoTracking()
                .ToDictionaryAsync(a => a.EnrollmentId);

            var studentRows = new List<StudentAttendanceRowItem>();
            foreach (var e in enrollments)
            {
                attendances.TryGetValue(e.Id, out var att);
                var fullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim();

                studentRows.Add(new StudentAttendanceRowItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = fullName,
                    Gender = e.Student.Gender ?? "Nam",
                    AttendedMass = att?.AttendedMass ?? false,
                    MassStatus = att?.MassStatus ?? AttendanceStatus.AbsentUnpermitted,
                    MassCheckInTime = att?.MassCheckInTime,
                    ClassAttended = att?.ClassAttended ?? false,
                    ClassStatus = att?.ClassStatus ?? AttendanceStatus.AbsentUnpermitted,
                    ClassCheckInTime = att?.ClassCheckInTime,
                    IsMakeUp = att?.IsMakeUp ?? false,
                    Note = att?.Note
                });

                var qrKey = RedisAttendanceKeys.QrStudentLookup(dateUtc, e.Student.StudentCode);
                await _cache.SetAsync(qrKey, new RedisQrStudentLookupItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode.ToUpperInvariant(),
                    ChristianName = e.Student.ChristianName,
                    FullName = fullName,
                    Gender = e.Student.Gender ?? "Nam",
                    ClassId = targetClass.Id,
                    ClassName = targetClass.Name,
                    GradeLevel = targetClass.GradeLevel
                }, DefaultCacheTtl);
            }

            var payload = new RedisClassAttendancePayload
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AttendanceDate = dateUtc,
                CachedAtUtc = DateTime.UtcNow,
                IsDirty = false,
                Students = studentRows
            };

            await _cache.SetAsync(sheetKey, payload, DefaultCacheTtl);

            return new ClassAttendanceSheetViewModel
            {
                ClassId = payload.ClassId,
                ClassName = payload.ClassName,
                GradeLevel = payload.GradeLevel,
                AttendanceDate = payload.AttendanceDate,
                Students = payload.Students
            };
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

            var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
            if (targetClass == null)
            {
                return new ClassSessionContextDto { ClassId = classId, SessionDate = dateUtc };
            }

            var schedule = await _context.ClassSchedules
                .Include(s => s.ClassRoomLocation)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClassRoomId == classId);

            int pendingAbsenceCount = await _context.AbsenceRequests
                .Include(r => r.Enrollment)
                .CountAsync(r => r.Status == "PENDING" && r.Enrollment.ClassRoomId == classId);

            var pinnedAnn = await _context.Announcements
                .Where(a => a.IsActive && a.IsPinned &&
                            (a.TargetClassRoomId == classId ||
                             (a.Scope == "GRADE" && a.TargetGradeLevel == targetClass.GradeLevel) ||
                             a.Scope == "ALL"))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.Title)
                .FirstOrDefaultAsync();

            var latestLessonDoc = await _context.ClassLessonDocuments
                .Where(d => d.ClassRoomId == classId && d.AcademicYearId == targetClass.AcademicYearId)
                .OrderByDescending(d => d.UploadedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            var ctx = BuildSessionContextObject(
                targetClass,
                dateUtc,
                schedule,
                pendingAbsenceCount,
                pinnedAnn,
                cachedSheet?.Students ?? new List<StudentAttendanceRowItem>(),
                latestLessonDoc?.Description);

            ctx.HasUnsyncedChanges = cachedSheet?.IsDirty ?? false;
            await _cache.SetAsync(sessionKey, ctx, DefaultCacheTtl);
            return ctx;
        }

        /// <summary>
        /// 4. TASK-P3-12.3 / P2-LOG-16: Lưu Sổ điểm danh hàng loạt lên bộ đệm Redis (AttendanceController.SaveBatch đồng thời gọi Write-Through SyncDirtyAttendancesToDatabaseAsync ngay sau đó).
        /// </summary>
        public async Task<(bool Success, string Message)> SaveBatchToRedisAsync(AttendanceBatchSubmitDto request)
        {
            var dateUtc = DateTime.SpecifyKind(request.AttendanceDate.Date, DateTimeKind.Utc);
            var sheetKey = RedisAttendanceKeys.ClassSheet(request.ClassId, dateUtc);

            // Đảm bảo sổ lớp đã có trên Redis
            var sheetVm = await GetOrLoadClassSheetAsync(request.ClassId, dateUtc);
            var studentDict = sheetVm.Students.ToDictionary(s => s.EnrollmentId, s => s);

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
                }
            }

            var updatedPayload = new RedisClassAttendancePayload
            {
                ClassId = sheetVm.ClassId,
                ClassName = sheetVm.ClassName,
                GradeLevel = sheetVm.GradeLevel,
                AttendanceDate = dateUtc,
                CachedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
                LastModifiedBy = SafeTruncate(request.ModifiedBy, 100) ?? "GLV",
                LastModifiedIp = SafeTruncate(request.IpAddress, 45),
                LastActionType = "BATCH_SAVE",
                IsDirty = true,
                Students = sheetVm.Students
            };

            await _cache.SetAsync(sheetKey, updatedPayload, DefaultCacheTtl);
            await MarkClassDirtyAsync(request.ClassId, dateUtc);

            // Làm mới lại trạng thái Checklist nhiệm vụ trong giờ học trên Redis
            await _cache.RemoveAsync(RedisAttendanceKeys.ClassSessionInfo(request.ClassId, dateUtc));

            return (true, $"Đã lưu nhanh sổ điểm danh ngày {dateUtc:dd/MM/yyyy} lên bộ đệm Redis ({request.Items.Count} em)! Hệ thống sẽ tự động đồng bộ bền vững xuống CSDL.");
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

            var sheetVm = await GetOrLoadClassSheetAsync(request.ClassId, dateUtc);
            var targetRow = sheetVm.Students.FirstOrDefault(s => s.EnrollmentId == request.EnrollmentId);
            if (targetRow == null)
            {
                return (false, "Không tìm thấy học sinh trong danh sách lớp này.", null);
            }

            var modifiedBy = SafeTruncate(request.ModifiedBy, 100) ?? "GLV";
            var clientIp = SafeTruncate(request.IpAddress, 45);

            bool finalMassAtt = targetRow.AttendedMass;
            string finalMassSt = targetRow.MassStatus;
            TimeSpan? finalMassTime = targetRow.MassCheckInTime;
            bool finalClassAtt = targetRow.ClassAttended;
            string finalClassSt = targetRow.ClassStatus;
            TimeSpan? finalClassTime = targetRow.ClassCheckInTime;
            string? finalNote = targetRow.Note;

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
                finalMassAtt = updateMass ? request.AttendedMass : (existing?.AttendedMass ?? targetRow.AttendedMass);
                finalMassSt = updateMass ? normalizedMassStatus : (existing?.MassStatus ?? targetRow.MassStatus);
                finalMassTime = updateMass
                    ? (request.AttendedMass ? (existing?.MassCheckInTime ?? targetRow.MassCheckInTime) : null)
                    : (existing != null ? existing.MassCheckInTime : targetRow.MassCheckInTime);

                finalClassAtt = updateClass ? request.ClassAttended : (existing?.ClassAttended ?? targetRow.ClassAttended);
                finalClassSt = updateClass ? normalizedClassStatus : (existing?.ClassStatus ?? targetRow.ClassStatus);
                finalClassTime = updateClass
                    ? (request.ClassAttended ? (existing?.ClassCheckInTime ?? targetRow.ClassCheckInTime) : null)
                    : (existing != null ? existing.ClassCheckInTime : targetRow.ClassCheckInTime);

                finalNote = updateNote ? normalizedNote : (existing != null ? existing.Note : targetRow.Note);

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
                        actionType, targetRow.StudentCode, dateUtc, updateTarget, modifiedBy, clientIp ?? "unknown", reasonSummary);
                }

                await transaction.CommitAsync();
            });

            // Đồng bộ trạng thái chuẩn cuối cùng về lại Redis ClassSheet
            targetRow.AttendedMass = finalMassAtt;
            targetRow.MassStatus = finalMassSt;
            targetRow.MassCheckInTime = finalMassTime;
            targetRow.ClassAttended = finalClassAtt;
            targetRow.ClassStatus = finalClassSt;
            targetRow.ClassCheckInTime = finalClassTime;
            targetRow.Note = finalNote;

            var existingSheetPayload = await _cache.GetAsync<RedisClassAttendancePayload>(sheetKey);
            var updatedPayload = new RedisClassAttendancePayload
            {
                ClassId = sheetVm.ClassId,
                ClassName = sheetVm.ClassName,
                GradeLevel = sheetVm.GradeLevel,
                AttendanceDate = dateUtc,
                CachedAtUtc = existingSheetPayload?.CachedAtUtc ?? DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
                LastModifiedBy = modifiedBy,
                LastModifiedIp = clientIp,
                LastActionType = "QUICK_UPDATE",
                IsDirty = existingSheetPayload?.IsDirty ?? false,
                Students = sheetVm.Students
            };

            await _cache.SetAsync(sheetKey, updatedPayload, DefaultCacheTtl);
            await _cache.RemoveAsync(RedisAttendanceKeys.ClassSessionInfo(request.ClassId, dateUtc));
            await _cache.RemoveAsync($"staff_lookup:student_360:{targetRow.StudentId}");

            var displayName = $"{targetRow.ChristianName} {targetRow.FullName}".Trim();
            return (true, $"Đã lưu điểm danh: {displayName}", targetRow);
        }

        /// <summary>
        /// 5. TASK-P3-12.3: Quét mã QR tốc độ cao trực tiếp trên bộ đệm Redis (Nâng cấp FEAT-P5-04, kết hợp Write-Through DB tại AttendanceController)
        /// </summary>
        public async Task<QrScanResponseDto> ProcessQrCheckInViaRedisAsync(QrScanRequestDto request)
        {
            var cleanCode = request.StudentCode.Trim().ToUpperInvariant();
            var scanTime = request.ScanTimestamp ?? DateTime.UtcNow.AddHours(7);
            var todayUtc = DateTime.SpecifyKind(scanTime.Date, DateTimeKind.Utc);

            var qrKey = RedisAttendanceKeys.QrStudentLookup(todayUtc, cleanCode);
            var lookupItem = await _cache.GetAsync<RedisQrStudentLookupItem>(qrKey);

            bool isHistoricalCode = false;
            string? oldCode = null;

            // Nếu chưa được Pre-warm trong Redis thì tìm trong DB (hỗ trợ cả mã chính và mã năm cũ trong StudentCodeHistories)
            if (lookupItem == null)
            {
                var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
                if (currentYear == null)
                {
                    return new QrScanResponseDto { Success = false, Message = "Chưa có niên khóa nào được kích hoạt!" };
                }

                var enrollment = await _context.Enrollments
                    .Include(e => e.Student)
                    .Include(e => e.ClassRoom)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Student.StudentCode.ToUpper() == cleanCode &&
                                              e.ClassRoom.AcademicYearId == currentYear.Id &&
                                              e.Student.IsActive);

                // TASK-P5-04.2: Nếu không khớp mã hiện hành, tra cứu trong bảng lịch sử StudentCodeHistories
                if (enrollment == null)
                {
                    var history = await _context.StudentCodeHistories
                        .Include(h => h.Student)
                        .AsNoTracking()
                        .Where(h => h.OriginalCode.ToUpper() == cleanCode || h.ArchivedYearCode.ToUpper() == cleanCode)
                        .OrderByDescending(h => h.ChangedAt)
                        .FirstOrDefaultAsync();

                    if (history?.Student != null && history.Student.IsActive)
                    {
                        enrollment = await _context.Enrollments
                            .Include(e => e.Student)
                            .Include(e => e.ClassRoom)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(e => e.StudentId == history.StudentId &&
                                                      e.ClassRoom.AcademicYearId == currentYear.Id &&
                                                      e.Student.IsActive);

                        if (enrollment != null)
                        {
                            isHistoricalCode = true;
                            oldCode = cleanCode;
                        }
                    }
                }

                if (enrollment == null)
                {
                    return new QrScanResponseDto
                    {
                        Success = false,
                        StudentCode = cleanCode,
                        Message = $"Không tìm thấy thiếu nhi có mã '{cleanCode}' trong niên khóa {currentYear.Name}!"
                    };
                }

                await GetOrLoadClassSheetAsync(enrollment.ClassRoomId, todayUtc);
                var officialCode = enrollment.Student.StudentCode.Trim().ToUpperInvariant();
                var officialKey = RedisAttendanceKeys.QrStudentLookup(todayUtc, officialCode);
                lookupItem = await _cache.GetAsync<RedisQrStudentLookupItem>(officialKey);

                if (lookupItem == null)
                {
                    return new QrScanResponseDto { Success = false, Message = "Không thể nạp bộ đệm điểm danh cho học sinh này." };
                }

                // Nếu quét bằng mã năm cũ, cache thêm alias trỏ về cùng lookupItem để lần quét kế tiếp đạt O(1)
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

                    // Tự động đối chiếu và ghi nhận vào bảng MassCampaignRecords nếu lớp có chiến dịch đang mở
                    try
                    {
                        var activeCampaign = await _context.MassCampaigns
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.ClassRoomId == lookupItem.ClassId &&
                                                      c.Status == "ACTIVE" &&
                                                      todayUtc >= c.StartDate.Date &&
                                                      todayUtc <= c.EndDate.Date);

                        if (activeCampaign != null)
                        {
                            var campRecord = await _context.MassCampaignRecords
                                .FirstOrDefaultAsync(r => r.CampaignId == activeCampaign.Id &&
                                                          r.EnrollmentId == lookupItem.EnrollmentId &&
                                                          r.AttendanceDate == todayUtc);
                            if (campRecord == null)
                            {
                                _context.MassCampaignRecords.Add(new MassCampaignRecord
                                {
                                    CampaignId = activeCampaign.Id,
                                    StudentId = lookupItem.StudentId,
                                    EnrollmentId = lookupItem.EnrollmentId,
                                    AttendanceDate = todayUtc,
                                    Status = "PRESENT",
                                    CheckInMethod = "QR_SCAN",
                                    CheckInTime = DateTime.UtcNow,
                                    LastModifiedBy = "Camera QR HUD",
                                    LastModifiedAt = DateTime.UtcNow
                                });
                            }
                            else
                            {
                                campRecord.Status = "PRESENT";
                                campRecord.CheckInMethod = "QR_SCAN";
                                campRecord.CheckInTime = DateTime.UtcNow;
                                campRecord.LastModifiedBy = "Camera QR HUD";
                                campRecord.LastModifiedAt = DateTime.UtcNow;
                            }
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lỗi cập nhật chiến dịch đi lễ cho EnrollmentId {EnrollmentId}", lookupItem.EnrollmentId);
                    }
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

            var sheetKey = RedisAttendanceKeys.ClassSheet(lookupItem.ClassId, todayUtc);
            var sheetVm = await GetOrLoadClassSheetAsync(lookupItem.ClassId, todayUtc);

            var targetRow = sheetVm.Students.FirstOrDefault(s => s.EnrollmentId == lookupItem.EnrollmentId);
            if (targetRow == null)
            {
                return new QrScanResponseDto
                {
                    Success = false,
                    StudentCode = cleanCode,
                    Message = "Học sinh chưa có trong danh sách bộ đệm của lớp."
                };
            }

            bool isDuplicate = false;
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
                }
            }

            if (!isDuplicate)
            {
                var updatedPayload = new RedisClassAttendancePayload
                {
                    ClassId = lookupItem.ClassId,
                    ClassName = lookupItem.ClassName,
                    GradeLevel = lookupItem.GradeLevel,
                    AttendanceDate = todayUtc,
                    CachedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                    LastModifiedBy = SafeTruncate(request.ModifiedBy, 100) ?? "Camera QR HUD",
                    LastModifiedIp = SafeTruncate(request.IpAddress, 45),
                    LastActionType = "QR_SCAN",
                    IsDirty = true,
                    Students = sheetVm.Students
                };

                await _cache.SetAsync(sheetKey, updatedPayload, DefaultCacheTtl);
                await MarkClassDirtyAsync(lookupItem.ClassId, todayUtc);
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

            var date = dto.AttendanceDate ?? DateTime.UtcNow.AddHours(7).Date;
            var todayUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var sheetKey = RedisAttendanceKeys.ClassSheet(dto.ClassId, todayUtc);
            var sheetVm = await GetOrLoadClassSheetAsync(dto.ClassId, todayUtc);

            var targetRow = sheetVm.Students.FirstOrDefault(s => s.EnrollmentId == dto.EnrollmentId);
            string studentName = targetRow?.FullName ?? dto.StudentCode ?? "Thiếu nhi";

            bool undoMass = string.IsNullOrWhiteSpace(dto.ActivityType) ||
                            dto.ActivityType.Contains("LỄ") ||
                            dto.ActivityType.Contains("MASS") ||
                            dto.ActivityType.Contains("CHIẾN DỊCH");

            bool undoClass = string.IsNullOrWhiteSpace(dto.ActivityType) ||
                             dto.ActivityType.Contains("GIÁO LÝ") ||
                             dto.ActivityType.Contains("CLASS");

            if (targetRow != null)
            {
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

                var updatedPayload = new RedisClassAttendancePayload
                {
                    ClassId = dto.ClassId,
                    ClassName = sheetVm.ClassName,
                    GradeLevel = sheetVm.GradeLevel,
                    AttendanceDate = todayUtc,
                    CachedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                    IsDirty = true,
                    Students = sheetVm.Students
                };

                await _cache.SetAsync(sheetKey, updatedPayload, DefaultCacheTtl);
                await MarkClassDirtyAsync(dto.ClassId, todayUtc);
            }

            // Đồng bộ trực tiếp xuống cơ sở dữ liệu PostgreSQL + Ghi Audit Log
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

                var undoOperator = SafeTruncate(dto.ModifiedBy, 100) ?? "Camera QR HUD";
                var undoIp = SafeTruncate(dto.IpAddress, 45);
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

            // Hoàn tác bản ghi chiến dịch nếu thuộc chiến dịch
            if (!string.IsNullOrWhiteSpace(dto.ActivityType) && dto.ActivityType.Contains("CHIẾN DỊCH"))
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

            return (true, $"Đã hoàn tác điểm danh thành công cho thiếu nhi {studentName}!");
        }

        /// <summary>
        /// 6. TASK-P3-12.4 / P2-LOG-16: Batch Upsert dữ liệu điểm danh còn cờ IsDirty từ Redis xuống PostgreSQL (được gọi Write-Through trực tiếp ở ScanCheckIn/SaveBatch và chạy dự phòng định kỳ mỗi 2 phút qua Hangfire).
        /// </summary>
        public async Task<(int ClassesSynced, int RecordsUpserted)> SyncDirtyAttendancesToDatabaseAsync(DateTime? targetDate = null)
        {
            var datesToSync = targetDate.HasValue
                ? new[] { DateTime.SpecifyKind(targetDate.Value.Date, DateTimeKind.Utc) }
                : new[]
                {
                    DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                    DateTime.SpecifyKind(DateTime.Today.AddDays(-1), DateTimeKind.Utc)
                };

            int classesSynced = 0;
            int recordsUpserted = 0;

            foreach (var dt in datesToSync)
            {
                var dirtyKey = RedisAttendanceKeys.DirtyPartitionsIndex(dt);
                var dirtyClassIds = await _cache.GetAsync<HashSet<int>>(dirtyKey);

                if (dirtyClassIds == null || !dirtyClassIds.Any())
                    continue;

                var remainingDirtyIds = new HashSet<int>();

                foreach (var classId in dirtyClassIds)
                {
                    var sheetKey = RedisAttendanceKeys.ClassSheet(classId, dt);
                    var payload = await _cache.GetAsync<RedisClassAttendancePayload>(sheetKey);

                    if (payload == null || !payload.IsDirty || !payload.Students.Any())
                        continue;

                    try
                    {
                        int count = await FlushSingleClassPayloadToDbAsync(payload, dt);
                        recordsUpserted += count;
                        classesSynced++;

                        // Đánh dấu đã đồng bộ xong trên Redis
                        payload.IsDirty = false;
                        await _cache.SetAsync(sheetKey, payload, DefaultCacheTtl);
                        await _cache.RemoveAsync(RedisAttendanceKeys.ClassSessionInfo(classId, dt));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SyncBackJob] Lỗi khi đồng bộ điểm danh từ Redis về DB cho Lớp ID={ClassId}, Ngày={Date:yyyy-MM-dd}", classId, dt);
                        remainingDirtyIds.Add(classId);
                    }
                }

                if (remainingDirtyIds.Any())
                {
                    await _cache.SetAsync(dirtyKey, remainingDirtyIds, DefaultCacheTtl);
                }
                else
                {
                    await _cache.RemoveAsync(dirtyKey);
                }
            }

            if (classesSynced > 0)
            {
                _logger.LogInformation("[SyncBackJob] Đã đồng bộ thành công {Records} bản ghi của {Classes} lớp từ Redis xuống PostgreSQL.",
                    recordsUpserted, classesSynced);
            }

            return (classesSynced, recordsUpserted);
        }

        public async Task<int> AutoMarkAbsentUncheckedStudentsAsync(DateTime? targetDate = null)
        {
            var vnNow = DateTime.UtcNow.AddHours(7);
            var rawDate = targetDate?.Date ?? vnNow.Date;
            var dateUtc = DateTime.SpecifyKind(rawDate, DateTimeKind.Utc);

            // Chỉ tự động đánh vắng cho ngày sinh hoạt định kỳ: Thứ Năm (Lễ T5) và Chúa Nhật (Lễ CN + Học GL)
            if (dateUtc.DayOfWeek != DayOfWeek.Thursday && dateUtc.DayOfWeek != DayOfWeek.Sunday)
            {
                return 0;
            }

            // 1. Đồng bộ toàn bộ dữ liệu đang chờ trên Redis xuống DB trước khi rà soát
            await SyncDirtyAttendancesToDatabaseAsync(dateUtc);

            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            if (currentYear == null) return 0;

            // 2. Lấy toàn bộ danh sách Enrollment đang theo học trong niên khóa hiện hành
            var activeEnrollmentIds = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.ClassRoom.AcademicYearId == currentYear.Id && e.Student.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

            if (!activeEnrollmentIds.Any()) return 0;

            // 3. Lấy danh sách EnrollmentId đã có bản ghi Attendance trong ngày
            var recordedEnrollmentIds = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.AttendanceDate == dateUtc && activeEnrollmentIds.Contains(a.EnrollmentId))
                .Select(a => a.EnrollmentId)
                .ToListAsync();

            // 4. Kiểm tra các đơn xin phép nghỉ đã được duyệt (APPROVED) for ngày này
            var approvedAbsences = await _context.AbsenceRequests
                .AsNoTracking()
                .Where(r => r.AbsenceDate == dateUtc && r.Status == "APPROVED" && activeEnrollmentIds.Contains(r.EnrollmentId))
                .ToDictionaryAsync(r => r.EnrollmentId, r => r.SessionType);

            var missingEnrollmentIds = activeEnrollmentIds
                .Where(id => !recordedEnrollmentIds.Contains(id))
                .ToList();

            if (!missingEnrollmentIds.Any()) return 0;

            bool isSunday = dateUtc.DayOfWeek == DayOfWeek.Sunday;
            var newAbsentRecords = new List<Attendance>(missingEnrollmentIds.Count);

            foreach (var enrollmentId in missingEnrollmentIds)
            {
                approvedAbsences.TryGetValue(enrollmentId, out var permittedSession);

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

            _logger.LogInformation(
                "[Hangfire AutoAbsent] Đã tự động ghi nhận vắng xuống DB cho {Count} thiếu nhi chưa điểm danh ngày {Date:dd/MM/yyyy}.",
                newAbsentRecords.Count, dateUtc);

            return newAbsentRecords.Count;
        }

        #region Helpers Nội bộ
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

        private async Task MarkClassDirtyAsync(int classId, DateTime dateUtc)
        {
            var dirtyKey = RedisAttendanceKeys.DirtyPartitionsIndex(dateUtc);
            var dirtySet = await _cache.GetAsync<HashSet<int>>(dirtyKey) ?? new HashSet<int>();
            dirtySet.Add(classId);
            await _cache.SetAsync(dirtyKey, dirtySet, DefaultCacheTtl);
        }

        private async Task<int> FlushSingleClassPayloadToDbAsync(RedisClassAttendancePayload payload, DateTime targetDateUtc)
        {
            var enrollmentIds = payload.Students.Select(s => s.EnrollmentId).ToList();
            var strategy = _context.Database.CreateExecutionStrategy();
            int affected = 0;

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var existingRecords = await _context.Attendances
                    .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.AttendanceDate == targetDateUtc)
                    .ToDictionaryAsync(a => a.EnrollmentId);

                var newRecords = new List<Attendance>();
                var pendingAudits = new List<(Attendance Record, bool IsNew, bool OldMassAtt, string OldMassSt, bool OldClassAtt, string OldClassSt, string? OldNote)>();

                foreach (var item in payload.Students)
                {
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

                        if (changed)
                        {
                            pendingAudits.Add((existing, false, oldMassAtt, oldMassSt, oldClassAtt, oldClassSt, oldNote));
                        }
                    }
                    else
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

                        bool meaningfulNew = item.AttendedMass ||
                                             item.MassStatus != AttendanceStatus.AbsentUnpermitted ||
                                             item.ClassAttended ||
                                             item.ClassStatus != AttendanceStatus.AbsentUnpermitted ||
                                             !string.IsNullOrWhiteSpace(item.Note);
                        if (meaningfulNew)
                        {
                            pendingAudits.Add((newRecord, true, false, AttendanceStatus.AbsentUnpermitted, false, AttendanceStatus.AbsentUnpermitted, null));
                        }
                    }
                    affected++;
                }

                if (newRecords.Any())
                {
                    await _context.Attendances.AddRangeAsync(newRecords);
                }

                await _context.SaveChangesAsync();

                if (pendingAudits.Any())
                {
                    var actionType = SafeTruncate(payload.LastActionType, 50) ?? "BATCH_SAVE";
                    var prefix = actionType == "QR_SCAN" ? "Quét mã QR" : "Lưu sổ điểm danh";
                    var modifiedBy = SafeTruncate(payload.LastModifiedBy, 100) ?? "SYSTEM";
                    var clientIp = SafeTruncate(payload.LastModifiedIp, 45);

                    var auditLogs = pendingAudits.Select(p => new AttendanceAuditLog
                    {
                        AttendanceId = p.Record.Id,
                        ActionType = actionType,
                        OldValues = FormatStateSnapshot(p.OldMassAtt, p.OldMassSt, p.OldClassAtt, p.OldClassSt, p.OldNote, isNewRecord: p.IsNew),
                        NewValues = FormatStateSnapshot(p.Record.AttendedMass, p.Record.MassStatus, p.Record.ClassAttended, p.Record.ClassStatus, p.Record.Note),
                        ModifiedBy = modifiedBy,
                        Reason = BuildAuditReasonSummary(
                            prefix, p.IsNew,
                            p.OldMassAtt, p.OldMassSt, p.Record.AttendedMass, p.Record.MassStatus,
                            p.OldClassAtt, p.OldClassSt, p.Record.ClassAttended, p.Record.ClassStatus,
                            p.OldNote, p.Record.Note),
                        IpAddress = clientIp,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();

                    await _context.AttendanceAuditLogs.AddRangeAsync(auditLogs);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            });

            return affected;
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