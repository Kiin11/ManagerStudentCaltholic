using System.Globalization;
using System.Security.Claims;
using System.Text;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class ClassEvaluationService : IClassEvaluationService
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeCalcService;
        private readonly ILogger<ClassEvaluationService> _logger;

        public ClassEvaluationService(
            ParishDbContext context,
            IGradeCalculationService gradeCalcService,
            ILogger<ClassEvaluationService> logger)
        {
            _context = context;
            _gradeCalcService = gradeCalcService;
            _logger = logger;
        }

        /// <summary>
        /// Nhận diện thông minh loại lớp để trả về cấu hình nút xét duyệt tương ứng:
        /// - Rước Lễ 2 (RL2) -> Xét Bí Tích Rước Lễ Lần Đầu (SacramentType = 2)
        /// - Thêm Sức 3 (TS3) -> Xét Bí Tích Thêm Sức (SacramentType = 3)
        /// - Bao Đồng 3 (BĐ3) / Dự Bị 4 (DB4) -> Xét Tuyên Hứa (SacramentType = 4)
        /// - Các lớp còn lại -> Xét Lên Lớp (SacramentType = null)
        /// </summary>
        public ClassEvaluationActionDescriptor ResolveClassEvaluationAction(string? className, string? gradeLevel)
        {
            var cleanName = RemoveDiacritics(className ?? string.Empty).Trim().ToUpperInvariant();
            var cleanGrade = RemoveDiacritics(gradeLevel ?? string.Empty).Trim().ToUpperInvariant();

            if (cleanName.Contains("RUOC LE 2") || cleanName.StartsWith("RL2") || cleanName.Contains("RL 2"))
            {
                return new ClassEvaluationActionDescriptor
                {
                    ActionType = "SACRAMENT_RL2",
                    ButtonLabel = "Xét Bí Tích Rước Lễ Lần Đầu",
                    ButtonCssClass = "btn-warning text-dark fw-bold",
                    IconClass = "bi-brightness-high-fill",
                    ModalTitle = "Xét Duyệt Điều Kiện Xưng Tội & Rước Lễ Lần Đầu (RL2)",
                    TargetDescription = "Tổng hợp ĐTB cả năm, điểm Khảo hạch, Chuyên cần và Chiến dịch đi lễ 1 tháng để lập danh sách đệ trình Cha Xứ.",
                    SacramentType = 2,
                    RequireCampaignCheck = true
                };
            }

            if (cleanName.Contains("THEM SUC 3") || cleanName.StartsWith("TS3") || cleanName.Contains("TS 3"))
            {
                return new ClassEvaluationActionDescriptor
                {
                    ActionType = "SACRAMENT_TS3",
                    ButtonLabel = "Xét Bí Tích Thêm Sức",
                    ButtonCssClass = "btn-danger fw-bold",
                    IconClass = "bi-fire",
                    ModalTitle = "Xét Duyệt Điều Kiện Lãnh Bí Tích Thêm Sức (TS3)",
                    TargetDescription = "Tổng hợp ĐTB cả năm, điểm Khảo hạch Giáo lý, Chuyên cần và Chiến dịch đi lễ 1 tháng để trình Ban Điều Hành & Cha Xứ.",
                    SacramentType = 3,
                    RequireCampaignCheck = true
                };
            }

            if (cleanName.Contains("BAO DONG 3") || cleanName.StartsWith("BD3") || cleanName.Contains("BD 3") ||
                cleanName.Contains("DU BI 4") || cleanName.StartsWith("DB4") || cleanName.Contains("DB 4") ||
                cleanGrade.Contains("DU BI"))
            {
                return new ClassEvaluationActionDescriptor
                {
                    ActionType = "PROMISE_BD3_DB4",
                    ButtonLabel = "Xét Tuyên Hứa Sống Đạo",
                    ButtonCssClass = "btn-info text-dark fw-bold",
                    IconClass = "bi-award-fill",
                    ModalTitle = "Xét Duyệt Danh Sách Tuyên Hứa (Bao Đồng 3 / Dự Bị 4)",
                    TargetDescription = "Đánh giá kết quả học tập, chuyên cần và chiến dịch đi lễ để lập Tờ trình Tuyên hứa Sống đạo.",
                    SacramentType = 4,
                    RequireCampaignCheck = true
                };
            }

            return new ClassEvaluationActionDescriptor
            {
                ActionType = "PROMOTION_NORMAL",
                ButtonLabel = "Xét Lên Lớp",
                ButtonCssClass = "btn-primary fw-bold",
                IconClass = "bi-mortarboard-fill",
                ModalTitle = "Xét Duyệt Lên Lớp Cuối Năm Học",
                TargetDescription = "Đánh giá học lực và chuyên cần cả năm để đề xuất lên lớp hoặc lưu ban cho niên khóa tới.",
                SacramentType = null,
                RequireCampaignCheck = false
            };
        }

        public async Task<bool> CanUserEvaluateClassAsync(ClaimsPrincipal user, int classId)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
                return false;

            if (user.IsInRole(UserRole.Admin) ||
                user.IsInRole(UserRole.SpiritualDirector) ||
                user.IsInRole(UserRole.ExecutiveBoard))
            {
                return true;
            }

            var classRoom = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (classRoom == null)
                return false;

            if (user.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = user.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrWhiteSpace(managedGrade) &&
                    string.Equals(managedGrade.Trim(), classRoom.GradeLevel.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                return await _context.ClassTeachers
                    .AsNoTracking()
                    .AnyAsync(ct => ct.ClassRoomId == classId && ct.UserId == userId);
            }

            return false;
        }

        public async Task<ClassEvaluationActionDescriptor> GetClassEvaluationHeaderAsync(int classId)
        {
            var classRoom = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (classRoom == null)
                return new ClassEvaluationActionDescriptor();

            var descriptor = ResolveClassEvaluationAction(classRoom.Name, classRoom.GradeLevel);

            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.ClassRoomId == classId && e.AcademicYearId == classRoom.AcademicYearId)
                .Select(e => e.TeacherProposalStatus)
                .ToListAsync();

            descriptor.TotalStudents = enrollments.Count;
            descriptor.SubmittedCount = enrollments.Count(s => !string.IsNullOrEmpty(s) && s != "PENDING");

            if (descriptor.SacramentType.HasValue)
            {
                var batch = await _context.SacramentApprovalBatches
                    .AsNoTracking()
                    .Where(b => b.AcademicYearId == classRoom.AcademicYearId
                             && b.ClassRoomId == classRoom.Id
                             && b.SacramentType == descriptor.SacramentType.Value)
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();

                if (batch != null)
                {
                    descriptor.LinkedApprovalBatchId = batch.Id;
                    descriptor.LinkedBatchStatus = batch.Status;
                }
            }

            return descriptor;
        }

        public async Task<ClassEvaluationSheetViewModel> BuildEvaluationSheetAsync(int classId)
        {
            var classRoom = await _context.Classes
                .AsNoTracking()
                .Include(c => c.AcademicYear)
                .FirstOrDefaultAsync(c => c.Id == classId)
                ?? throw new KeyNotFoundException("Không tìm thấy thông tin lớp học.");

            var descriptor = await GetClassEvaluationHeaderAsync(classId);

            var vm = new ClassEvaluationSheetViewModel
            {
                ClassId = classRoom.Id,
                ClassName = classRoom.Name,
                GradeLevel = classRoom.GradeLevel,
                AcademicYearId = classRoom.AcademicYearId,
                AcademicYearName = classRoom.AcademicYear?.Name ?? string.Empty,
                Descriptor = descriptor,
                MinYearAverage = 5.0m,
                MinAttendanceRate = 80.0,
                MinSacramentExamScore = 5.0m
            };

            // Lấy cấu hình ngưỡng xét lên lớp nếu có trong PromotionRuleConfigs (FEAT-P3-01)
            var ruleConfig = await _context.Set<PromotionRuleConfig>()
                .AsNoTracking()
                .Where(r => r.AcademicYearId == classRoom.AcademicYearId
                         && (r.GradeLevel == classRoom.GradeLevel || r.GradeLevel == "ALL"))
                .OrderByDescending(r => r.GradeLevel == classRoom.GradeLevel)
                .FirstOrDefaultAsync();

            if (ruleConfig != null)
            {
                vm.MinYearAverage = ruleConfig.MinYearAverage;
                vm.MinAttendanceRate = ruleConfig.MinAttendanceRate;
            }

            // Kiểm tra Chiến dịch đi lễ 1 tháng (FEAT-P4-01) nếu là lớp Bí tích / Tuyên hứa
            MassCampaign? activeCampaign = null;
            Dictionary<long, int> campaignAttendedByEnrollment = new();

            if (descriptor.RequireCampaignCheck)
            {
                activeCampaign = await _context.MassCampaigns
                    .AsNoTracking()
                    .Where(mc => mc.ClassRoomId == classRoom.Id && mc.AcademicYearId == classRoom.AcademicYearId)
                    .OrderByDescending(mc => mc.Status == "ACTIVE")
                    .ThenByDescending(mc => mc.StartDate)
                    .FirstOrDefaultAsync();

                if (activeCampaign != null)
                {
                    vm.HasActiveMassCampaign = true;
                    vm.MassCampaignTitle = activeCampaign.Title;
                    vm.MassCampaignTargetDays = activeCampaign.TargetDaysCount;

                    campaignAttendedByEnrollment = await _context.MassCampaignRecords
                        .AsNoTracking()
                        .Where(r => r.CampaignId == activeCampaign.Id
                                 && (r.Status == "PRESENT" || r.Status == "LATE"))
                        .GroupBy(r => r.EnrollmentId)
                        .Select(g => new { EnrollmentId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.EnrollmentId, x => x.Count);
                }
            }

            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Include(e => e.GradeRecords)
                    .ThenInclude(gr => gr.GradeConfiguration)
                .Include(e => e.Attendances)
                .Where(e => e.ClassRoomId == classRoom.Id
                         && e.AcademicYearId == classRoom.AcademicYearId
                         && e.Student != null
                         && !e.Student.IsDeleted)
                .OrderBy(e => e.Student!.StudentCode)
                .ToListAsync();

            var configs = await _context.GradeConfigurations
                .AsNoTracking()
                .Where(gc => gc.AcademicYearId == classRoom.AcademicYearId
                          && (gc.ClassRoomId == classRoom.Id || gc.ClassRoomId == null)
                          && (gc.GradeLevel == classRoom.GradeLevel || gc.GradeLevel == "ALL"))
                .ToListAsync();

            foreach (var env in enrollments)
            {
                var student = env.Student!;
                var sem1Cols = configs.Where(c => c.Semester == 1).ToList();
                var sem2Cols = configs.Where(c => c.Semester == 2).ToList();

                decimal? sem1Avg = CalculateWeightedSemesterAverage(env.GradeRecords, sem1Cols);
                decimal? sem2Avg = CalculateWeightedSemesterAverage(env.GradeRecords, sem2Cols);
                decimal? yearAvg = _gradeCalcService.CalculateYearAverage(sem1Avg, sem2Avg);

                // Điểm thi khảo hạch Bí tích (nếu cột điểm có ScoreType == "SACRAMENT_EXAM" hoặc tên chứa "Khảo hạch")
                var sacramentRecord = env.GradeRecords
                    .Where(gr => gr.GradeConfiguration != null &&
                                (gr.GradeConfiguration.ScoreType == "SACRAMENT_EXAM" ||
                                 gr.GradeConfiguration.DisplayName.Contains("Khảo hạch", StringComparison.OrdinalIgnoreCase) ||
                                 gr.GradeConfiguration.DisplayName.Contains("Giáo lý Bí tích", StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(gr => gr.Id)
                    .FirstOrDefault();

                decimal? sacramentScore = sacramentRecord?.Score;

                // Tính chuyên cần Lễ & Học
                var (massRate, classRate, combinedRate) = CalculateAttendanceRates(env.Attendances);
                var (rank, _) = _gradeCalcService.EvaluateStudent(yearAvg, combinedRate, classRoom.GradeLevel, 0);

                int campaignAttended = campaignAttendedByEnrollment.TryGetValue(env.Id, out int cDays) ? cDays : 0;
                int campaignTarget = activeCampaign?.TargetDaysCount ?? 0;
                bool campaignMet = activeCampaign == null || campaignAttended >= campaignTarget;

                var reasons = new List<string>();
                if (!yearAvg.HasValue || yearAvg.Value < vm.MinYearAverage)
                    reasons.Add($"ĐTB năm < {vm.MinYearAverage:0.0}");

                if (combinedRate < vm.MinAttendanceRate)
                    reasons.Add($"Chuyên cần < {vm.MinAttendanceRate:0}%");

                if (descriptor.SacramentType.HasValue && sacramentScore.HasValue && sacramentScore.Value < vm.MinSacramentExamScore)
                    reasons.Add($"Khảo hạch < {vm.MinSacramentExamScore:0.0}");

                if (descriptor.RequireCampaignCheck && activeCampaign != null && !campaignMet)
                    reasons.Add($"Chiến dịch lễ ({campaignAttended}/{campaignTarget} ngày)");

                bool isAutoEligible = reasons.Count == 0;

                string defaultProposal = env.TeacherProposalStatus;
                if (string.IsNullOrWhiteSpace(defaultProposal) || defaultProposal == "PENDING")
                {
                    defaultProposal = isAutoEligible ? "PROPOSED_PASS" : "PROPOSED_RETAIN";
                }

                vm.Students.Add(new ClassEvaluationStudentItemDto
                {
                    EnrollmentId = env.Id,
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    ChristianName = student.ChristianName ?? string.Empty,
                    FullName = student.FirstName + " " + student.LastName,
                    Semester1Avg = sem1Avg,
                    Semester2Avg = sem2Avg,
                    YearAvg = yearAvg,
                    SacramentExamScore = sacramentScore,
                    MassAttendanceRate = massRate,
                    ClassAttendanceRate = classRate,
                    CombinedAttendanceRate = combinedRate,
                    CampaignAttendedDays = campaignAttended,
                    CampaignTargetDays = campaignTarget,
                    IsCampaignTargetMet = campaignMet,
                    AcademicRank = rank,
                    IsAutoEligible = isAutoEligible,
                    AutoIneligibleReason = isAutoEligible ? "Đạt đủ tiêu chuẩn" : string.Join("; ", reasons),
                    ProposalStatus = defaultProposal,
                    ProposalNote = env.TeacherProposalNote,
                    GodparentName = env.ProposedGodparentName
                });
            }

            return vm;
        }

        public async Task<(bool Success, string Message, long? BatchId)> SubmitClassEvaluationAsync(
            SubmitClassEvaluationRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress)
        {
            if (!await CanUserEvaluateClassAsync(user, dto.ClassId))
                return (false, "Bạn không có quyền thực hiện xét duyệt trên lớp này.", null);

            var sheet = await BuildEvaluationSheetAsync(dto.ClassId);
            var descriptor = sheet.Descriptor;
            var operatorName = user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "Giáo lý viên";

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var enrollments = await _context.Enrollments
                    .Where(e => e.ClassRoomId == dto.ClassId && e.AcademicYearId == sheet.AcademicYearId)
                    .ToDictionaryAsync(e => e.Id);

                var itemMap = dto.Items.ToDictionary(i => i.EnrollmentId);
                int passCount = 0;
                int specialCount = 0;
                int retainCount = 0;

                foreach (var studentRow in sheet.Students)
                {
                    if (!enrollments.TryGetValue(studentRow.EnrollmentId, out var env))
                        continue;

                    string status = studentRow.IsAutoEligible ? "PROPOSED_PASS" : "PROPOSED_RETAIN";
                    string? note = null;
                    string? godparent = null;

                    if (itemMap.TryGetValue(env.Id, out var submitted))
                    {
                        var normalized = (submitted.ProposalStatus ?? string.Empty).Trim().ToUpperInvariant();
                        if (normalized == "PROPOSED_PASS" || normalized == "PROPOSED_SPECIAL" || normalized == "PROPOSED_RETAIN")
                        {
                            status = normalized;
                        }
                        note = string.IsNullOrWhiteSpace(submitted.ProposalNote) ? null : submitted.ProposalNote.Trim();
                        godparent = string.IsNullOrWhiteSpace(submitted.GodparentName) ? null : submitted.GodparentName.Trim();
                    }

                    // Nếu học sinh chưa đủ điều kiện tự động nhưng GLV chọn cho qua -> bắt buộc chuyển thành PROPOSED_SPECIAL (Đặc cách)
                    if (!studentRow.IsAutoEligible && status == "PROPOSED_PASS")
                    {
                        status = "PROPOSED_SPECIAL";
                    }

                    env.EvaluationActionType = descriptor.ActionType;
                    env.TeacherProposalStatus = status;
                    env.TeacherProposalNote = note;
                    env.ProposedGodparentName = godparent;
                    env.ProposedBy = operatorName;
                    env.ProposedAt = DateTime.UtcNow;
                    env.IsPassed = (status == "PROPOSED_PASS" || status == "PROPOSED_SPECIAL");

                    if (status == "PROPOSED_PASS") passCount++;
                    else if (status == "PROPOSED_SPECIAL") specialCount++;
                    else retainCount++;
                }

                long? syncedBatchId = null;

                // Nếu là lớp xét Bí tích (RL2, TS3) hoặc Xét Tuyên hứa (BĐ3, DB4) -> Đồng bộ thẳng sang Tờ trình SacramentApprovalBatches (FEAT-P3-04)
                if (descriptor.SacramentType.HasValue)
                {
                    var existingBatch = await _context.SacramentApprovalBatches
                        .Include(b => b.Items)
                        .FirstOrDefaultAsync(b => b.AcademicYearId == sheet.AcademicYearId
                                               && b.ClassRoomId == sheet.ClassId
                                               && b.SacramentType == descriptor.SacramentType.Value
                                               && b.Status == "DRAFT");

                    if (existingBatch == null)
                    {
                        existingBatch = new SacramentApprovalBatch
                        {
                            AcademicYearId = sheet.AcademicYearId,
                            GradeLevel = sheet.GradeLevel,
                            ClassRoomId = sheet.ClassId,
                            SacramentType = descriptor.SacramentType.Value,
                            Title = $"{descriptor.ButtonLabel} - Lớp {sheet.ClassName} ({sheet.AcademicYearName})",
                            MinYearAverage = sheet.MinYearAverage,
                            MinAttendanceRate = sheet.MinAttendanceRate,
                            RequireSacramentExam = true,
                            MinSacramentExamScore = sheet.MinSacramentExamScore,
                            PlannedCeremonyDate = DateTime.UtcNow.AddDays(30).Date,
                            Status = "DRAFT",
                            CreatedBy = operatorName,
                            CreatedAt = DateTime.UtcNow,
                            Notes = $"Đề xuất từ Sổ điểm lớp {sheet.ClassName} bởi {operatorName}"
                        };
                        _context.SacramentApprovalBatches.Add(existingBatch);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        existingBatch.Notes = $"Cập nhật từ Sổ điểm lớp {sheet.ClassName} bởi {operatorName} lúc {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";
                        _context.SacramentApprovalItems.RemoveRange(existingBatch.Items);
                    }

                    foreach (var s in sheet.Students)
                    {
                        if (!enrollments.TryGetValue(s.EnrollmentId, out var env))
                            continue;

                        bool isSpecial = env.TeacherProposalStatus == "PROPOSED_SPECIAL";
                        bool isApproved = env.TeacherProposalStatus == "PROPOSED_PASS" || isSpecial;

                        _context.SacramentApprovalItems.Add(new SacramentApprovalItem
                        {
                            BatchId = existingBatch.Id,
                            StudentId = s.StudentId,
                            EnrollmentId = s.EnrollmentId,
                            ClassName = sheet.ClassName,
                            Semester1Average = s.Semester1Avg,
                            Semester2Average = s.Semester2Avg,
                            YearAverage = s.YearAvg,
                            SacramentExamScore = s.SacramentExamScore,
                            MassAttendanceRate = s.MassAttendanceRate,
                            ClassAttendanceRate = s.ClassAttendanceRate,
                            CombinedAttendanceRate = s.CombinedAttendanceRate,
                            //AcademicRank = s.AcademicRank,
                            //IsAutoEligible = s.IsAutoEligible,
                            //IneligibleReason = s.AutoIneligibleReason,
                            //IsSpecialApproved = isSpecial,
                            //SpecialApprovalReason = env.TeacherProposalNote,
                            //IsFinalApproved = isApproved,
                            GodparentName = env.ProposedGodparentName
                        });
                    }

                    syncedBatchId = existingBatch.Id;
                }

                //_context.AuditLogs.Add(new AuditLog
                //{
                //    Username = user.Identity?.Name ?? operatorName,
                //    Action = $"SUBMIT_{descriptor.ActionType}",
                //    Details = $"GLV {operatorName} trình kết quả '{descriptor.ButtonLabel}' cho lớp {sheet.ClassName}: Đạt chuẩn={passCount}, Đề xuất đặc cách={specialCount}, Chưa đạt={retainCount}.",
                //    IpAddress = ipAddress,
                //    Timestamp = DateTime.UtcNow
                //});

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                string summaryMsg = descriptor.SacramentType.HasValue
                    ? $"Đã lưu kết quả {descriptor.ButtonLabel} cho lớp {sheet.ClassName} (Đạt: {passCount}, Đặc cách: {specialCount}, Chưa đạt: {retainCount}) và đồng bộ sang Tờ trình trình Trưởng khối / Cha Xứ."
                    : $"Đã lưu đề xuất Xét lên lớp cho lớp {sheet.ClassName} (Lên lớp: {passCount + specialCount}, Lưu ban: {retainCount}).";

                return (true, summaryMsg, syncedBatchId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi lưu xét duyệt cấp lớp ClassId={ClassId}", dto.ClassId);
                return (false, "Đã xảy ra lỗi khi lưu danh sách xét duyệt. Vui lòng thử lại.", null);
            }
        }

        private static decimal? CalculateWeightedSemesterAverage(
            ICollection<GradeRecord> records,
            List<GradeConfiguration> semesterCols)
        {
            if (semesterCols == null || !semesterCols.Any() || records == null || !records.Any())
                return null;

            decimal totalWeighted = 0m;
            int totalWeights = 0;

            foreach (var col in semesterCols)
            {
                var rec = records.FirstOrDefault(r => r.GradeConfigurationId == col.Id);
                if (rec?.Score != null)
                {
                    int weight = Math.Max(1, col.WeightFactor);
                    totalWeighted += rec.Score.Value * weight;
                    totalWeights += weight;
                }
            }

            if (totalWeights == 0) return null;
            return Math.Round(totalWeighted / totalWeights, 2);
        }

        private static (double MassRate, double ClassRate, double CombinedRate) CalculateAttendanceRates(
            ICollection<Attendance> attendances)
        {
            if (attendances == null || !attendances.Any())
                return (100.0, 100.0, 100.0);

            var sundayOrThuMass = attendances.ToList();
            double massPoints = 0;
            foreach (var a in sundayOrThuMass)
            {
                if (a.IsMakeUp || a.AttendedMass)
                {
                    massPoints += a.MassStatus == AttendanceStatus.Late ? 0.8 : 1.0;
                }
                else if (a.MassStatus == AttendanceStatus.AbsentPermitted)
                {
                    massPoints += 0.5;
                }
            }

            var classSessions = attendances.Where(a => a.AttendanceDate.DayOfWeek == DayOfWeek.Sunday).ToList();
            double classPoints = 0;
            foreach (var a in classSessions)
            {
                if (a.IsMakeUp || a.ClassAttended)
                {
                    classPoints += a.ClassStatus == AttendanceStatus.Late ? 0.8 : 1.0;
                }
                else if (a.ClassStatus == AttendanceStatus.AbsentPermitted)
                {
                    classPoints += 0.5;
                }
            }

            double massRate = sundayOrThuMass.Count > 0
                ? Math.Round(massPoints * 100.0 / sundayOrThuMass.Count, 1)
                : 100.0;

            double classRate = classSessions.Count > 0
                ? Math.Round(classPoints * 100.0 / classSessions.Count, 1)
                : 100.0;

            double combined = Math.Round((massRate + classRate) / 2.0, 1);
            return (massRate, classRate, combined);
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }
    }
}