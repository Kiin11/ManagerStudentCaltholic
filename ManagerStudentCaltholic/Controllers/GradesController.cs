using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
    public class GradesController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;
        private readonly ILogger<GradesController> _logger;

        public GradesController(
            ParishDbContext context,
            IGradeCalculationService gradeService,
            ILogger<GradesController> logger)
        {
            _context = context;
            _gradeService = gradeService;
            _logger = logger;
        }

        private long CurrentUserId
        {
            get
            {
                var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return long.TryParse(str, out var id) ? id : 0;
            }
        }

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        private async Task<bool> HasAccessToClassAsync(ClassRoom targetClass)
        {
            if (IsLeadership) return true;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrEmpty(managedGrade) &&
                    string.Equals(targetClass.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return await _context.ClassTeachers
                .AnyAsync(ct => ct.ClassRoomId == targetClass.Id && ct.UserId == CurrentUserId);
        }

        /// <summary>
        /// 1. GET: /Grades/Index?classId=...&semester=1
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? classId, int semester = 1)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            if (currentYear == null)
            {
                TempData["ErrorMessage"] = "Chưa có niên khóa nào được kích hoạt.";
                return RedirectToAction("Index", "Home");
            }

            var classesQuery = _context.Classes.Where(c => c.AcademicYearId == currentYear.Id);

            if (IsLeadership)
            {
                // Xem toàn bộ lớp
            }
            else if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrEmpty(managedGrade))
                {
                    classesQuery = classesQuery.Where(c => c.GradeLevel == managedGrade);
                }
            }
            else
            {
                var uid = CurrentUserId;
                classesQuery = classesQuery.Where(c => c.ClassTeachers.Any(ct => ct.UserId == uid));
            }

            var allowedClasses = await classesQuery
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            if (!allowedClasses.Any())
            {
                ViewBag.Classes = allowedClasses;
                ViewBag.CurrentSemester = semester;
                ViewBag.NoAccessReason = "Bạn chưa được phân công giảng dạy lớp nào trong niên khóa hiện hành.";
                return View(new ClassGradeMatrixViewModel { CurrentSemester = semester });
            }

            int targetClassId = classId ?? allowedClasses.First().Id;
            var targetClass = allowedClasses.FirstOrDefault(c => c.Id == targetClassId);
            if (targetClass == null)
            {
                return Forbid();
            }

            ViewBag.Classes = allowedClasses;
            ViewBag.SelectedClassId = targetClass.Id;
            ViewBag.CurrentSemester = semester;

            // Lấy toàn bộ cấu hình cột điểm của lớp trong năm học
            var allYearConfigs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == currentYear.Id &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == targetClass.GradeLevel) &&
                            (g.ClassRoomId == null || g.ClassRoomId == targetClass.Id))
                .OrderBy(g => g.Semester)
                .ThenBy(g => g.ColumnIndex)
                .AsNoTracking()
                .ToListAsync();

            var displayColumns = semester == 0
                ? allYearConfigs
                : allYearConfigs.Where(g => g.Semester == semester).ToList();

            var hk1Configs = allYearConfigs.Where(g => g.Semester == 1).ToList();
            var hk2Configs = allYearConfigs.Where(g => g.Semester == 2).ToList();

            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.GradeRecords)
                .Where(e => e.ClassRoomId == targetClass.Id && e.Student.IsActive)
                .OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            var totalSessions = Math.Max(1, attendances.Select(a => a.AttendanceDate).Distinct().Count());

            var rows = enrollments.Select(e =>
            {
                var scoreDict = e.GradeRecords
                    .Where(r => displayColumns.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var hk1Dict = e.GradeRecords
                    .Where(r => hk1Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var hk2Dict = e.GradeRecords
                    .Where(r => hk2Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var attList = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                var presentCount = attList.Count(a => a.ClassAttended && (a.ClassStatus == "PRESENT" || a.ClassStatus == "LATE"));
                var attRate = attendances.Any()
                    ? Math.Round((presentCount / (double)totalSessions) * 100, 1)
                    : 100.0;

                var sem1Avg = _gradeService.CalculateSemesterAverage(hk1Dict, hk1Configs);
                var sem2Avg = _gradeService.CalculateSemesterAverage(hk2Dict, hk2Configs);
                var yearAvg = _gradeService.CalculateYearAverage(sem1Avg, sem2Avg);

                decimal? activeAvg = semester switch
                {
                    1 => sem1Avg,
                    2 => sem2Avg,
                    _ => yearAvg
                };

                var (rank, eligible) = _gradeService.EvaluateStudent(activeAvg, attRate, targetClass.GradeLevel, semester);

                return new StudentGradeRowItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    Scores = scoreDict,
                    Semester1Average = sem1Avg,
                    Semester2Average = sem2Avg,
                    SemesterAverage = activeAvg,
                    YearAverage = yearAvg,
                    AttendanceRate = attRate,
                    AcademicRank = rank,
                    IsEligibleForSacrament = eligible
                };
            }).ToList();

            var viewModel = new ClassGradeMatrixViewModel
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AcademicYearName = currentYear.Name,
                CurrentSemester = semester,
                IsLocked = targetClass.IsGradeLocked,
                Columns = displayColumns,
                Rows = rows
            };

            return View(viewModel);
        }

        /// <summary>
        /// 2. POST: /Grades/SaveSingleCell (Lưu Ajax tức thì từng ô & trả về ĐTB mới)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSingleCell([FromBody] SaveGradeItemDto dto)
        {
            if (dto == null) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            if (dto.Score.HasValue && (dto.Score.Value < 0m || dto.Score.Value > 10m))
            {
                return Json(new { success = false, message = "Điểm số phải từ 0.0 đến 10.0." });
            }

            var enrollment = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .FirstOrDefaultAsync(e => e.Id == dto.EnrollmentId);

            if (enrollment == null)
                return Json(new { success = false, message = "Không tìm thấy học sinh." });

            if (!await HasAccessToClassAsync(enrollment.ClassRoom))
                return Forbid();

            if (enrollment.ClassRoom.IsGradeLocked)
                return Json(new { success = false, message = "Sổ điểm lớp này đã bị khóa bởi Ban Điều Hành!" });

            var config = await _context.GradeConfigurations.FindAsync(dto.GradeConfigurationId);
            if (config == null)
                return Json(new { success = false, message = "Cột điểm không tồn tại." });

            var record = await _context.GradeRecords
                .FirstOrDefaultAsync(r => r.EnrollmentId == dto.EnrollmentId && r.GradeConfigurationId == dto.GradeConfigurationId);

            var currentUser = User.Identity?.Name ?? "GLV";

            if (record == null)
            {
                record = new GradeRecord
                {
                    EnrollmentId = dto.EnrollmentId,
                    GradeConfigurationId = dto.GradeConfigurationId,
                    Score = dto.Score,
                    Note = dto.Note,
                    UpdatedBy = currentUser,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.GradeRecords.Add(record);
            }
            else
            {
                record.Score = dto.Score;
                record.Note = dto.Note;
                record.UpdatedBy = currentUser;
                record.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Tính lại Điểm Trung Bình Học kỳ tức thì để cập nhật lên UI
            var semConfigs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == enrollment.ClassRoom.AcademicYearId &&
                            g.Semester == config.Semester &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == enrollment.ClassRoom.GradeLevel) &&
                            (g.ClassRoomId == null || g.ClassRoomId == enrollment.ClassRoomId))
                .AsNoTracking()
                .ToListAsync();

            var studentScores = await _context.GradeRecords
                .Where(r => r.EnrollmentId == enrollment.Id && semConfigs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                .ToDictionaryAsync(r => r.GradeConfigurationId, r => r.Score);

            var newAvg = _gradeService.CalculateSemesterAverage(studentScores, semConfigs);

            return Json(new
            {
                success = true,
                score = dto.Score,
                semesterAverage = newAvg.HasValue ? newAvg.Value.ToString("0.00") : "--"
            });
        }

        /// <summary>
        /// 3. POST: /Grades/ToggleLock (Bật/Tắt khóa sổ điểm - Chỉ Leadership)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int classId)
        {
            var cls = await _context.Classes.FindAsync(classId);
            if (cls == null) return Json(new { success = false, message = "Không tìm thấy lớp học." });

            cls.IsGradeLocked = !cls.IsGradeLocked;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                isLocked = cls.IsGradeLocked,
                message = cls.IsGradeLocked ? "Đã khóa sổ điểm thành công!" : "Đã mở khóa sổ điểm!"
            });
        }

        /// <summary>
        /// 4. POST: /Grades/AddCustomColumn (Thêm cột điểm cho lớp)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCustomColumn([FromBody] CreateGradeColumnDto dto)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Thông tin cột điểm không hợp lệ." });
            }

            var targetClass = await _context.Classes.FindAsync(dto.ClassId);
            if (targetClass == null)
                return Json(new { success = false, message = "Lớp không tồn tại." });

            if (!await HasAccessToClassAsync(targetClass))
                return Forbid();

            if (targetClass.IsGradeLocked)
                return Json(new { success = false, message = "Sổ điểm đã khóa, không thể thêm cột." });

            var maxIndex = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == targetClass.AcademicYearId &&
                            g.Semester == dto.Semester &&
                            (g.ClassRoomId == targetClass.Id || g.ClassRoomId == null))
                .MaxAsync(g => (int?)g.ColumnIndex) ?? 0;

            var config = new GradeConfiguration
            {
                AcademicYearId = targetClass.AcademicYearId,
                GradeLevel = targetClass.GradeLevel,
                ClassRoomId = targetClass.Id,
                Semester = dto.Semester,
                ScoreType = string.IsNullOrWhiteSpace(dto.ScoreType) ? "CUSTOM" : dto.ScoreType.Trim(),
                DisplayName = dto.DisplayName.Trim(),
                WeightFactor = dto.WeightFactor <= 0 ? 1 : dto.WeightFactor,
                ColumnIndex = maxIndex + 1,
                IsSacramentExam = false,
                IsRequired = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.GradeConfigurations.Add(config);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Thêm cột điểm thành công!" });
        }

        /// <summary>
        /// 5. POST: /Grades/DeleteCustomColumn (Xóa cột điểm riêng của lớp)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomColumn(int columnId)
        {
            var config = await _context.GradeConfigurations
                .Include(c => c.ClassRoom)
                .FirstOrDefaultAsync(c => c.Id == columnId);

            if (config == null || config.ClassRoomId == null || config.ClassRoom == null)
                return Json(new { success = false, message = "Chỉ được xóa cột điểm tự tạo của riêng lớp." });

            if (!await HasAccessToClassAsync(config.ClassRoom))
                return Forbid();

            if (config.ClassRoom.IsGradeLocked)
                return Json(new { success = false, message = "Sổ điểm đang khóa, không thể xóa cột điểm." });

            _context.GradeConfigurations.Remove(config);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa cột điểm." });
        }
    }
}