using ClosedXML.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Enum;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class SacramentEvaluationService : ISacramentEvaluationService
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;
        private readonly ILogger<SacramentEvaluationService> _logger;

        public SacramentEvaluationService(
            ParishDbContext context,
            IGradeCalculationService gradeService,
            ILogger<SacramentEvaluationService> logger)
        {
            _context = context;
            _gradeService = gradeService;
            _logger = logger;
        }

        public string GetSacramentDisplayName(int sacramentType)
        {
            return sacramentType switch
            {
                (int)SacramentTypeEnum.Baptism => "Bí tích Rửa Tội",
                (int)SacramentTypeEnum.FirstCommunion => "Xưng Tội & Rước Lễ Lần Đầu",
                (int)SacramentTypeEnum.Confirmation => "Bí tích Thêm Sức",
                (int)SacramentTypeEnum.ProfessionOfFaith => "Bao Đồng (Tuyên Hứa Sống Đạo)",
                _ => "Bí tích Giáo Lý"
            };
        }

        public int InferDefaultSacramentTypeByGrade(string gradeLevel)
        {
            if (string.IsNullOrWhiteSpace(gradeLevel)) return (int)SacramentTypeEnum.FirstCommunion;
            if (gradeLevel.Contains("Khai Tâm", StringComparison.OrdinalIgnoreCase)) return (int)SacramentTypeEnum.Baptism;
            if (gradeLevel.Contains("Rước Lễ", StringComparison.OrdinalIgnoreCase)) return (int)SacramentTypeEnum.FirstCommunion;
            if (gradeLevel.Contains("Thêm Sức", StringComparison.OrdinalIgnoreCase)) return (int)SacramentTypeEnum.Confirmation;
            if (gradeLevel.Contains("Bao Đồng", StringComparison.OrdinalIgnoreCase)) return (int)SacramentTypeEnum.ProfessionOfFaith;
            return (int)SacramentTypeEnum.FirstCommunion;
        }

        public async Task<SacramentEvaluationFilterViewModel> EvaluateCandidatesAsync(
            int academicYearId,
            string gradeLevel,
            int? classId,
            int sacramentType,
            decimal minYearAvg = 5.0m,
            double minAttendanceRate = 80.0,
            bool requireSacramentExam = true,
            decimal minSacramentScore = 5.0m,
            long? batchId = null)
        {
            var allYears = await _context.AcademicYears
                .OrderByDescending(y => y.StartDate)
                .AsNoTracking()
                .ToListAsync();

            var targetYear = allYears.FirstOrDefault(y => y.Id == academicYearId)
                             ?? allYears.FirstOrDefault(y => y.IsCurrent)
                             ?? allYears.FirstOrDefault();

            int yearId = targetYear?.Id ?? 0;

            SacramentApprovalBatch? loadedBatch = null;
            if (batchId.HasValue && batchId.Value > 0)
            {
                loadedBatch = await _context.SacramentApprovalBatches
                    .Include(b => b.Items)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == batchId.Value);

                if (loadedBatch != null)
                {
                    yearId = loadedBatch.AcademicYearId;
                    gradeLevel = loadedBatch.GradeLevel;
                    classId = loadedBatch.ClassRoomId;
                    sacramentType = loadedBatch.SacramentType;
                    minYearAvg = loadedBatch.MinYearAverage;
                    minAttendanceRate = loadedBatch.MinAttendanceRate;
                    requireSacramentExam = loadedBatch.RequireSacramentExam;
                    minSacramentScore = loadedBatch.MinSacramentExamScore;
                }
            }

            var classesInGrade = await _context.Classes
                .Where(c => c.AcademicYearId == yearId && (gradeLevel == "ALL" || c.GradeLevel == gradeLevel))
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            var savedBatches = await _context.SacramentApprovalBatches
                .Where(b => b.AcademicYearId == yearId && (gradeLevel == "ALL" || b.GradeLevel == gradeLevel))
                .OrderByDescending(b => b.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var targetClassIds = classId.HasValue && classId.Value > 0
                ? classesInGrade.Where(c => c.Id == classId.Value).Select(c => c.Id).ToList()
                : classesInGrade.Select(c => c.Id).ToList();

            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.ClassRoom)
                .Include(e => e.GradeRecords)
                .Where(e => targetClassIds.Contains(e.ClassRoomId) && e.Student.IsActive)
                .OrderBy(e => e.ClassRoom.Name)
                .ThenBy(e => e.Student.LastName)
                .ThenBy(e => e.Student.FirstName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

            var yearGradeConfigs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == yearId)
                .AsNoTracking()
                .ToListAsync();

            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            var existingSacramentRecords = await _context.StudentSacramentRecords
                .Where(r => studentIds.Contains(r.StudentId) && r.SacramentType == sacramentType)
                .AsNoTracking()
                .ToDictionaryAsync(r => r.StudentId, r => r);

            var savedItemDict = loadedBatch?.Items.ToDictionary(i => i.StudentId, i => i)
                                ?? new Dictionary<long, SacramentApprovalItem>();

            // Tính tổng số buổi Lễ Chúa Nhật và Giờ Học theo từng lớp
            var attendancesByClass = enrollments
                .GroupBy(e => e.ClassRoomId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var classEnrollmentIds = g.Select(x => x.Id).ToHashSet();
                        var classAtts = attendances.Where(a => classEnrollmentIds.Contains(a.EnrollmentId)).ToList();
                        int sunMassDays = Math.Max(1, classAtts
                            .Where(a => a.DayOfWeek == DayOfWeek.Sunday && (a.AttendedMass || a.MassStatus != AttendanceStatus.AbsentUnpermitted))
                            .Select(a => a.AttendanceDate.Date).Distinct().Count());
                        int classDays = Math.Max(1, classAtts
                            .Where(a => a.ClassAttended || a.ClassStatus != AttendanceStatus.AbsentUnpermitted)
                            .Select(a => a.AttendanceDate.Date).Distinct().Count());
                        return (SunMassDays: sunMassDays, ClassDays: classDays);
                    });

            var candidateRows = new List<StudentSacramentEvalRowDto>();
            int seqIndex = 1;

            foreach (var e in enrollments)
            {
                var classConfigs = yearGradeConfigs
                    .Where(g => (g.GradeLevel == "ALL" || g.GradeLevel == e.ClassRoom.GradeLevel) &&
                                (g.ClassRoomId == null || g.ClassRoomId == e.ClassRoomId))
                    .ToList();

                var hk1Configs = classConfigs.Where(c => c.Semester == 1).ToList();
                var hk2Configs = classConfigs.Where(c => c.Semester == 2).ToList();
                var sacExamConfigs = classConfigs.Where(c => c.IsSacramentExam).ToList();

                var hk1Scores = e.GradeRecords
                    .Where(r => hk1Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var hk2Scores = e.GradeRecords
                    .Where(r => hk2Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var sem1Avg = _gradeService.CalculateSemesterAverage(hk1Scores, hk1Configs);
                var sem2Avg = _gradeService.CalculateSemesterAverage(hk2Scores, hk2Configs);
                var yearAvg = _gradeService.CalculateYearAverage(sem1Avg, sem2Avg);

                bool hasSacExamCol = sacExamConfigs.Any();
                decimal? sacExamScore = null;
                if (hasSacExamCol)
                {
                    var validSacScores = e.GradeRecords
                        .Where(r => sacExamConfigs.Select(c => c.Id).Contains(r.GradeConfigurationId) && r.Score.HasValue)
                        .Select(r => r.Score!.Value)
                        .ToList();

                    if (validSacScores.Any())
                    {
                        sacExamScore = Math.Round(validSacScores.Average(), 2);
                    }
                }

                // Tính % Chuyên cần Lễ Chúa Nhật & Học Giáo Lý
                var stuAtts = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                var sessionTotals = attendancesByClass.TryGetValue(e.ClassRoomId, out var totals) ? totals : (1, 1);

                var sunPresent = stuAtts.Count(a => a.DayOfWeek == DayOfWeek.Sunday && a.AttendedMass &&
                                                   (a.MassStatus == AttendanceStatus.Present || a.MassStatus == AttendanceStatus.LinedUp));
                var sunLate = stuAtts.Count(a => a.DayOfWeek == DayOfWeek.Sunday && a.AttendedMass && a.MassStatus == AttendanceStatus.Late);

                var clsPresent = stuAtts.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Present);
                var clsLate = stuAtts.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Late);
                var makeUps = stuAtts.Count(a => a.IsMakeUp);

                double massRate = stuAtts.Any()
                    ? Math.Min(100.0, Math.Round(((sunPresent + sunLate * 0.8) / stuAtts.Count(a => a.DayOfWeek == DayOfWeek.Sunday)) * 100.0, 1))
                    : 100.0;

                double classRate = stuAtts.Any()
                    ? Math.Min(100.0, Math.Round(((clsPresent + clsLate * 0.8) / stuAtts.Count(a => a.DayOfWeek == DayOfWeek.Sunday)) * 100.0, 1))
                    : 100.0;

                double combinedRate = Math.Round((massRate + classRate) / 2.0, 1);

                var (academicRank, _) = _gradeService.EvaluateStudent(yearAvg, combinedRate, e.ClassRoom.GradeLevel, 0);

                // Kiểm tra đã nhận Bí tích này trong Sổ bộ chưa
                bool alreadyReceived = existingSacramentRecords.TryGetValue(e.StudentId, out var existingSacRecord);
                if (!alreadyReceived && sacramentType == (int)SacramentTypeEnum.Baptism && e.Student.BaptismDate.HasValue)
                {
                    alreadyReceived = true;
                }
                else if (!alreadyReceived && sacramentType == (int)SacramentTypeEnum.Confirmation && e.Student.ConfirmationDate.HasValue)
                {
                    alreadyReceived = true;
                }

                // Kiểm tra các điều kiện tự động
                var failReasons = new List<string>();
                if (!yearAvg.HasValue || yearAvg.Value < minYearAvg)
                {
                    failReasons.Add($"ĐTB năm ({(yearAvg?.ToString("0.00") ?? "Chưa có")}) < {minYearAvg:0.0}");
                }
                if (combinedRate < minAttendanceRate)
                {
                    failReasons.Add($"Chuyên cần ({combinedRate:0.0}%) < {minAttendanceRate:0.#}%");
                }
                if (requireSacramentExam && hasSacExamCol)
                {
                    if (!sacExamScore.HasValue || sacExamScore.Value < minSacramentScore)
                    {
                        failReasons.Add($"Khảo hạch ({(sacExamScore?.ToString("0.00") ?? "Chưa thi")}) < {minSacramentScore:0.0}");
                    }
                }

                bool isSystemEligible = failReasons.Count == 0;
                bool isSpecialExemption = false;
                bool isApproved = isSystemEligible && !alreadyReceived;
                string? godparent = null;
                string defaultBookNo = $"SB-{targetYear?.Name?.Split('-').FirstOrDefault() ?? DateTime.Now.Year.ToString()}-{seqIndex++:D3}";
                string? assignedBookNo = defaultBookNo;
                string reasonText = alreadyReceived
                    ? "Đã lãnh nhận Bí tích trong Sổ bộ"
                    : (isSystemEligible ? "Đủ điều kiện chuẩn" : string.Join("; ", failReasons));

                if (savedItemDict.TryGetValue(e.StudentId, out var savedItem))
                {
                    isSpecialExemption = savedItem.IsSpecialExemption;
                    isApproved = savedItem.IsApprovedForSacrament;
                    godparent = savedItem.GodparentName;
                    assignedBookNo = string.IsNullOrWhiteSpace(savedItem.AssignedRegistryNumber)
                        ? defaultBookNo
                        : savedItem.AssignedRegistryNumber;
                    if (!string.IsNullOrWhiteSpace(savedItem.EvaluationNote))
                    {
                        reasonText = savedItem.EvaluationNote;
                    }
                }

                candidateRows.Add(new StudentSacramentEvalRowDto
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    Gender = e.Student.Gender ?? "Nam",
                    DateOfBirth = e.Student.DateOfBirth,
                    ClassName = e.ClassRoom.Name,
                    GradeLevel = e.ClassRoom.GradeLevel,
                    Semester1Average = sem1Avg,
                    Semester2Average = sem2Avg,
                    YearAverage = yearAvg,
                    HasSacramentExamColumn = hasSacExamCol,
                    SacramentExamScore = sacExamScore,
                    MassAttendanceRate = massRate,
                    ClassAttendanceRate = classRate,
                    CombinedAttendanceRate = combinedRate,
                    TotalMakeUpCount = makeUps,
                    AcademicRank = academicRank,
                    AlreadyReceivedSacrament = alreadyReceived,
                    ExistingReceivedDateStr = existingSacRecord?.ReceivedDate.ToString("dd/MM/yyyy")
                        ?? (sacramentType == 1 ? e.Student.BaptismDate?.ToString("dd/MM/yyyy") : e.Student.ConfirmationDate?.ToString("dd/MM/yyyy")),
                    ExistingBookNumber = existingSacRecord?.RegistryBookNumber,
                    IsSystemEligible = isSystemEligible,
                    IsSpecialExemption = isSpecialExemption,
                    IsApprovedForSacrament = isApproved,
                    GodparentName = godparent,
                    AssignedRegistryNumber = assignedBookNo,
                    EvaluationReason = reasonText
                });
            }

            string sacDisplayName = GetSacramentDisplayName(sacramentType);
            string defaultTitle = $"Danh sách đệ trình lãnh nhận {sacDisplayName} - Khối {gradeLevel} ({targetYear?.Name})";

            return new SacramentEvaluationFilterViewModel
            {
                AcademicYearId = yearId,
                AcademicYearName = targetYear?.Name ?? "",
                SelectedGrade = gradeLevel,
                SelectedClassId = classId,
                SacramentType = sacramentType,
                SacramentName = sacDisplayName,
                MinYearAverage = minYearAvg,
                MinAttendanceRate = minAttendanceRate,
                RequireSacramentExam = requireSacramentExam,
                MinSacramentExamScore = minSacramentScore,
                ExistingBatchId = loadedBatch?.Id,
                BatchStatus = loadedBatch?.Status ?? "NEW",
                BatchTitle = loadedBatch?.Title ?? defaultTitle,
                PlannedCeremonyDate = loadedBatch?.PlannedCeremonyDate ?? DateTime.Today,
                CeremonyParish = loadedBatch?.CeremonyParish ?? "Giáo xứ Phú Trung",
                CelebrantPriest = loadedBatch?.CelebrantPriest ?? "",
                RegistryBookPrefix = loadedBatch?.RegistryBookPrefix ?? $"Q.{DateTime.Now.Year}",
                ApprovedBy = loadedBatch?.ApprovedBy,
                ApprovedAt = loadedBatch?.ApprovedAt,
                AcademicYears = allYears,
                AvailableClasses = classesInGrade,
                SavedBatches = savedBatches,
                Candidates = candidateRows
            };
        }

        public async Task<SacramentApprovalBatch> SaveDraftBatchAsync(
            SaveSacramentApprovalBatchRequestDto dto,
            string operatorName)
        {
            var evalVm = await EvaluateCandidatesAsync(
                dto.AcademicYearId,
                dto.GradeLevel,
                dto.ClassRoomId,
                dto.SacramentType,
                dto.MinYearAverage,
                dto.MinAttendanceRate,
                dto.RequireSacramentExam,
                dto.MinSacramentExamScore);

            var evalMap = evalVm.Candidates.ToDictionary(c => c.StudentId, c => c);
            var strategy = _context.Database.CreateExecutionStrategy();
            SacramentApprovalBatch? batch = null;

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                if (dto.BatchId.HasValue && dto.BatchId.Value > 0)
                {
                    batch = await _context.SacramentApprovalBatches
                        .Include(b => b.Items)
                        .FirstOrDefaultAsync(b => b.Id == dto.BatchId.Value);
                }

                if (batch == null)
                {
                    batch = new SacramentApprovalBatch
                    {
                        AcademicYearId = dto.AcademicYearId,
                        GradeLevel = dto.GradeLevel,
                        ClassRoomId = dto.ClassRoomId,
                        SacramentType = dto.SacramentType,
                        Status = "DRAFT",
                        CreatedBy = operatorName,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.SacramentApprovalBatches.Add(batch);
                }
                else if (batch.Status == "APPROVED")
                {
                    throw new InvalidOperationException("Đợt xét duyệt này đã được phê duyệt và ghi vào Sổ bộ, không thể chỉnh sửa.");
                }

                batch.Title = dto.Title.Trim();
                batch.MinYearAverage = dto.MinYearAverage;
                batch.MinAttendanceRate = dto.MinAttendanceRate;
                batch.RequireSacramentExam = dto.RequireSacramentExam;
                batch.MinSacramentExamScore = dto.MinSacramentExamScore;
                batch.PlannedCeremonyDate = DateTime.SpecifyKind(dto.PlannedCeremonyDate.Date, DateTimeKind.Utc);
                batch.CeremonyParish = dto.CeremonyParish.Trim();
                batch.CelebrantPriest = dto.CelebrantPriest?.Trim();
                batch.RegistryBookPrefix = dto.RegistryBookPrefix?.Trim();
                batch.Notes = dto.Notes?.Trim();

                await _context.SaveChangesAsync();

                if (batch.Items.Any())
                {
                    _context.SacramentApprovalItems.RemoveRange(batch.Items);
                    await _context.SaveChangesAsync();
                }

                var newItems = new List<SacramentApprovalItem>();
                foreach (var input in dto.Items)
                {
                    if (!evalMap.TryGetValue(input.StudentId, out var calcRow)) continue;

                    newItems.Add(new SacramentApprovalItem
                    {
                        BatchId = batch.Id,
                        StudentId = input.StudentId,
                        EnrollmentId = calcRow.EnrollmentId,
                        ClassName = calcRow.ClassName,
                        Semester1Average = calcRow.Semester1Average,
                        Semester2Average = calcRow.Semester2Average,
                        YearAverage = calcRow.YearAverage,
                        SacramentExamScore = calcRow.SacramentExamScore,
                        MassAttendanceRate = calcRow.MassAttendanceRate,
                        ClassAttendanceRate = calcRow.ClassAttendanceRate,
                        CombinedAttendanceRate = calcRow.CombinedAttendanceRate,
                        IsSystemEligible = calcRow.IsSystemEligible,
                        IsSpecialExemption = input.IsSpecialExemption,
                        IsApprovedForSacrament = input.IsApprovedForSacrament,
                        GodparentName = input.GodparentName?.Trim(),
                        AssignedRegistryNumber = input.AssignedRegistryNumber?.Trim(),
                        EvaluationNote = string.IsNullOrWhiteSpace(input.EvaluationNote)
                            ? calcRow.EvaluationReason
                            : input.EvaluationNote.Trim()
                    });
                }

                if (newItems.Any())
                {
                    await _context.SacramentApprovalItems.AddRangeAsync(newItems);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            });

            return batch!;
        }

        public async Task<(bool Success, int SyncedCount, string Message)> ApproveBatchAndSyncToRegistryAsync(
            long batchId,
            string approverName)
        {
            var batch = await _context.SacramentApprovalBatches
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
            {
                return (false, 0, "Không tìm thấy tờ trình xét duyệt Bí tích.");
            }

            if (batch.Status == "APPROVED")
            {
                return (false, 0, "Tờ trình này đã được phê duyệt và cập nhật Sổ bộ trước đó.");
            }

            var approvedItems = batch.Items.Where(i => i.IsApprovedForSacrament).ToList();
            if (!approvedItems.Any())
            {
                return (false, 0, "Danh sách không có thiếu nhi nào được tích chọn phê duyệt.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            int syncedCount = 0;

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    var approvedStudentIds = approvedItems.Select(i => i.StudentId).ToList();
                    var studentsDict = await _context.Students
                        .Where(s => approvedStudentIds.Contains(s.Id))
                        .ToDictionaryAsync(s => s.Id, s => s);

                    var existingRecordsDict = await _context.StudentSacramentRecords
                        .IgnoreQueryFilters()
                        .Where(r => approvedStudentIds.Contains(r.StudentId) && r.SacramentType == batch.SacramentType)
                        .ToDictionaryAsync(r => r.StudentId, r => r);

                    var receivedDateUtc = DateTime.SpecifyKind(batch.PlannedCeremonyDate.Date, DateTimeKind.Utc);
                    int seq = 1;

                    foreach (var item in approvedItems)
                    {
                        string bookNo = !string.IsNullOrWhiteSpace(item.AssignedRegistryNumber)
                            ? item.AssignedRegistryNumber.Trim()
                            : $"{batch.RegistryBookPrefix ?? "SB"}-{seq++:D3}";

                        if (!existingRecordsDict.TryGetValue(item.StudentId, out var record))
                        {
                            record = new StudentSacramentRecord
                            {
                                StudentId = item.StudentId,
                                SacramentType = batch.SacramentType,
                                ReceivedDate = receivedDateUtc,
                                ParishName = batch.CeremonyParish,
                                PriestName = batch.CelebrantPriest ?? "Linh mục Chánh xứ",
                                GodparentName = item.GodparentName ?? string.Empty,
                                RegistryBookNumber = bookNo,
                                Notes = item.IsSpecialExemption
                                    ? $"[Đặc cách - Đợt #{batch.Id}]: {item.EvaluationNote}"
                                    : $"Phê duyệt theo Tờ trình #{batch.Id} (ĐTB: {item.YearAverage?.ToString("0.00")}, CC: {item.CombinedAttendanceRate:0.0}%)",
                                CreatedBy = approverName,
                                UpdatedBy = approverName,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                IsDeleted = false
                            };
                            _context.StudentSacramentRecords.Add(record);
                        }
                        else
                        {
                            record.ReceivedDate = receivedDateUtc;
                            record.ParishName = batch.CeremonyParish;
                            record.PriestName = batch.CelebrantPriest ?? record.PriestName;
                            if (!string.IsNullOrWhiteSpace(item.GodparentName))
                            {
                                record.GodparentName = item.GodparentName.Trim();
                            }
                            record.RegistryBookNumber = bookNo;
                            record.UpdatedBy = approverName;
                            record.UpdatedAt = DateTime.UtcNow;
                            record.IsDeleted = false;
                            record.DeletedAt = null;
                        }

                        // Đồng bộ ngược cột ngày Rửa Tội (1) và Thêm Sức (3) trên bảng Students
                        if (studentsDict.TryGetValue(item.StudentId, out var student))
                        {
                            if (batch.SacramentType == (int)SacramentTypeEnum.Baptism)
                            {
                                student.BaptismDate = receivedDateUtc;
                            }
                            else if (batch.SacramentType == (int)SacramentTypeEnum.Confirmation)
                            {
                                student.ConfirmationDate = receivedDateUtc;
                            }
                        }

                        syncedCount++;
                    }

                    batch.Status = "APPROVED";
                    batch.ApprovedBy = approverName;
                    batch.ApprovedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                _logger.LogInformation(
                    "AUDIT SACRAMENT_BATCH_APPROVE: User={Approver} approved BatchId={BatchId} and synced {Count} records to SacramentRegistry.",
                    approverName, batchId, syncedCount);

                return (true, syncedCount, $"Đã phê duyệt chính thức và tự động ghi {syncedCount} hồ sơ vào Sổ bộ Bí tích điện tử!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phê duyệt và đồng bộ Sổ bộ Bí tích cho BatchId={BatchId}", batchId);
                return (false, 0, "Đã xảy ra lỗi khi đồng bộ Sổ bộ Bí tích. Giao dịch đã được hoàn tác an toàn.");
            }
        }

        public async Task<byte[]> ExportApprovalSheetToExcelAsync(
            int academicYearId,
            string gradeLevel,
            int? classId,
            int sacramentType,
            decimal minYearAvg,
            double minAttendanceRate,
            bool requireSacramentExam,
            decimal minSacramentScore,
            long? batchId)
        {
            var vm = await EvaluateCandidatesAsync(
                academicYearId, gradeLevel, classId, sacramentType,
                minYearAvg, minAttendanceRate, requireSacramentExam, minSacramentScore, batchId);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("DanhSachXetDuyetBiTich");

            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;

            ws.Cell("A1").Value = "GIÁO PHẬN SÀI GÒN - GIÁO XỨ PHÚ TRUNG";
            ws.Cell("A1").Style.Font.Bold = true;

            ws.Cell("A2").Value = $"BIÊN BẢN XÉT DUYỆT ĐIỀU KIỆN LÃNH NHẬN {vm.SacramentName.ToUpperInvariant()}";
            ws.Cell("A2").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.FontSize = 14;
            ws.Range("A2:M2").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell("A3").Value = $"Niên khóa: {vm.AcademicYearName} | Khối: {vm.SelectedGrade} | Ngày Lễ dự kiến: {vm.PlannedCeremonyDate:dd/MM/yyyy} | Đủ điều kiện: {vm.TotalFinalApproved}/{vm.TotalCandidates} em";
            ws.Range("A3:M3").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            string[] headers =
            {
                "STT", "Mã Thiếu Nhi", "Tên Thánh", "Họ và Tên", "Ngày Sinh", "Lớp",
                "ĐTB Năm", "Thi Khảo Hạch", "% Lễ CN", "% Học GL", "% TB Chuyên Cần",
                "Người Đỡ Đầu", "Kết Quả Xét Duyệt"
            };

            int headerRow = 5;
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(224, 231, 255);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int rowIdx = headerRow + 1;
            int stt = 1;

            foreach (var c in vm.Candidates)
            {
                ws.Cell(rowIdx, 1).Value = stt++;
                ws.Cell(rowIdx, 2).Value = SanitizeExcelValue(c.StudentCode);
                ws.Cell(rowIdx, 3).Value = SanitizeExcelValue(c.ChristianName);
                ws.Cell(rowIdx, 4).Value = SanitizeExcelValue(c.FullName);
                ws.Cell(rowIdx, 5).Value = c.DateOfBirth.ToString("dd/MM/yyyy");
                ws.Cell(rowIdx, 6).Value = SanitizeExcelValue(c.ClassName);
                ws.Cell(rowIdx, 7).Value = c.YearAverage.HasValue ? c.YearAverage.Value.ToString("0.00") : "--";
                ws.Cell(rowIdx, 8).Value = c.SacramentExamScore.HasValue ? c.SacramentExamScore.Value.ToString("0.00") : "--";
                ws.Cell(rowIdx, 9).Value = $"{c.MassAttendanceRate:0.0}%";
                ws.Cell(rowIdx, 10).Value = $"{c.ClassAttendanceRate:0.0}%";
                ws.Cell(rowIdx, 11).Value = $"{c.CombinedAttendanceRate:0.0}%";
                ws.Cell(rowIdx, 12).Value = SanitizeExcelValue(c.GodparentName);

                string statusLabel = c.AlreadyReceivedSacrament
                    ? "Đã nhận trước đây"
                    : (c.IsApprovedForSacrament
                        ? (c.IsSpecialExemption ? "ĐẠT (Đặc cách)" : "ĐỦ ĐIỀU KIỆN")
                        : $"CHƯA ĐẠT ({c.EvaluationReason})");

                ws.Cell(rowIdx, 13).Value = SanitizeExcelValue(statusLabel);
                ws.Cell(rowIdx, 13).Style.Font.Bold = true;
                ws.Cell(rowIdx, 13).Style.Font.FontColor = c.IsApprovedForSacrament ? XLColor.Green : XLColor.Red;

                for (int col = 1; col <= 13; col++)
                {
                    ws.Cell(rowIdx, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    if (col != 4 && col != 12 && col != 13)
                    {
                        ws.Cell(rowIdx, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }
                rowIdx++;
            }

            rowIdx += 2;
            ws.Cell(rowIdx, 3).Value = "TRƯỞNG BAN ĐIỀU HÀNH GIÁO LÝ";
            ws.Cell(rowIdx, 3).Style.Font.Bold = true;
            ws.Cell(rowIdx, 10).Value = "LINH MỤC CHÁNH XỨ / TUYÊN ÚY PHÊ DUYỆT";
            ws.Cell(rowIdx, 10).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        private static string SanitizeExcelValue(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var trimmed = text.Trim();
            if (trimmed.Length > 0 && (trimmed[0] == '=' || trimmed[0] == '+' || trimmed[0] == '-' || trimmed[0] == '@' || trimmed[0] == '\t' || trimmed[0] == '\r'))
            {
                return "'" + trimmed;
            }
            return trimmed;
        }
    }
}