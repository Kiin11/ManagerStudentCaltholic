using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard}")]
    public class PromotionController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;
        private readonly ILogger<PromotionController> _logger;

        public PromotionController(
            ParishDbContext context,
            IGradeCalculationService gradeService,
            ILogger<PromotionController> logger)
        {
            _context = context;
            _gradeService = gradeService;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /Promotion/Index (Màn hình Xét lên lớp & Kết chuyển Niên khóa - Epic 11)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            int? sourceYearId,
            int? sourceClassId,
            int? targetYearId,
            decimal minAvg = 5.0m,
            double minAtt = 60.0)
        {
            var allYears = await _context.AcademicYears
                .OrderByDescending(y => y.StartDate)
                .AsNoTracking()
                .ToListAsync();

            if (!allYears.Any())
            {
                TempData["ErrorMessage"] = "Chưa có niên khóa nào trong hệ thống.";
                return RedirectToAction("Index", "AcademicYears");
            }

            // Mặc định Niên khóa nguồn là niên khóa đang Active (hoặc niên khóa liền trước)
            var currentYear = allYears.FirstOrDefault(y => y.IsCurrent) ?? allYears.First();
            int selectedSourceYearId = sourceYearId ?? currentYear.Id;
            var sourceYear = allYears.FirstOrDefault(y => y.Id == selectedSourceYearId) ?? currentYear;

            // Mặc định Niên khóa đích là niên khóa mới nhất khác niên khóa nguồn
            var defaultTargetYear = allYears.FirstOrDefault(y => y.Id != sourceYear.Id && y.StartDate >= sourceYear.StartDate)
                                 ?? allYears.FirstOrDefault(y => y.Id != sourceYear.Id)
                                 ?? sourceYear;
            int selectedTargetYearId = targetYearId ?? defaultTargetYear.Id;
            var targetYear = allYears.FirstOrDefault(y => y.Id == selectedTargetYearId) ?? defaultTargetYear;

            ViewBag.AllYears = allYears;

            // Danh sách lớp của niên khóa nguồn
            var sourceClasses = await _context.Classes
                .Where(c => c.AcademicYearId == sourceYear.Id)
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.SourceClasses = sourceClasses;

            // Danh sách lớp của niên khóa đích
            var targetClasses = await _context.Classes
                .Where(c => c.AcademicYearId == targetYear.Id)
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            if (!sourceClasses.Any())
            {
                return View(new PromotionPreviewViewModel
                {
                    SourceYearId = sourceYear.Id,
                    SourceYearName = sourceYear.Name,
                    TargetYearId = targetYear.Id,
                    TargetYearName = targetYear.Name,
                    MinYearAverage = minAvg,
                    MinAttendanceRate = minAtt,
                    TargetYearClasses = targetClasses
                });
            }

            var selectedSourceClass = sourceClassId.HasValue
                ? sourceClasses.FirstOrDefault(c => c.Id == sourceClassId.Value) ?? sourceClasses.First()
                : sourceClasses.First();

            // Lấy cấu hình điểm của lớp nguồn
            var configs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == sourceYear.Id &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == selectedSourceClass.GradeLevel) &&
                            (g.ClassRoomId == null || g.ClassRoomId == selectedSourceClass.Id))
                .AsNoTracking()
                .ToListAsync();

            var hk1Configs = configs.Where(c => c.Semester == 1).ToList();
            var hk2Configs = configs.Where(c => c.Semester == 2).ToList();

            // Lấy học sinh trong lớp nguồn
            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.GradeRecords)
                .Where(e => e.ClassRoomId == selectedSourceClass.Id && e.Student.IsActive)
                .OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();
            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            var totalSessions = Math.Max(1, attendances.Select(a => a.AttendanceDate).Distinct().Count());

            // Kiểm tra những học sinh nào đã được xếp lớp ở niên khóa đích rồi
            var studentIds = enrollments.Select(e => e.StudentId).ToList();
            var targetEnrollments = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .Where(e => e.ClassRoom.AcademicYearId == targetYear.Id && studentIds.Contains(e.StudentId))
                .AsNoTracking()
                .ToDictionaryAsync(e => e.StudentId, e => e.ClassRoom.Name);

            var rows = enrollments.Select(e =>
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

                var attList = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                var presentCount = attList.Count(a => a.ClassAttended && (a.ClassStatus == "PRESENT" || a.ClassStatus == "LATE"));
                var attRate = attendances.Any()
                    ? Math.Round((presentCount / (double)totalSessions) * 100, 1)
                    : 100.0;

                var (rank, _) = _gradeService.EvaluateStudent(yearAvg, attRate, selectedSourceClass.GradeLevel, 0);

                // Xét điều kiện Lên lớp dựa theo ngưỡng cấu hình
                bool isPromoted = (yearAvg ?? 0m) >= minAvg && attRate >= minAtt;
                string note = isPromoted
                    ? "Đủ điều kiện lên lớp"
                    : ((yearAvg ?? 0m) < minAvg ? $"ĐTB Năm < {minAvg:0.0}" : $"Chuyên cần < {minAtt}%");

                targetEnrollments.TryGetValue(e.StudentId, out var existingTargetClass);

                return new StudentPromotionRowDto
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    DateOfBirth = e.Student.DateOfBirth,
                    HK1Average = sem1Avg,
                    HK2Average = sem2Avg,
                    YearAverage = yearAvg,
                    AttendanceRate = attRate,
                    AcademicRank = rank,
                    IsEligibleForSacrament = false, // Tạm khóa theo yêu cầu
                    IsPromoted = isPromoted,
                    PromotionNote = note,
                    AlreadyEnrolledInTargetYear = existingTargetClass != null,
                    ExistingTargetClassName = existingTargetClass
                };
            }).ToList();

            // Tự động gợi ý lớp ở lại (cùng tên ở niên khóa mới)
            var suggestedRetain = targetClasses.FirstOrDefault(c =>
                c.Name.Equals(selectedSourceClass.Name, StringComparison.OrdinalIgnoreCase));

            var vm = new PromotionPreviewViewModel
            {
                SourceYearId = sourceYear.Id,
                SourceYearName = sourceYear.Name,
                SourceClassId = selectedSourceClass.Id,
                SourceClassName = selectedSourceClass.Name,
                SourceGradeLevel = selectedSourceClass.GradeLevel,
                TargetYearId = targetYear.Id,
                TargetYearName = targetYear.Name,
                MinYearAverage = minAvg,
                MinAttendanceRate = minAtt,
                EnableSacramentEvaluation = false,
                TargetYearClasses = targetClasses,
                SuggestedRetainClassId = suggestedRetain?.Id,
                Students = rows
            };

            return View(vm);
        }

        /// <summary>
        /// 2. POST: /Promotion/CloneClassesToNewYear (Tự động sao chép danh mục Lớp từ năm cũ sang năm mới)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloneClassesToNewYear(int sourceYearId, int targetYearId)
        {
            if (sourceYearId == targetYearId)
            {
                return Json(new { success = false, message = "Niên khóa nguồn và niên khóa đích phải khác nhau!" });
            }

            var sourceClasses = await _context.Classes
                .Where(c => c.AcademicYearId == sourceYearId)
                .AsNoTracking()
                .ToListAsync();

            var existingTargetNames = await _context.Classes
                .Where(c => c.AcademicYearId == targetYearId)
                .Select(c => c.Name.ToLower())
                .ToListAsync();

            var toCreate = sourceClasses
                .Where(sc => !existingTargetNames.Contains(sc.Name.ToLower()))
                .Select(sc => new ClassRoom
                {
                    AcademicYearId = targetYearId,
                    Name = sc.Name,
                    GradeLevel = sc.GradeLevel,
                    RoomName = sc.RoomName,
                    ClassRoomLocationId = sc.ClassRoomLocationId,
                    Shift = sc.Shift,
                    IsGradeLocked = false
                })
                .ToList();

            if (!toCreate.Any())
            {
                return Json(new { success = false, message = "Tất cả các lớp của niên khóa nguồn đều đã tồn tại ở niên khóa đích." });
            }

            await _context.Classes.AddRangeAsync(toCreate);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đã sao chép {Count} lớp từ Year {Source} sang Year {Target}", toCreate.Count, sourceYearId, targetYearId);
            return Json(new { success = true, message = $"Đã khởi tạo tự động {toCreate.Count} lớp học cho niên khóa mới!" });
        }

        /// <summary>
        /// 3. POST: /Promotion/ExecuteRollover (Thực thi Kết chuyển Học sinh hàng loạt sang Niên khóa mới)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteRollover([FromBody] ExecuteRolloverRequestDto request)
        {
            if (request == null || !request.Decisions.Any())
            {
                return Json(new { success = false, message = "Không có danh sách học sinh nào để kết chuyển." });
            }

            // Lấy các học sinh đã có lớp ở niên khóa đích để chống trùng lặp
            var studentIds = request.Decisions.Select(d => d.StudentId).ToList();
            var alreadyEnrolledIds = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .Where(e => e.ClassRoom.AcademicYearId == request.TargetYearId && studentIds.Contains(e.StudentId))
                .Select(e => e.StudentId)
                .ToListAsync();

            var newEnrollments = new List<Enrollment>();
            int promotedCount = 0;
            int retainedCount = 0;

            foreach (var item in request.Decisions)
            {
                if (item.Skip || alreadyEnrolledIds.Contains(item.StudentId))
                    continue;

                if (item.IsPromoted && request.PromoteToClassId.HasValue && request.PromoteToClassId.Value > 0)
                {
                    newEnrollments.Add(new Enrollment
                    {
                        StudentId = item.StudentId,
                        ClassRoomId = request.PromoteToClassId.Value,
                        EnrolledAt = DateTime.UtcNow
                    });
                    promotedCount++;
                }
                else if (!item.IsPromoted && request.RetainInClassId.HasValue && request.RetainInClassId.Value > 0)
                {
                    newEnrollments.Add(new Enrollment
                    {
                        StudentId = item.StudentId,
                        ClassRoomId = request.RetainInClassId.Value,
                        EnrolledAt = DateTime.UtcNow
                    });
                    retainedCount++;
                }
            }

            if (!newEnrollments.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Chưa chọn Lớp Đích hợp lệ hoặc tất cả học sinh trong danh sách đã được xếp lớp ở niên khóa mới!"
                });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    await _context.Enrollments.AddRangeAsync(newEnrollments);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                _logger.LogInformation("Kết chuyển niên khóa thành công: {Promoted} em lên lớp, {Retained} em ở lại lớp.",
                    promotedCount, retainedCount);

                return Json(new
                {
                    success = true,
                    message = $"Kết chuyển thành công {newEnrollments.Count} thiếu nhi sang niên khóa mới ({promotedCount} em lên lớp, {retainedCount} em ở lại lớp)!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực thi kết chuyển niên khóa");
                return Json(new { success = false, message = "Lỗi khi lưu kết chuyển: " + ex.Message });
            }
        }
    }
}