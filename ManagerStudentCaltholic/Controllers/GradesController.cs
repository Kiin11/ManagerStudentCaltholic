using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
    public class GradesController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;
        private readonly ILogger<GradesController> _logger;

        public GradesController(ParishDbContext context, IGradeCalculationService gradeService, ILogger<GradesController> logger)
        {
            _context = context;
            _gradeService = gradeService;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /Grades/Index?classId=...
        /// </summary>
        /// <param name="classId"></param>
        /// <param name="semester"></param>
        /// <returns></returns>&semester=1 (Màn hình ma trận nhập điểm)
        [HttpGet]
        public async Task<IActionResult> Index(int? classId, int semester = 1)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            if (currentYear == null)
            {
                TempData["ErrorMessage"] = "Chưa có niên khóa nào được kích hoạt.";
                return RedirectToAction("Index", "Home");
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            long.TryParse(userIdStr, out var currentUserId);

            bool isLeadership = User.IsInRole(UserRole.Admin) ||
                                User.IsInRole(UserRole.SpiritualDirector) ||
                                User.IsInRole(UserRole.ExecutiveBoard);
            bool isBranchHead = User.IsInRole(UserRole.BranchHead);
            var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;


            var classes =  _context.Classes
                .Where(c => c.AcademicYearId == currentYear.Id);

            if (isLeadership)
            {
                // Admin, Cha, BĐH: Xem được toàn bộ lớp của tất cả các khối
            }
            else if (isBranchHead && !string.IsNullOrEmpty(managedGrade))
            {
                // Trưởng khối: Xem được tất cả các lớp trong khối mình phụ trách
                classes = classes.Where(c => c.GradeLevel == managedGrade);
            }
            else
            {
                // Giáo lý viên (Teacher): CHỈ XEM ĐƯỢC CÁC LỚP MÌNH ĐƯỢC PHÂN CÔNG (dựa theo ClassTeachers.UserId)
                classes = classes.Where(c => c.ClassTeachers.Any(ct => ct.UserId == currentUserId));
            }

            var allowedClasses = await classes
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

            // 3. Kiểm tra lớp được yêu cầu (classId)
            int targetClassId = classId ?? allowedClasses.First().Id;

            // CHẶN HÀNH VI ĐỔI ID TRÊN URL ĐỂ XEM LỚP KHÁC
            var targetClass = allowedClasses.FirstOrDefault(c => c.Id == targetClassId);
            if (targetClass == null)
            {
                // Nếu lớp yêu cầu không nằm trong danh sách được phép
                return Forbid(); // Trả về HTTP 403 Forbidden
            }

            ViewBag.Classes = allowedClasses;
            ViewBag.SelectedClassId = targetClass.Id;
            ViewBag.CurrentSemester = semester;

            // 4. Lấy cấu hình các cột điểm cho khối/lớp này
            var configs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == currentYear.Id &&
                            g.Semester == semester &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == targetClass.GradeLevel) &&
                            (g.ClassRoomId == null || g.ClassRoomId == targetClass.Id))
                .OrderBy(g => g.ColumnIndex)
                .AsNoTracking()
                .ToListAsync();

            // 5. Lấy danh sách học sinh và toàn bộ điểm đã có
            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.GradeRecords)
                .Where(e => e.ClassRoomId == targetClass.Id && e.Student.IsActive)
                .OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            // 6. Tính % chuyên cần phục vụ đánh giá xếp loại (từ Epic 4[cite: 1, 2])
            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            var totalSessions = Math.Max(1, attendances.Select(a => a.AttendanceDate).Distinct().Count());

            var rows = enrollments.Select(e =>
            {
                var scoreDict = e.GradeRecords
                    .Where(r => configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var attList = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                var presentCount = attList.Count(a => a.ClassAttended && (a.ClassStatus == "PRESENT" || a.ClassStatus == "LATE"));
                var attRate = Math.Round((presentCount / (double)totalSessions) * 100, 1);

                var semAvg = _gradeService.CalculateSemesterAverage(scoreDict, configs);
                var (rank, eligible) = _gradeService.EvaluateStudent(semAvg, attRate, targetClass.GradeLevel, semester);

                return new StudentGradeRowItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    Scores = scoreDict,
                    SemesterAverage = semAvg,
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
                Columns = configs,
                Rows = rows
            };

            return View(viewModel);
        }

        /// <summary>
        /// 2. POST: /Grades/SaveSingleCell (Lưu Ajax tức thì từng ô khi nhập điểm)
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSingleCell([FromBody] SaveGradeItemDto dto)
        {
            if (dto == null) return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            var enrollment = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .FirstOrDefaultAsync(e => e.Id == dto.EnrollmentId);

            if (enrollment == null) return Json(new { success = false, message = "Không tìm thấy học sinh" });

            if (enrollment.ClassRoom.IsGradeLocked)
            {
                return Json(new { success = false, message = "Sổ điểm lớp này đã bị khóa bởi Ban Điều Hành!" });
            }

            var record = await _context.GradeRecords
                .FirstOrDefaultAsync(r => r.EnrollmentId == dto.EnrollmentId && r.GradeConfigurationId == dto.GradeConfigurationId);

            decimal? oldScore = record?.Score;
            var currentUser = User.Identity?.Name ?? "GLV";

            if (record == null)
            {
                record = new GradeRecord
                {
                    EnrollmentId = dto.EnrollmentId,
                    GradeConfigurationId = dto.GradeConfigurationId,
                    Score = dto.Score,
                    UpdatedBy = currentUser,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.GradeRecords.Add(record);
            }
            else
            {
                record.Score = dto.Score;
                record.UpdatedBy = currentUser;
                record.UpdatedAt = DateTime.UtcNow;
            }

            // Ghi vết Audit Log (TASK-1010)
            if (oldScore != dto.Score)
            {
                _context.Database.ExecuteSqlRaw(
                    @"INSERT INTO ""GradeAuditLogs"" (""EnrollmentId"", ""GradeConfigurationId"", ""OldScore"", ""NewScore"", ""Action"", ""ModifiedBy"", ""ModifiedAt"")
                      VALUES ({0}, {1}, {2}, {3}, {4}, {5}, NOW())",
                    dto.EnrollmentId, dto.GradeConfigurationId, (object?)oldScore ?? DBNull.Value, (object?)dto.Score ?? DBNull.Value,
                    oldScore.HasValue ? "UPDATE" : "INSERT", currentUser);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, score = dto.Score });
        }

        /// <summary>
        /// 3. POST: /Grades/ToggleLock (Bật/Tắt khóa sổ điểm - Dành cho BĐH/Admin)
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int classId)
        {
            var cls = await _context.Classes.FindAsync(classId);
            if (cls == null) return NotFound();

            cls.IsGradeLocked = !cls.IsGradeLocked;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isLocked = cls.IsGradeLocked, message = cls.IsGradeLocked ? "Đã khóa sổ điểm thành công!" : "Đã mở khóa sổ điểm!" });
        }
    }
}