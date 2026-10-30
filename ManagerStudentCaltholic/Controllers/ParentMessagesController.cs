using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
    public class ParentMessagesController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<ParentMessagesController> _logger;

        public ParentMessagesController(ParishDbContext context, ILogger<ParentMessagesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        private async Task<bool> CanAccessClassAsync(ClassRoom classRoom)
        {
            if (IsLeadership) return true;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                return !string.IsNullOrWhiteSpace(managedGrade) &&
                       string.Equals(classRoom.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase);
            }

            var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(uidStr, out var uid)) return false;

            return await _context.ClassTeachers.AnyAsync(ct => ct.ClassRoomId == classRoom.Id && ct.UserId == uid);
        }

        /// <summary>
        /// 1. GET: /ParentMessages (Trang danh sách & Quản lý mẫu tin nhắn nhanh)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(string? category)
        {
            // Tự động khởi tạo mẫu tin nhắn mặc định nếu bảng đang trống
            if (!await _context.ParentMessageTemplates.AnyAsync())
            {
                var defaults = new List<ParentMessageTemplate>
                {
                    new ParentMessageTemplate
                    {
                        Title = "Thông báo vắng Thánh Lễ & Học Giáo Lý Chúa Nhật",
                        Category = "CHUYÊN CẦN",
                        ContentTemplate = "Kính chào Quý Phụ huynh em {TenThieuNhi} (Lớp {TenLop}). Hôm nay ngày {NgayVang}, Ban Giáo Lý ghi nhận em vắng mặt trong buổi sinh hoạt Giáo lý mà chưa có đơn xin phép. Gia đình vui lòng phản hồi lại cho GLV qua {SDTLienHe}. Xin cảm ơn Quý Phụ huynh!",
                        CreatedAt = DateTime.UtcNow
                    },
                    new ParentMessageTemplate
                    {
                        Title = "Nhắc nhở đi trễ Thánh Lễ Chúa Nhật",
                        Category = "CHUYÊN CẦN",
                        ContentTemplate = "Kính gửi Quý Phụ huynh em {TenThieuNhi} (Lớp {TenLop}). Sáng Chúa Nhật ngày {NgayVang}, em đến tham dự Thánh Lễ trễ sau giờ quy định. Kính mong Phụ huynh nhắc nhở em đi lễ đúng giờ (trước 06h30) để giữ sốt sắng. Trân trọng!",
                        CreatedAt = DateTime.UtcNow
                    },
                    new ParentMessageTemplate
                    {
                        Title = "Thông báo lịch kiểm tra / Khảo hạch Giáo lý",
                        Category = "HỌC TẬP",
                        ContentTemplate = "Thông báo đến Quý Phụ huynh em {TenThieuNhi} (Lớp {TenLop}): Chúa Nhật tuần tới lớp sẽ có bài kiểm tra Giáo lý định kỳ. Kính nhờ Phụ huynh nhắc nhở em ôn bài đầy đủ tại nhà. Mọi thắc mắc xin liên hệ {SDTLienHe}.",
                        CreatedAt = DateTime.UtcNow
                    }
                };

                _context.ParentMessageTemplates.AddRange(defaults);
                await _context.SaveChangesAsync();
            }

            var query = _context.ParentMessageTemplates.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(category) && category != "ALL")
            {
                query = query.Where(t => t.Category == category);
            }

            var templates = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.SelectedCategory = category ?? "ALL";
            return View(templates);
        }

        /// <summary>
        /// 2. POST: /ParentMessages/Create (Thêm mẫu tin nhắn mới)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> Create(string title, string category, string contentTemplate)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(contentTemplate))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ Tiêu đề và Nội dung mẫu tin nhắn.";
                return RedirectToAction(nameof(Index));
            }

            var item = new ParentMessageTemplate
            {
                Title = title.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "CHUNG" : category.Trim().ToUpperInvariant(),
                ContentTemplate = contentTemplate.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.ParentMessageTemplates.Add(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm mẫu tin nhắn phụ huynh thành công!";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 3. POST: /ParentMessages/Delete/{id} (Xóa mẫu tin nhắn)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> Delete(long id)
        {
            var item = await _context.ParentMessageTemplates.FindAsync(id);
            if (item != null)
            {
                _context.ParentMessageTemplates.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa mẫu tin nhắn.";
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 4. GET: /ParentMessages/GetTemplatesJson (Dùng cho Popup trong Sổ Điểm Danh)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTemplatesJson()
        {
            var templates = await _context.ParentMessageTemplates
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    category = t.Category,
                    contentTemplate = t.ContentTemplate
                })
                .AsNoTracking()
                .ToListAsync();

            return Json(new { success = true, templates });
        }

        /// <summary>
        /// 5. GET: /ParentMessages/RenderTemplateByEnrollment (Điền dữ liệu học sinh theo EnrollmentId)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RenderTemplateByEnrollment(long templateId, long enrollmentId)
        {
            var template = await _context.ParentMessageTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId);
            if (template == null)
                return NotFound(new { success = false, message = "Không tìm thấy mẫu tin nhắn." });

            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.ClassRoom)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment == null || enrollment.ClassRoom == null || enrollment.Student == null)
                return BadRequest(new { success = false, message = "Không tìm thấy thông tin xếp lớp của thiếu nhi." });

            if (!await CanAccessClassAsync(enrollment.ClassRoom))
                return Forbid();

            var student = enrollment.Student;
            string message = template.ContentTemplate
                .Replace("{TenThieuNhi}", $"{student.ChristianName} {student.FirstName} {student.LastName}".Trim())
                .Replace("{TenLop}", enrollment.ClassRoom.Name)
                .Replace("{NgayVang}", DateTime.Now.ToString("dd/MM/yyyy"))
                .Replace("{SDTLienHe}", User.FindFirst("FullName")?.Value ?? "Văn phòng Giáo xứ");

            return Json(new { success = true, compiledMessage = message });
        }

        /// <summary>
        /// 6. GET: /ParentMessages/RenderTemplate (Điền dữ liệu theo templateId, studentId, classId)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RenderTemplate(long templateId, long studentId, int classId)
        {
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ClassRoomId == classId && e.StudentId == studentId);

            if (enrollment == null)
                return BadRequest(new { success = false, message = "Học viên không thuộc lớp học đã chọn." });

            return await RenderTemplateByEnrollment(templateId, enrollment.Id);
        }
    }
}