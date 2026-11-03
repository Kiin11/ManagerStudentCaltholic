using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class LessonPlansController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<LessonPlansController> _logger;

        public LessonPlansController(ParishDbContext context, ILogger<LessonPlansController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private long CurrentUserId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        /// <summary>
        /// SEC-NEW-05 FIX: Kiểm tra người dùng hiện tại có thẩm quyền quản lý tài liệu của lớp hay không
        /// </summary>
        private async Task<bool> CanManageClassLessonAsync(ClassRoom targetClass)
        {
            if (IsLeadership) return true;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrWhiteSpace(managedGrade) &&
                    string.Equals(targetClass.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return await _context.ClassTeachers
                .AnyAsync(ct => ct.ClassRoomId == targetClass.Id && ct.UserId == CurrentUserId);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? classRoomId)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var classesQuery = _context.Classes.Where(c => c.AcademicYearId == yearId);

            // Lọc danh sách lớp theo đúng phạm vi quyền hạn
            if (!IsLeadership)
            {
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    classesQuery = !string.IsNullOrWhiteSpace(managedGrade)
                        ? classesQuery.Where(c => c.GradeLevel == managedGrade || c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId))
                        : classesQuery.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
                }
                else
                {
                    classesQuery = classesQuery.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
                }
            }

            var classes = await classesQuery
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Classes = classes;
            ViewBag.CurrentYear = currentYear;

            if (classRoomId.HasValue && !classes.Any(c => c.Id == classRoomId.Value))
            {
                return Forbid();
            }

            var selectedClassId = classRoomId ?? classes.FirstOrDefault()?.Id ?? 0;
            ViewBag.SelectedClassId = selectedClassId;

            var selectedClass = classes.FirstOrDefault(c => c.Id == selectedClassId);
            ViewBag.SelectedClassName = selectedClass?.Name ?? "Chưa chọn lớp";
            ViewBag.SelectedGrade = selectedClass?.GradeLevel ?? "";

            var documents = await _context.ClassLessonDocuments
                .Where(d => d.ClassRoomId == selectedClassId && d.AcademicYearId == yearId)
                .OrderByDescending(d => d.UploadedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(documents);
        }

        /// <summary>
        /// P2-SEC-05 FIX: Kiểm tra chữ ký byte đầu (Magic Bytes) của tệp tải lên
        /// </summary>
        private static async Task<bool> HasValidMagicBytesAsync(IFormFile file, string ext)
        {
            if (file.Length < 4) return false;

            byte[] header = new byte[8];
            await using var stream = file.OpenReadStream();
            int bytesRead = await stream.ReadAsync(header, 0, header.Length);
            if (bytesRead < 4) return false;

            return ext switch
            {
                // PDF: %PDF (0x25 0x50 0x44 0x46)
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,

                // DOCX / XLSX (OpenXML ZIP): PK\x03\x04 (0x50 0x4B 0x03 0x04)
                ".docx" or ".xlsx" => header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,

                // DOC / XLS (OLE2 Compound Document): D0 CF 11 E0 A1 B1 1A E1
                ".doc" or ".xls" => bytesRead >= 8 &&
                                    header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0 &&
                                    header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1,

                _ => false
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] int classRoomId, [FromForm] string? description)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn file kế hoạch để tải lên." });
                }

                const long maxFileSize = 15 * 1024 * 1024;
                if (file.Length > maxFileSize)
                {
                    return Json(new { success = false, message = "Dung lượng file vượt quá giới hạn 15MB." });
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".xlsx", ".xls" };
                if (!allowedExtensions.Contains(ext) || !await HasValidMagicBytesAsync(file, ext))
                {
                    return Json(new { success = false, message = "Định dạng hoặc cấu trúc tệp không hợp lệ! Chỉ chấp nhận tệp chuẩn .pdf, .docx, .xlsx." });
                }

                var targetClass = await _context.Classes.FindAsync(classRoomId);
                if (targetClass == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học yêu cầu." });
                }

                if (!await CanManageClassLessonAsync(targetClass))
                {
                    return Json(new { success = false, message = "Bạn không có thẩm quyền tải giáo án lên lớp học này!" });
                }

                // P2-SEC-05 FIX: Lưu vào PrivateUploads/lesson_plans thay vì wwwroot
                var privateFolder = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "PrivateUploads", "lesson_plans"));
                Directory.CreateDirectory(privateFolder);

                var storedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                var physicalPath = Path.Combine(privateFolder, storedFileName);

                await using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var safeOriginalName = Path.GetFileName(file.FileName);
                var doc = new ClassLessonDocument
                {
                    ClassRoomId = targetClass.Id,
                    AcademicYearId = targetClass.AcademicYearId,
                    FileName = safeOriginalName,
                    StoredFileName = storedFileName,
                    FilePath = string.Empty, // Sẽ gán link DownloadDocument ngay sau khi SaveChanges
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    Description = string.IsNullOrWhiteSpace(description) ? safeOriginalName : description.Trim(),
                    UploadedBy = User.Identity?.Name ?? "GLV",
                    UploadedAt = DateTime.UtcNow
                };

                _context.ClassLessonDocuments.Add(doc);
                await _context.SaveChangesAsync();

                doc.FilePath = $"/LessonPlans/DownloadDocument/{doc.Id}";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Người dùng {User} đã upload tài liệu {FileName} cho lớp {ClassId}",
                    User.Identity?.Name, safeOriginalName, classRoomId);

                return Json(new { success = true, message = $"Đã tải lên và lưu file '{safeOriginalName}' an toàn!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload tài liệu giáo án cho lớp {ClassId}", classRoomId);
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi lưu tệp." });
            }
        }

        /// <summary>
        /// P2-SEC-05 FIX: Endpoint bảo mật để Xem/Tải giáo án có kiểm tra quyền truy cập lớp
        /// </summary>
        [HttpGet("LessonPlans/DownloadDocument/{id:long}")]
        public async Task<IActionResult> DownloadDocument(long id, [FromQuery] bool inline = true)
        {
            var doc = await _context.ClassLessonDocuments
                .Include(d => d.ClassRoom)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null || doc.ClassRoom == null) return NotFound();

            if (!await CanManageClassLessonAsync(doc.ClassRoom))
            {
                return Forbid();
            }

            var safeStoredName = Path.GetFileName(doc.StoredFileName);
            var privatePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "PrivateUploads", "lesson_plans", safeStoredName));
            var legacyPublicPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "lesson_plans", safeStoredName));

            string? targetPath = System.IO.File.Exists(privatePath)
                ? privatePath
                : (System.IO.File.Exists(legacyPublicPath) ? legacyPublicPath : null);

            if (targetPath == null) return NotFound("Không tìm thấy tệp giáo án trên máy chủ.");

            var contentType = Path.GetExtension(safeStoredName).ToLowerInvariant() == ".pdf"
                ? "application/pdf"
                : (string.IsNullOrWhiteSpace(doc.ContentType) ? "application/octet-stream" : doc.ContentType);

            var fileStream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (inline && contentType == "application/pdf")
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(doc.FileName)}\"";
                return File(fileStream, contentType);
            }

            return File(fileStream, contentType, doc.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // SEC-NEW-02 FIX: Bật lại kiểm tra CSRF
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> DeleteDocument([FromForm] long documentId)
        {
            var doc = await _context.ClassLessonDocuments
                .Include(d => d.ClassRoom)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null) return Json(new { success = false, message = "Không tìm thấy tài liệu cần xóa." });

            // SEC-NEW-05 FIX 1: Kiểm tra quyền của Trưởng khối trên lớp của tài liệu
            if (doc.ClassRoom != null && !await CanManageClassLessonAsync(doc.ClassRoom))
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa tài liệu của khối/lớp khác!" });
            }

            // SEC-NEW-05 FIX 2: Chặn Path Traversal khi xóa tệp vật lý
            var uploadsRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "lesson_plans"));
            if (!uploadsRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                uploadsRoot += Path.DirectorySeparatorChar;
            }

            var safeStoredName = Path.GetFileName(doc.StoredFileName);
            if (!string.IsNullOrWhiteSpace(safeStoredName))
            {
                var physicalPath = Path.GetFullPath(Path.Combine(uploadsRoot, safeStoredName));
                if (physicalPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(physicalPath))
                {
                    try { System.IO.File.Delete(physicalPath); } catch { }
                }
            }

            _context.ClassLessonDocuments.Remove(doc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa tài liệu kế hoạch thành công!" });
        }
    }
}