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

            var student = await _context.Students.FindAsync(studentId);
            var classRoom = await _context.Classes.FindAsync(classId);

            if (student == null || classRoom == null)
                return BadRequest(new { success = false, message = "Thông tin học viên hoặc lớp không hợp lệ." });

            // Thay thế các biến động (Placeholder substitution)
            string message = template.ContentTemplate
                .Replace("{TenThieuNhi}", $"{student.ChristianName} {student.FirstName} {student.LastName}")
                .Replace("{TenLop}", classRoom.Name)
                .Replace("{NgayVang}", DateTime.Now.ToString("dd/MM/yyyy"))
                .Replace("{SDTLienHe}", "Văn phòng Giáo xứ");

            return Json(new { success = true, compiledMessage = message });
        }
    }
}
