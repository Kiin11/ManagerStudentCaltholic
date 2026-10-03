using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireExecutiveBoard")]
    public class EnrollmentsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<EnrollmentsController> _logger;

        public EnrollmentsController(ParishDbContext context, ILogger<EnrollmentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /Enrollments/Manage?classId=... (Màn hình điều phối xếp lớp)
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Manage(int classId)
        {
            var targetClass = await _context.Classes
                .Include(c => c.AcademicYear)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (targetClass == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học yêu cầu.";
                return RedirectToAction("Index", "Classes");
            }

            var yearId = targetClass.AcademicYearId;

            // 1. Lấy danh sách học sinh ĐÃ Ở TRONG LỚP NÀY
            var enrolledList = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassRoomId == classId)
                .OrderBy(e => e.Student.LastName)
                .ThenBy(e => e.Student.FirstName)
                .Select(e => new EnrolledStudentItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    FullName = (e.Student.ChristianName + " " + e.Student.FirstName + " " + e.Student.LastName).Trim(),
                    Gender = e.Student.Gender,
                    DateOfBirth = e.Student.DateOfBirth,
                    EnrolledAt = e.EnrolledAt
                })
                .ToListAsync();

            // 2. Tìm các học sinh ĐÃ ĐƯỢC XẾP LỚP trong niên khóa này (bất kể lớp nào)
            // Nhằm đảm bảo quy tắc 1 em chỉ vào đúng 1 lớp trong 1 niên khóa (TASK-306)
            var studentIdsAlreadyEnrolledInYear = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .Where(e => e.ClassRoom.AcademicYearId == yearId)
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync();

            // 3. Lấy danh sách học sinh CHƯA CÓ LỚP trong niên khóa này và đang Active
            var unassignedList = await _context.Students
                .Where(s => s.IsActive && !studentIdsAlreadyEnrolledInYear.Contains(s.Id))
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Select(s => new UnassignedStudentItem
                {
                    StudentId = s.Id,
                    StudentCode = s.StudentCode,
                    FullName = (s.ChristianName + " " + s.FirstName + " " + s.LastName).Trim(),
                    Gender = s.Gender,
                    DateOfBirth = s.DateOfBirth,
                    ParentPhone = s.ParentPhone
                })
                .ToListAsync();

            var viewModel = new ClassEnrollmentViewModel
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AcademicYearId = targetClass.AcademicYearId,
                AcademicYearName = targetClass.AcademicYear.Name,
                EnrolledStudents = enrolledList,
                UnassignedStudents = unassignedList
            };

            return View(viewModel);
        }

        /// <summary>
        /// 2. POST: /Enrollments/AssignStudents (Xếp học sinh vào lớp - Ajax)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AssignStudents([FromBody] AssignStudentsRequest request)
        {
            if (request == null || !request.StudentIds.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 học sinh để xếp lớp." });
            }

            var targetClass = await _context.Classes
                .Include(c => c.AcademicYear)
                .FirstOrDefaultAsync(c => c.Id == request.ClassId);

            if (targetClass == null)
            {
                return Json(new { success = false, message = "Lớp học không tồn tại." });
            }

            var yearId = targetClass.AcademicYearId;

            // KIỂM TRA CHẶN TRÙNG LẶP (TASK-306):
            // Lấy danh sách học sinh đã có lớp trong niên khóa này
            var enrolledInYear = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .Where(e => e.ClassRoom.AcademicYearId == yearId && request.StudentIds.Contains(e.StudentId))
                .Select(e => e.StudentId)
                .ToListAsync();

            // Lọc ra các em thực sự chưa có lớp
            var validStudentIds = request.StudentIds.Except(enrolledInYear).Distinct().ToList();

            if (!validStudentIds.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Tất cả các học sinh được chọn đều đã được xếp vào một lớp khác trong niên khóa này!"
                });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    var newEnrollments = validStudentIds.Select(sid => new Enrollment
                    {
                        StudentId = sid,
                        ClassRoomId = targetClass.Id,
                        EnrolledAt = DateTime.UtcNow
                    }).ToList();

                    await _context.Enrollments.AddRangeAsync(newEnrollments);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                _logger.LogInformation("Đã xếp {Count} học sinh vào lớp {Class} (Niên khóa: {Year})",
                    validStudentIds.Count, targetClass.Name, targetClass.AcademicYear.Name);

                var message = $"Đã xếp thành công {validStudentIds.Count} học sinh vào lớp {targetClass.Name}.";
                if (enrolledInYear.Any())
                {
                    message += $" (Bỏ qua {enrolledInYear.Count} em đã có lớp trước đó).";
                }

                return Json(new { success = true, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xếp học sinh vào lớp ID: {ClassId}", request.ClassId);
                return Json(new { success = false, message = "Có lỗi xảy ra trong quá trình xếp lớp: " + ex.Message });
            }
        }

        /// <summary>
        /// 3. POST: /Enrollments/RemoveStudent (Rút học sinh khỏi lớp)
        /// </summary>
        /// <param name="enrollmentId"></param>
        /// <returns></returns>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RemoveStudent(long enrollmentId)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Attendances)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin xếp lớp." });
            }

            if (enrollment.Attendances.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể rút học sinh này vì đã có dữ liệu điểm danh trong lớp! Vui lòng kiểm tra lại."
                });
            }

            _context.Enrollments.Remove(enrollment);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã đưa học sinh ra khỏi lớp thành công!" });
        }
    }
}
