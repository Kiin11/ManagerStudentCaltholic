using ManagerStudentCaltholic.Controllers;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ManagerStudentCaltholic.Services
{
    public class PromotionEvaluationService : IPromotionEvaluationService
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;
        private readonly IStudentPlacementService _placementService;

        public PromotionEvaluationService(
            ParishDbContext context,
            IGradeCalculationService gradeService,
            IStudentPlacementService placementService)
        {
            _context = context;
            _gradeService = gradeService;
            _placementService = placementService;
        }

        /// <summary>
        /// Lấy quy tắc xét lên lớp theo Khối; nếu chưa cấu hình riêng cho Khối thì lấy cấu hình "ALL" hoặc mặc định chuẩn
        /// </summary>
        public async Task<PromotionRuleConfig> GetEffectiveRuleAsync(int academicYearId, string gradeLevel)
        {
            var gradeRule = await _context.PromotionRuleConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.AcademicYearId == academicYearId && r.GradeLevel == gradeLevel);

            if (gradeRule != null) return gradeRule;

            var globalRule = await _context.PromotionRuleConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.AcademicYearId == academicYearId && r.GradeLevel == "ALL");

            if (globalRule != null) return globalRule;

            return new PromotionRuleConfig
            {
                AcademicYearId = academicYearId,
                GradeLevel = gradeLevel,
                MinYearAverage = 5.00m,
                MinAttendanceRate = 80.0,
                RequireSacramentPassed = true,
                MinSacramentScore = 5.00m
            };
        }

        /// <summary>
        /// Sơ đồ thăng tiến tự động của các lớp Giáo lý (VD: Khai Tâm 1 -> Khai Tâm 2, Khai Tâm 3 -> Rước Lễ 1)
        /// </summary>
        public (string NextGradeLevel, string NextClassPrefix, bool IsGraduating) ResolveProgressionTarget(
            string currentClassName,
            string currentGradeLevel,
            string? configuredNextGrade)
        {
            var normalizedName = currentClassName.Trim();

            // Bắt số cấp lớp (1, 2, 3, 4) trong tên lớp (VD: "Khai Tâm 1A" -> levelNum = 1)
            var match = Regex.Match(normalizedName, @"(\d+)");
            int levelNum = match.Success && int.TryParse(match.Groups[1].Value, out var n) ? n : 1;

            string grade = currentGradeLevel.Trim();

            // Quy tắc chuyển cấp chuẩn giữa các khối Giáo lý Công giáo
            if (grade.Contains("Khai Tâm", StringComparison.OrdinalIgnoreCase))
            {
                if (levelNum < 2)
                    return (grade, $"Khai Tâm {levelNum + 1}", false);
                return (configuredNextGrade ?? "Rước Lễ", "Rước Lễ 1", false);
            }

            if (grade.Contains("Rước Lễ", StringComparison.OrdinalIgnoreCase))
            {
                if (levelNum < 3)
                    return (grade, $"Rước Lễ {levelNum + 1}", false);
                return (configuredNextGrade ?? "Thêm Sức", "Thêm Sức 1", false);
            }

            if (grade.Contains("Thêm Sức", StringComparison.OrdinalIgnoreCase))
            {
                if (levelNum < 3)
                    return (grade, $"Thêm Sức {levelNum + 1}", false);
                return (configuredNextGrade ?? "Bao Đồng", "Bao Đồng 1", false);
            }

            if (grade.Contains("Bao Đồng", StringComparison.OrdinalIgnoreCase))
            {
                if (levelNum < 4)
                    return (grade, $"Bao Đồng {levelNum + 1}", false);
                // Bao Đồng năm cuối -> Hoàn thành chương trình / Vào Đời
                return ("Vào Đời", "Vào Đời", true);
            }

            return (configuredNextGrade ?? grade, normalizedName, false);
        }

        /// <summary>
        /// Đánh giá toàn bộ học sinh trong lớp nguồn để xét lên lớp hoặc ở lại lớp (TASK-P3-01.2)
        /// </summary>
        public async Task<List<StudentPromotionEvalItem>> EvaluateClassEligibilityAsync(int sourceClassId, int targetYearId)
        {
            var sourceClass = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == sourceClassId);

            if (sourceClass == null) return new List<StudentPromotionEvalItem>();

            var rule = await GetEffectiveRuleAsync(sourceClass.AcademicYearId, sourceClass.GradeLevel);
            var (nextGrade, nextClassPrefix, isGraduating) = ResolveProgressionTarget(
                sourceClass.Name, sourceClass.GradeLevel, rule.NextGradeLevel);

            // Lấy cấu hình cột điểm của lớp trong niên khóa nguồn
            var allYearConfigs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == sourceClass.AcademicYearId &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == sourceClass.GradeLevel) &&
                            (g.ClassRoomId == null || g.ClassRoomId == sourceClass.Id))
                .AsNoTracking()
                .ToListAsync();

            var hk1Configs = allYearConfigs.Where(g => g.Semester == 1).ToList();
            var hk2Configs = allYearConfigs.Where(g => g.Semester == 2).ToList();
            var sacramentConfigs = allYearConfigs.Where(g => g.IsSacramentExam).ToList();

            // Lấy danh sách học sinh trong lớp nguồn
            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.GradeRecords)
                .Where(e => e.ClassRoomId == sourceClassId && e.Student.IsActive)
                .OrderBy(e => e.Student.LastName)
                .ThenBy(e => e.Student.FirstName)
                .AsNoTracking()
                .ToListAsync();

            if (!enrollments.Any()) return new List<StudentPromotionEvalItem>();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();
            var studentIds = enrollments.Select(e => e.StudentId).ToList();

            // Lấy dữ liệu điểm danh của lớp
            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            var totalSessions = Math.Max(1, attendances.Select(a => a.AttendanceDate.Date).Distinct().Count());

            // Kiểm tra những học sinh nào đã được xếp lớp ở niên khóa mới (tránh kết chuyển trùng)
            var existingTargetEnrollments = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .Where(e => studentIds.Contains(e.StudentId) && e.ClassRoom.AcademicYearId == targetYearId)
                .AsNoTracking()
                .ToDictionaryAsync(e => e.StudentId, e => e.ClassRoom.Name);

            // Lấy danh sách lớp ở niên khóa đích để gợi ý lớp học lại hoặc lớp lên thẳng
            var targetYearClasses = await _context.Classes
                .Where(c => c.AcademicYearId == targetYearId)
                .AsNoTracking()
                .ToListAsync();

            var result = new List<StudentPromotionEvalItem>();
            var promotedStudentIds = new List<long>();

            foreach (var e in enrollments)
            {
                var hk1Dict = e.GradeRecords
                    .Where(r => hk1Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var hk2Dict = e.GradeRecords
                    .Where(r => hk2Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var sem1Avg = _gradeService.CalculateSemesterAverage(hk1Dict, hk1Configs);
                var sem2Avg = _gradeService.CalculateSemesterAverage(hk2Dict, hk2Configs);
                var yearAvg = _gradeService.CalculateYearAverage(sem1Avg, sem2Avg);

                // Tính % chuyên cần kết hợp (Lễ + Học Giáo lý)
                var studentAtts = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                double attRate = 100.0;
                if (attendances.Any())
                {
                    var classPresent = studentAtts.Count(a => a.ClassAttended && (a.ClassStatus == "PRESENT" || a.ClassStatus == "LATE" || a.ClassStatus == "EXCUSED"));
                    var massPresent = studentAtts.Count(a => a.AttendedMass && (a.MassStatus == "PRESENT" || a.MassStatus == "LATE" || a.MassStatus == "EXCUSED"));
                    double classRate = (classPresent / (double)totalSessions) * 100.0;
                    double massRate = (massPresent / (double)totalSessions) * 100.0;
                    attRate = Math.Round((classRate + massRate) / 2.0, 1);
                }

                // Kiểm tra điểm thi khảo hạch Bí tích (nếu lớp có cột IsSacramentExam)
                decimal? sacramentScore = null;
                bool hasSacramentCol = sacramentConfigs.Any();
                if (hasSacramentCol)
                {
                    var sacScores = e.GradeRecords
                        .Where(r => sacramentConfigs.Select(c => c.Id).Contains(r.GradeConfigurationId) && r.Score.HasValue)
                        .Select(r => r.Score!.Value)
                        .ToList();

                    if (sacScores.Any())
                    {
                        sacramentScore = Math.Round(sacScores.Average(), 2);
                    }
                }

                var (rank, sacramentEligible) = _gradeService.EvaluateStudent(yearAvg, attRate, sourceClass.GradeLevel, 0);

                // Xét điều kiện theo PromotionRuleConfig
                var failReasons = new List<string>();
                if (!yearAvg.HasValue || yearAvg.Value < rule.MinYearAverage)
                {
                    failReasons.Add($"ĐTB năm ({(yearAvg?.ToString("0.00") ?? "Chưa có")}) < {rule.MinYearAverage:0.0}");
                }
                if (attRate < rule.MinAttendanceRate)
                {
                    failReasons.Add($"Chuyên cần ({attRate:0.0}%) < {rule.MinAttendanceRate:0.#}%");
                }
                if (rule.RequireSacramentPassed && hasSacramentCol)
                {
                    if (!sacramentScore.HasValue || sacramentScore.Value < rule.MinSacramentScore)
                    {
                        failReasons.Add($"Khảo hạch Bí tích ({(sacramentScore?.ToString("0.00") ?? "Chưa thi")}) < {rule.MinSacramentScore:0.0}");
                    }
                }

                bool isPromoted = failReasons.Count == 0;
                bool alreadyRolled = existingTargetEnrollments.TryGetValue(e.StudentId, out var existingClassName);

                string suggestedAction = isPromoted
                    ? (isGraduating ? "GRADUATE" : "PROMOTE")
                    : "RETAIN";

                // Nếu ở lại lớp: gợi ý lớp cùng cấp ở niên khóa mới
                int? retainClassId = null;
                if (!isPromoted)
                {
                    var sameLevelClass = targetYearClasses
                        .FirstOrDefault(c => c.Name.Equals(sourceClass.Name, StringComparison.OrdinalIgnoreCase))
                        ?? targetYearClasses.FirstOrDefault(c => c.GradeLevel == sourceClass.GradeLevel);
                    retainClassId = sameLevelClass?.Id;
                }
                else if (!alreadyRolled && !isGraduating)
                {
                    promotedStudentIds.Add(e.StudentId);
                }

                result.Add(new StudentPromotionEvalItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    Gender = e.Student.Gender ?? "Nam",
                    DateOfBirth = e.Student.DateOfBirth,
                    Semester1Average = sem1Avg,
                    Semester2Average = sem2Avg,
                    YearAverage = yearAvg,
                    AttendanceRate = attRate,
                    SacramentExamScore = sacramentScore,
                    HasSacramentExamColumn = hasSacramentCol,
                    AcademicRank = rank,
                    IsSacramentEligible = sacramentEligible,
                    IsPromoted = isPromoted,
                    SuggestedAction = suggestedAction,
                    SuggestedTargetClassId = retainClassId,
                    EvaluationReason = isPromoted
                        ? "Đủ điều kiện lên lớp"
                        : string.Join("; ", failReasons),
                    IsAlreadyRolledOver = alreadyRolled,
                    ExistingTargetClassName = existingClassName
                });
            }

            // Gọi thuật toán Smart Class Balancing (TASK-P3-01.3) để gợi ý phân bổ đều lớp đích cho học sinh được lên lớp
            if (promotedStudentIds.Any() && targetYearId > 0)
            {
                var balancedAssignments = await _placementService.BatchAssignToTargetClassesAsync(
                    promotedStudentIds, targetYearId, nextGrade, nextClassPrefix);

                foreach (var item in result.Where(r => r.IsPromoted && !r.IsAlreadyRolledOver))
                {
                    if (balancedAssignments.TryGetValue(item.StudentId, out var assignedClassId))
                    {
                        item.SuggestedTargetClassId = assignedClassId;
                    }
                }
            }

            return result;
        }
    }
}