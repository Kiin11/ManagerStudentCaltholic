using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
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
        public async Task<IActionResult> SubmitRequest([FromBody] SubmitAbsenceRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
            {
                return Json(new { success = false, message = "Vui lòng cung cấp lý do xin phép nghỉ." });
            }

            var cleanCode = dto.StudentCode.Trim().ToUpperInvariant();
            var targetDob = dto.DateOfBirth.Date;

            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentCode.ToUpper() == cleanCode && s.DateOfBirth.Date == targetDob && s.IsActive);

            if (student == null)
            {
                return Json(new { success = false, message = "Thông tin Mã thiếu nhi hoặc Ngày sinh không chính xác!" });
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
            var classes = await _context.Classes
                .Where(c => currentYear == null || c.AcademicYearId == currentYear.Id)
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .ToListAsync();

            ViewBag.Classes = classes;
            var selectedClassId = classId ?? classes.FirstOrDefault()?.Id ?? 0;
            ViewBag.SelectedClassId = selectedClassId;

            var requests = await _context.AbsenceRequests
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.Student)
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.ClassRoom)
                .Where(r => selectedClassId == 0 || r.Enrollment.ClassRoomId == selectedClassId)
                .OrderByDescending(r => r.CreatedAt)
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
                .FirstOrDefaultAsync(r => r.Id == dto.RequestId);

            if (req == null) return Json(new { success = false, message = "Không tìm thấy đơn." });

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
    }
}