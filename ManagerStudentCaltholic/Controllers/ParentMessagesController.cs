using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
    public class ParentMessagesController : Controller
    {
        private readonly ParishDbContext _context;

        public ParentMessagesController(ParishDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 1. Trang danh sách 
        /// </summary>
        /// <returns></returns>& Quản lý mẫu tin nhắn
        public async Task<IActionResult> Index()
        {
            var templates = await _context.ParentMessageTemplates
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(templates);
        }

        /// <summary>
        /// 2. API điền dữ liệu thực tế vào template cho GLV (Dùng khi bấm chọn mẫu ở Sổ điểm danh)
        /// </summary>
        /// <param name="templateId"></param>
        /// <param name="studentId"></param>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> RenderTemplate(long templateId, int studentId, int classId)
        {
            var template = await _context.ParentMessageTemplates.FindAsync(templateId);
            if (template == null) return NotFound(new { success = false, message = "Không tìm thấy mẫu tin nhắn." });

            var classRoom = await _context.Classes.FindAsync(classId);
            if (classRoom == null)
                return BadRequest(new { success = false, message = "Thông tin lớp không hợp lệ." });

            // SEC-NEW-05 FIX 1: Kiểm tra quyền của GLV / Trưởng khối trên lớp này
            bool isLeadership = User.IsInRole(UserRole.Admin) || User.IsInRole(UserRole.ExecutiveBoard);
            if (!isLeadership)
            {
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    if (string.IsNullOrWhiteSpace(managedGrade) ||
                        !string.Equals(classRoom.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                    {
                        return Forbid();
                    }
                }
                else
                {
                    var uidStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    long.TryParse(uidStr, out var uid);
                    bool isAssigned = await _context.ClassTeachers.AnyAsync(ct => ct.ClassRoomId == classId && ct.UserId == uid);
                    if (!isAssigned) return Forbid();
                }
            }

            // SEC-NEW-05 FIX 2: Đảm bảo thiếu nhi thực sự thuộc lớp đang chọn
            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.ClassRoomId == classId && e.StudentId == studentId);

            if (enrollment == null)
                return BadRequest(new { success = false, message = "Học viên không thuộc lớp học đã chọn." });

            var student = enrollment.Student;
            string message = template.ContentTemplate
                .Replace("{TenThieuNhi}", $"{student.ChristianName} {student.FirstName} {student.LastName}".Trim())
                .Replace("{TenLop}", classRoom.Name)
                .Replace("{NgayVang}", DateTime.Now.ToString("dd/MM/yyyy"))
                .Replace("{SDTLienHe}", "Văn phòng Giáo xứ");

            return Json(new { success = true, compiledMessage = message });
        }
    }
}
