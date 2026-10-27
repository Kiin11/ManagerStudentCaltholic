using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    // SEC-04 FIX 1: Mặc định yêu cầu quyền RequireStaff ở cấp Controller
    [Authorize(Policy = "RequireStaff")]
    public class AbsenceController : Controller
    {
        private readonly ParishDbContext _context;

        public AbsenceController(ParishDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 1. Phụ huynh nộp đơn xin nghỉ trực tuyến (Public không cần đăng nhập)
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)] // SEC-04 FIX 2: Chặn spam & brute-force SĐT
        public async Task<IActionResult> SubmitRequest([FromBody] SubmitAbsenceRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Reason) || string.IsNullOrWhiteSpace(dto.ParentPhone))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ Số điện thoại Phụ huynh và Lý do xin phép nghỉ." });
            }

            var cleanCode = dto.StudentCode.Trim().ToUpperInvariant();
            var startDob = DateTime.SpecifyKind(dto.DateOfBirth.Date, DateTimeKind.Utc);
            var endDob = startDob.AddDays(1);

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentCode.ToUpper() == cleanCode &&
                                          s.DateOfBirth >= startDob && s.DateOfBirth < endDob &&
                                          s.IsActive);

            // Kiểm tra đối chiếu SĐT Phụ huynh gửi lên với SĐT trong hồ sơ
            var inputPhoneDigits = new string(dto.ParentPhone.Where(char.IsDigit).ToArray());
            var recordPhoneDigits = new string((student?.ParentPhone ?? "").Where(char.IsDigit).ToArray());

            if (student == null || string.IsNullOrEmpty(recordPhoneDigits) || inputPhoneDigits != recordPhoneDigits)
            {
                return Json(new { success = false, message = "Số điện thoại Phụ huynh không khớp với hồ sơ đã đăng ký của thiếu nhi!" });
            }

            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.StudentId == student.Id && (currentYear == null || e.ClassRoom.AcademicYearId == currentYear.Id));

            if (enrollment == null)
            {
                return Json(new { success = false, message = "Thiếu nhi chưa được xếp vào lớp học trong năm nay." });
            }

            var newRequest = new AbsenceRequest
            {
                EnrollmentId = enrollment.Id,
                AbsenceDate = dto.AbsenceDate.Date,
                SessionType = dto.SessionType,
                Reason = dto.Reason.Trim(),
                ParentPhone = dto.ParentPhone?.Trim(),
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            _context.AbsenceRequests.Add(newRequest);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Gửi đơn xin phép thành công! Giáo lý viên chủ nhiệm sẽ sớm xem và xác nhận." });
        }

        /// <summary>
        /// 2. GLV / BĐH Xem danh sách đơn xin phép theo lớp
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> Manage(int? classId)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            // SEC-04 FIX 3: Chỉ lấy danh sách lớp nằm trong phạm vi quyền hạn
            var authorizedClasses = await GetAuthorizedClassesAsync(yearId);
            ViewBag.Classes = authorizedClasses;

            if (!authorizedClasses.Any())
            {
                ViewBag.SelectedClassId = 0;
                return View(new List<AbsenceRequest>());
            }

            // Chặn xem chéo lớp nếu tự truyền classId qua URL
            if (classId.HasValue && !authorizedClasses.Any(c => c.Id == classId.Value))
            {
                return Forbid();
            }

            var selectedClassId = classId ?? authorizedClasses.First().Id;
            ViewBag.SelectedClassId = selectedClassId;

            var requests = await _context.AbsenceRequests
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.Student)
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.ClassRoom)
                .Where(r => r.Enrollment.ClassRoomId == selectedClassId)
                .OrderByDescending(r => r.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(requests);
        }

        /// <summary>
        /// 3. GLV / BĐH Duyệt đơn & Tự động đồng bộ sang bảng Điểm Danh (Attendances)
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review([FromBody] ReviewAbsenceRequestDto dto)
        {
            var req = await _context.AbsenceRequests
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.ClassRoom)
                .FirstOrDefaultAsync(r => r.Id == dto.RequestId);

            if (req == null)
                return Json(new { success = false, message = "Không tìm thấy đơn." });

            // SEC-04 FIX 4: Kiểm tra người duyệt có quyền trên lớp của học sinh này không
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var authorizedClasses = await GetAuthorizedClassesAsync(currentYear?.Id ?? 0);
            if (!authorizedClasses.Any(c => c.Id == req.Enrollment.ClassRoomId))
            {
                return Json(new { success = false, message = "Bạn không có thẩm quyền duyệt đơn xin phép của lớp này!" });
            }

            req.Status = dto.IsApproved ? "APPROVED" : "REJECTED";
            req.ReviewNote = dto.Note;
            req.ReviewedBy = User.Identity?.Name ?? "GLV";
            req.ReviewedAt = DateTime.UtcNow;

            // Nếu DUYỆT: Tự động cập nhật Attendance sang vắng có phép
            if (dto.IsApproved)
            {
                var attDate = req.AbsenceDate.Date;
                var att = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.EnrollmentId == req.EnrollmentId && a.AttendanceDate == attDate);

                if (att == null)
                {
                    att = new Attendance
                    {
                        EnrollmentId = req.EnrollmentId,
                        AttendanceDate = attDate,
                        DayOfWeek = attDate.DayOfWeek
                    };
                    _context.Attendances.Add(att);
                }

                if (req.SessionType == "MASS" || req.SessionType == "ALL")
                {
                    att.AttendedMass = false;
                    att.MassStatus = "ABSENT_PERMITTED";
                }

                if (req.SessionType == "CLASS" || req.SessionType == "ALL")
                {
                    att.ClassAttended = false;
                    att.ClassStatus = "ABSENT_PERMITTED";
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = dto.IsApproved ? "Đã duyệt đơn và ghi nhận vắng có phép!" : "Đã từ chối đơn." });
        }

        /// <summary>
        /// SEC - 04 FIX 2: Hàm phụ trợ lấy danh sách lớp học mà người dùng hiện tại được phép quản lý đơn nghỉ phép
        /// </summary>
        private async Task<List<ClassRoom>> GetAuthorizedClassesAsync(int currentYearId)
        {
            var classesQuery = _context.Classes
                .Where(c => currentYearId == 0 || c.AcademicYearId == currentYearId);

            // Nếu là Trưởng khối: Chỉ lấy các lớp thuộc khối mình phụ trách
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrEmpty(managedGrade))
                {
                    classesQuery = classesQuery.Where(c => c.GradeLevel == managedGrade);
                }
            }
            // Nếu là Giáo lý viên: Chỉ lấy các lớp được phân công đứng lớp
            else if (User.IsInRole(UserRole.Teacher))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                long.TryParse(userIdStr, out var currentUserId);

                var assignedClassIds = await _context.ClassTeachers
                    .Where(ct => ct.UserId == currentUserId && (currentYearId == 0 || ct.AcademicYearId == currentYearId))
                    .Select(ct => ct.ClassRoomId)
                    .ToListAsync();

                classesQuery = classesQuery.Where(c => assignedClassIds.Contains(c.Id));
            }

            return await classesQuery
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}