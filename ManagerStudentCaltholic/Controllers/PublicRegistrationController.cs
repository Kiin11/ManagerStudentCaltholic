using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [AllowAnonymous]
    public class PublicRegistrationController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PublicRegistrationController> _logger;

        public PublicRegistrationController(
            ParishDbContext context,
            IWebHostEnvironment env,
            ILogger<PublicRegistrationController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// SEC-NEW-06 FIX: Bổ sung Rate Limiting, Kiểm tra Magic Bytes và Lưu ra ngoài wwwroot
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> Submit(
            [FromForm] string christianName,
            [FromForm] string firstName,
            [FromForm] string lastName,
            [FromForm] string gender,
            [FromForm] DateTime dateOfBirth,
            [FromForm] DateTime? baptismDate,
            [FromForm] string parentPhone,
            [FromForm] IFormFile? baptismCertificate)
        {
            if (string.IsNullOrWhiteSpace(christianName) || string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(parentPhone))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ các thông tin bắt buộc." });
            }

            string? storedCertPath = null;

            if (baptismCertificate != null && baptismCertificate.Length > 0)
            {
                const long maxFileSize = 5 * 1024 * 1024; // 5MB
                if (baptismCertificate.Length > maxFileSize)
                {
                    return Json(new { success = false, message = "Dung lượng file ảnh giấy tờ rửa tội vượt quá giới hạn 5MB." });
                }

                var ext = Path.GetExtension(baptismCertificate.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                if (!allowedExtensions.Contains(ext))
                {
                    return Json(new { success = false, message = "Chỉ chấp nhận file định dạng ảnh (.jpg, .jpeg, .png) hoặc (.pdf)." });
                }

                // SEC-NEW-06 FIX 1: Kiểm tra chữ ký tệp thực tế (Magic Bytes) chống giả mạo phần mở rộng
                if (!await IsValidFileSignatureAsync(baptismCertificate, ext))
                {
                    _logger.LogWarning("Cảnh báo bảo mật: Phát hiện upload file giả mạo định dạng '{FileName}' từ IP {IP}",
                        baptismCertificate.FileName, HttpContext.Connection.RemoteIpAddress);
                    return Json(new { success = false, message = "Nội dung tệp tải lên không khớp với định dạng ảnh/PDF chuẩn." });
                }

                // SEC-NEW-06 FIX 2: Lưu vào thư mục PrivateUploads bên ngoài wwwroot để chặn truy cập tĩnh công khai
                var privateFolder = Path.Combine(_env.ContentRootPath, "PrivateUploads", "baptism_certs");
                if (!Directory.Exists(privateFolder))
                {
                    Directory.CreateDirectory(privateFolder);
                }

                var storedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                var physicalPath = Path.Combine(privateFolder, storedFileName);

                using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await baptismCertificate.CopyToAsync(stream);
                }

                // Lưu đường dẫn trỏ về Action có kiểm tra quyền [Authorize]
                storedCertPath = $"/PublicRegistration/ViewCertificate?fileName={storedFileName}";
            }

            var newReg = new StudentRegistrationRequest
            {
                ChristianName = christianName.Trim(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Gender = gender == "Nữ" ? "Nữ" : "Nam",
                DateOfBirth = DateTime.SpecifyKind(dateOfBirth.Date, DateTimeKind.Utc),
                BaptismDate = baptismDate.HasValue ? DateTime.SpecifyKind(baptismDate.Value.Date, DateTimeKind.Utc) : null,
                ParentPhone = parentPhone.Trim(),
                BaptismCertificatePath = storedCertPath,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            _context.StudentRegistrationRequests.Add(newReg);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Gửi hồ sơ đăng ký thành công!" });
        }

        /// <summary>
        /// SEC-NEW-06 FIX 3: Chỉ nhân sự Ban Giáo Lý đã đăng nhập mới được xem Giấy Chứng Nhận Rửa Tội
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "RequireStaff")]
        public IActionResult ViewCertificate(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return BadRequest();

            var safeFileName = Path.GetFileName(fileName.Trim());
            var privateRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "PrivateUploads", "baptism_certs"));
            if (!privateRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                privateRoot += Path.DirectorySeparatorChar;
            }

            var fullPath = Path.GetFullPath(Path.Combine(privateRoot, safeFileName));
            if (!fullPath.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            {
                return NotFound("Không tìm thấy tệp chứng nhận.");
            }

            var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                _ => "image/jpeg"
            };

            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(fileStream, contentType);
        }

        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }

        private static async Task<bool> IsValidFileSignatureAsync(IFormFile file, string ext)
        {
            if (file.Length < 4) return false;

            byte[] header = new byte[8];
            using var stream = file.OpenReadStream();
            int bytesRead = await stream.ReadAsync(header, 0, header.Length);
            if (bytesRead < 4) return false;

            return ext switch
            {
                // JPEG: FF D8 FF
                ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                // PNG: 89 50 4E 47 0D 0A 1A 0A
                ".png" => bytesRead >= 8 &&
                          header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                          header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
                // PDF: 25 50 44 46 (%PDF)
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                _ => false
            };
        }
    }
}