using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [AllowAnonymous]
    public class PublicRegistrationController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<PublicRegistrationController> _logger;

        public PublicRegistrationController(ParishDbContext context, ILogger<PublicRegistrationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /PublicRegistration/Index (Giao diện Form đăng ký trực tuyến)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

            string? relativeWebPath = null;

            // Xử lý upload ảnh/file giấy tờ Rửa tội
            if (baptismCertificate != null && baptismCertificate.Length > 0)
            {
                const long maxFileSize = 5 * 1024 * 1024; // Giới hạn tối đa 5MB
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

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "baptism_certs");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var storedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                var physicalPath = Path.Combine(uploadsFolder, storedFileName);
                relativeWebPath = $"/uploads/baptism_certs/{storedFileName}";

                using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await baptismCertificate.CopyToAsync(stream);
                }
            }

            var newReg = new StudentRegistrationRequest
            {
                ChristianName = christianName.Trim(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Gender = gender,
                DateOfBirth = DateTime.SpecifyKind(dateOfBirth.Date, DateTimeKind.Utc),
                BaptismDate = baptismDate.HasValue ? DateTime.SpecifyKind(baptismDate.Value.Date, DateTimeKind.Utc) : null,
                ParentPhone = parentPhone.Trim(),
                BaptismCertificatePath = relativeWebPath,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            _context.StudentRegistrationRequests.Add(newReg);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Gửi hồ sơ đăng ký thành công!" });
        }

        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }
    }
}