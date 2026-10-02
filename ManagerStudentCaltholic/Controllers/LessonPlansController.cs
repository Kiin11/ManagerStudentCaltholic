using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize]
    public class LessonPlansController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<LessonPlansController> _logger;

        public LessonPlansController(ParishDbContext context, ILogger<LessonPlansController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==============================================================
        // 1. GET: /LessonPlans (hoặc /LessonPlans/Index?classRoomId=...)
        // ==============================================================
        [HttpGet]
        public async Task<IActionResult> Index(int? classRoomId)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var classes = await _context.Classes
                .Where(c => c.AcademicYearId == yearId)
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Classes = classes;
            ViewBag.CurrentYear = currentYear;

            var selectedClassId = classRoomId ?? classes.FirstOrDefault()?.Id ?? 0;
            ViewBag.SelectedClassId = selectedClassId;

            var selectedClass = classes.FirstOrDefault(c => c.Id == selectedClassId);
            ViewBag.SelectedClassName = selectedClass?.Name ?? "Chưa chọn lớp";
            ViewBag.SelectedGrade = selectedClass?.GradeLevel ?? "";

            // Lấy danh sách file kế hoạch đã lưu của lớp
            var documents = await _context.ClassLessonDocuments
                .Where(d => d.ClassRoomId == selectedClassId && d.AcademicYearId == yearId)
                .OrderByDescending(d => d.UploadedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(documents);
        }

        // ==============================================================
        // 2. POST: /LessonPlans/UploadDocument
        // ==============================================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] int classRoomId, [FromForm] string? description)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn file kế hoạch để tải lên." });
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".xlsx", ".xls" };
                if (!allowedExtensions.Contains(ext))
                {
                    return Json(new { success = false, message = "Chỉ chấp nhận các định dạng file: .pdf, .docx, .xlsx" });
                }

                var targetClass = await _context.Classes.FindAsync(classRoomId);
                if (targetClass == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học yêu cầu." });
                }

                // Thư mục lưu trữ: wwwroot/uploads/lesson_plans/
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "lesson_plans");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var storedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                var physicalPath = Path.Combine(uploadsFolder, storedFileName);
                var relativeWebPath = $"/uploads/lesson_plans/{storedFileName}";

                using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var doc = new ClassLessonDocument
                {
                    ClassRoomId = targetClass.Id,
                    AcademicYearId = targetClass.AcademicYearId,
                    FileName = file.FileName,
                    StoredFileName = storedFileName,
                    FilePath = relativeWebPath,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    Description = string.IsNullOrWhiteSpace(description) ? file.FileName : description.Trim(),
                    UploadedBy = User.Identity?.Name ?? "GLV",
                    UploadedAt = DateTime.UtcNow
                };

                _context.ClassLessonDocuments.Add(doc);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Người dùng {User} đã upload tài liệu {FileName} cho lớp {ClassId}",
                    User.Identity?.Name, file.FileName, classRoomId);

                return Json(new { success = true, message = $"Đã tải lên và lưu file '{file.FileName}' thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu file: " + ex.Message });
            }
        }

        // ==============================================================
        // 3. POST: /LessonPlans/DeleteDocument
        // ==============================================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> DeleteDocument([FromForm] long documentId)
        {
            var doc = await _context.ClassLessonDocuments.FindAsync(documentId);
            if (doc == null) return Json(new { success = false, message = "Không tìm thấy tài liệu cần xóa." });

            var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(physicalPath))
            {
                try { System.IO.File.Delete(physicalPath); } catch { }
            }

            _context.ClassLessonDocuments.Remove(doc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa tài liệu kế hoạch thành công!" });
        }
    }
}