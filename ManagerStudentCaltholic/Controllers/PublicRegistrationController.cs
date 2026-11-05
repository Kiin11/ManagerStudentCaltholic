using System.Text.RegularExpressions;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
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
        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

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
        public async Task<IActionResult> Index()
        {
            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            ViewBag.CurrentYearName = currentYear?.Name ?? DateTime.Now.Year.ToString();
            return View();
        }

        /// <summary>
        /// POST: /PublicRegistration/Submit (Tiếp nhận hồ sơ đăng ký trực tuyến - TASK-P3-02.2 & TASK-P3-02.5)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> Submit([FromForm] PublicRegistrationSubmitDto dto)
        {
            // 1. Chống Bot tự động bằng Honeypot field
            if (!string.IsNullOrWhiteSpace(dto.WebsiteUrlHoneypot))
            {
                _logger.LogWarning("SECURITY: Honeypot triggered on PublicRegistration from IP {IP}",
                    HttpContext.Connection.RemoteIpAddress);
                return Json(new { success = true, message = "Hồ sơ đã được ghi nhận." });
            }

            if (!ModelState.IsValid)
            {
                var firstErr = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                               ?? "Dữ liệu đăng ký không hợp lệ.";
                return BadRequest(new { success = false, message = firstErr });
            }

            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            string cleanPhone = Regex.Replace(dto.ParentPhone.Trim(), @"\s+", "");
            string cleanFirstName = dto.FirstName.Trim();
            string cleanLastName = dto.LastName.Trim();

            // 2. Kiểm tra trùng đơn đang chờ duyệt (PENDING) hoặc đã duyệt trong cùng niên khóa
            var duplicateExists = await _context.Set<StudentRegistrationRequest>()
                .AsNoTracking()
                .AnyAsync(r => r.ParentPhone == cleanPhone &&
                               r.DateOfBirth.Date == dto.DateOfBirth.Date &&
                               r.LastName.ToLower() == cleanLastName.ToLower() &&
                               r.Status != "REJECTED" &&
                               (currentYear == null || r.AcademicYearId == currentYear.Id));

            if (duplicateExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Hồ sơ của thiếu nhi này đã được nộp trước đó và đang trong quá trình xử lý. Phụ huynh vui lòng kiểm tra tại mục Tra cứu trạng thái đơn."
                });
            }

            // 3. Kiểm tra & Lưu file ảnh/PDF Giấy Rửa Tội vào PrivateUploads (ngoài wwwroot)
            string? savedFileName = null;
            if (dto.CertificateFile != null && dto.CertificateFile.Length > 0)
            {
                if (dto.CertificateFile.Length > MaxFileSize)
                {
                    return BadRequest(new { success = false, message = "Kích thước ảnh Giấy Rửa Tội không được vượt quá 5MB." });
                }

                var ext = Path.GetExtension(dto.CertificateFile.FileName).ToLowerInvariant();
                var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                if (!allowedExts.Contains(ext))
                {
                    return BadRequest(new { success = false, message = "Chỉ chấp nhận tệp định dạng JPG, PNG hoặc PDF." });
                }

                if (!await IsValidMagicBytesAsync(dto.CertificateFile, ext))
                {
                    _logger.LogWarning("SECURITY: Invalid file magic bytes uploaded from IP {IP}",
                        HttpContext.Connection.RemoteIpAddress);
                    return BadRequest(new { success = false, message = "Nội dung tệp tải lên không hợp lệ hoặc bị hỏng." });
                }

                var privateUploadDir = Path.Combine(_env.ContentRootPath, "PrivateUploads", "baptism_certs");
                Directory.CreateDirectory(privateUploadDir);

                savedFileName = $"cert_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
                var fullSavePath = Path.Combine(privateUploadDir, savedFileName);

                await using var fileStream = new FileStream(fullSavePath, FileMode.Create);
                await dto.CertificateFile.CopyToAsync(fileStream);
            }

            var requestEntity = new StudentRegistrationRequest
            {
                ChristianName = dto.ChristianName.Trim(),
                FirstName = cleanFirstName,
                LastName = cleanLastName,
                Gender = dto.Gender == "Nữ" ? "Nữ" : "Nam",
                DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth.Date, DateTimeKind.Utc),
                BaptismDate = dto.BaptismDate.HasValue
                    ? DateTime.SpecifyKind(dto.BaptismDate.Value.Date, DateTimeKind.Utc)
                    : null,
                BaptismParish = dto.BaptismParish?.Trim(),
                ParentFullName = dto.ParentFullName.Trim(),
                ParentPhone = cleanPhone,
                ParentEmail = dto.ParentEmail?.Trim(),
                Address = dto.Address?.Trim(),
                ParishSubDivision = dto.ParishSubDivision?.Trim(),
                RegistrationType = dto.RegistrationType == "TRANSFER" ? "TRANSFER" : "NEW",
                AcademicYearId = currentYear?.Id,
                TargetGradeLevel = dto.TargetGradeLevel.Trim(),
                BaptismCertificatePath = savedFileName,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<StudentRegistrationRequest>().Add(requestEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "AUDIT PUBLIC_REG: New registration #{RegId} submitted for {FirstName} {LastName} (Phone: {Phone})",
                requestEntity.Id, requestEntity.FirstName, requestEntity.LastName, requestEntity.ParentPhone);

            TempData["SuccessMessage"] = $"Hồ sơ của em {requestEntity.ChristianName} {requestEntity.FirstName} {requestEntity.LastName} (Mã hồ sơ: #{requestEntity.Id}) đã được tiếp nhận thành công!";

            return Json(new
            {
                success = true,
                registrationId = requestEntity.Id,
                redirectUrl = Url.Action("Success", "PublicRegistration")
            });
        }

        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }

        /// <summary>
        /// GET/POST: /PublicRegistration/CheckStatus (Cổng tra cứu trạng thái hồ sơ cho Phụ huynh - TASK-P3-02.5)
        /// </summary>
        [HttpGet]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> CheckStatus(string? phone, DateTime? dob)
        {
            var vm = new RegistrationStatusCheckViewModel
            {
                ParentPhone = phone,
                DateOfBirth = dob,
                HasSearched = false
            };

            if (!string.IsNullOrWhiteSpace(phone) && dob.HasValue)
            {
                vm.HasSearched = true;
                string cleanPhone = Regex.Replace(phone.Trim(), @"\s+", "");
                var targetDob = dob.Value.Date;

                vm.Results = await _context.Set<StudentRegistrationRequest>()
                    .Include(r => r.CreatedStudent)
                    .Include(r => r.AssignedClass)
                    .Include(r => r.AcademicYear)
                    .Where(r => r.ParentPhone == cleanPhone && r.DateOfBirth.Date == targetDob)
                    .OrderByDescending(r => r.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync();
            }

                return View(vm);
        }

        /// <summary>
        /// GET: /PublicRegistration/Resubmit (Màn hình Bổ sung &amp; Nộp lại hồ sơ tuyển sinh cho Phụ huynh)
        /// </summary>
        [HttpGet]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> Resubmit(long id, string? phone, DateTime? dob)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(phone) || !dob.HasValue)
            {
                TempData["ErrorMessage"] = "Thông tin truy cập hồ sơ cần bổ sung không hợp lệ.";
                return RedirectToAction("CheckStatus");
            }

            string cleanPhone = Regex.Replace(phone.Trim(), @"\s+", "");
            var targetDob = dob.Value.Date;

            var req = await _context.Set<StudentRegistrationRequest>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.ParentPhone == cleanPhone && r.DateOfBirth.Date == targetDob);

            if (req == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ cần bổ sung hoặc thông tin xác thực không khớp.";
                return RedirectToAction("CheckStatus", new { phone = cleanPhone, dob = targetDob.ToString("yyyy-MM-dd") });
            }

            if (req.Status == "APPROVED")
            {
                TempData["ErrorMessage"] = $"Hồ sơ #{req.Id} đã được phê duyệt và xếp lớp chính thức, không thể chỉnh sửa.";
                return RedirectToAction("CheckStatus", new { phone = cleanPhone, dob = targetDob.ToString("yyyy-MM-dd") });
            }

            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            ViewBag.CurrentYearName = currentYear?.Name ?? DateTime.Now.Year.ToString();

            var model = new PublicRegistrationResubmitDto
            {
                Id = req.Id,
                OriginalParentPhone = req.ParentPhone,
                OriginalDateOfBirth = req.DateOfBirth,
                ChristianName = req.ChristianName,
                FirstName = req.FirstName,
                LastName = req.LastName,
                Gender = req.Gender,
                DateOfBirth = req.DateOfBirth,
                BaptismDate = req.BaptismDate,
                BaptismParish = req.BaptismParish,
                ParentFullName = req.ParentFullName,
                ParentPhone = req.ParentPhone,
                ParentEmail = req.ParentEmail,
                Address = req.Address,
                ParishSubDivision = req.ParishSubDivision,
                RegistrationType = req.RegistrationType,
                TargetGradeLevel = req.TargetGradeLevel,
                RejectionReason = req.RejectionReason,
                ExistingCertificatePath = req.BaptismCertificatePath,
                Status = req.Status,
                CreatedAt = req.CreatedAt,
                ReviewedAt = req.ReviewedAt
            };

            return View("Resubmit", model);
        }

        /// <summary>
        /// POST: /PublicRegistration/SubmitResubmit (Lưu thông tin bổ sung &amp; Gửi lại hồ sơ tuyển sinh)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> SubmitResubmit([FromForm] PublicRegistrationResubmitDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.WebsiteUrlHoneypot))
            {
                _logger.LogWarning("SECURITY: Honeypot triggered on SubmitResubmit from IP {IP}",
                    HttpContext.Connection.RemoteIpAddress);
                return Json(new { success = true, message = "Hồ sơ đã được cập nhật." });
            }

            if (!ModelState.IsValid)
            {
                var firstErr = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                               ?? "Dữ liệu bổ sung hồ sơ không hợp lệ.";
                return BadRequest(new { success = false, message = firstErr });
            }

            string cleanOrigPhone = Regex.Replace((dto.OriginalParentPhone ?? string.Empty).Trim(), @"\s+", "");
            var origDob = dto.OriginalDateOfBirth.Date;

            var req = await _context.Set<StudentRegistrationRequest>()
                .FirstOrDefaultAsync(r => r.Id == dto.Id && r.ParentPhone == cleanOrigPhone && r.DateOfBirth.Date == origDob);

            if (req == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy hồ sơ cần cập nhật hoặc thông tin xác thực không hợp lệ."
                });
            }

            if (req.Status == "APPROVED")
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Hồ sơ này đã được phê duyệt chính thức, không thể chỉnh sửa."
                });
            }

            // Kiểm tra & Lưu file ảnh/PDF Giấy Rửa Tội mới nếu phụ huynh tải lên thay thế
            if (dto.CertificateFile != null && dto.CertificateFile.Length > 0)
            {
                if (dto.CertificateFile.Length > MaxFileSize)
                {
                    return BadRequest(new { success = false, message = "Kích thước ảnh Giấy Rửa Tội không được vượt quá 5MB." });
                }

                var ext = Path.GetExtension(dto.CertificateFile.FileName).ToLowerInvariant();
                var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                if (!allowedExts.Contains(ext))
                {
                    return BadRequest(new { success = false, message = "Chỉ chấp nhận tệp định dạng JPG, PNG hoặc PDF." });
                }

                if (!await IsValidMagicBytesAsync(dto.CertificateFile, ext))
                {
                    _logger.LogWarning("SECURITY: Invalid file magic bytes uploaded on SubmitResubmit from IP {IP}",
                        HttpContext.Connection.RemoteIpAddress);
                    return BadRequest(new { success = false, message = "Nội dung tệp tải lên không hợp lệ hoặc bị hỏng." });
                }

                var privateUploadDir = Path.Combine(_env.ContentRootPath, "PrivateUploads", "baptism_certs");
                Directory.CreateDirectory(privateUploadDir);

                var savedFileName = $"cert_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
                var fullSavePath = Path.Combine(privateUploadDir, savedFileName);

                await using (var fileStream = new FileStream(fullSavePath, FileMode.Create))
                {
                    await dto.CertificateFile.CopyToAsync(fileStream);
                }

                // Xóa tệp cũ nếu có để tiết kiệm dung lượng đĩa
                if (!string.IsNullOrWhiteSpace(req.BaptismCertificatePath))
                {
                    var oldSafeFile = Path.GetFileName(req.BaptismCertificatePath);
                    var oldFullPath = Path.Combine(privateUploadDir, oldSafeFile);
                    if (System.IO.File.Exists(oldFullPath))
                    {
                        try { System.IO.File.Delete(oldFullPath); } catch { /* Bỏ qua lỗi xóa file cũ */ }
                    }
                }

                req.BaptismCertificatePath = savedFileName;
            }

            string cleanNewPhone = Regex.Replace(dto.ParentPhone.Trim(), @"\s+", "");

            req.ChristianName = dto.ChristianName.Trim();
            req.FirstName = dto.FirstName.Trim();
            req.LastName = dto.LastName.Trim();
            req.Gender = dto.Gender == "Nữ" ? "Nữ" : "Nam";
            req.DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth.Date, DateTimeKind.Utc);
            req.BaptismDate = dto.BaptismDate.HasValue
                ? DateTime.SpecifyKind(dto.BaptismDate.Value.Date, DateTimeKind.Utc)
                : null;
            req.BaptismParish = dto.BaptismParish?.Trim();
            req.ParentFullName = dto.ParentFullName.Trim();
            req.ParentPhone = cleanNewPhone;
            req.ParentEmail = dto.ParentEmail?.Trim();
            req.Address = dto.Address?.Trim();
            req.ParishSubDivision = dto.ParishSubDivision?.Trim();
            req.RegistrationType = dto.RegistrationType == "TRANSFER" ? "TRANSFER" : "NEW";
            req.TargetGradeLevel = dto.TargetGradeLevel.Trim();

            // Chuyển lại về trạng thái chờ Ban Điều Hành duyệt
            req.Status = "PENDING";
            req.RejectionReason = null;
            req.CreatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "AUDIT PUBLIC_REG_RESUBMIT: Registration #{RegId} updated and resubmitted for {FirstName} {LastName} (Phone: {Phone})",
                req.Id, req.FirstName, req.LastName, req.ParentPhone);

            TempData["SuccessMessage"] = $"Đã cập nhật và gửi lại hồ sơ #{req.Id} của em {req.ChristianName} {req.FirstName} {req.LastName} thành công! Hồ sơ hiện đang chờ Ban Điều Hành xét duyệt.";

            return Json(new
            {
                success = true,
                registrationId = req.Id,
                redirectUrl = Url.Action("CheckStatus", "PublicRegistration", new
                {
                    phone = req.ParentPhone,
                    dob = req.DateOfBirth.ToString("yyyy-MM-dd")
                })
            });
        }

        /// <summary>
        /// GET: /PublicRegistration/ViewOwnCertificate (Xem lại ảnh/PDF Giấy Rửa Tội đã nộp khi bổ sung hồ sơ)
        /// </summary>
        [HttpGet]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> ViewOwnCertificate(long id, string? phone, DateTime? dob)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(phone) || !dob.HasValue)
            {
                return NotFound();
            }

            string cleanPhone = Regex.Replace(phone.Trim(), @"\s+", "");
            var targetDob = dob.Value.Date;

            var req = await _context.Set<StudentRegistrationRequest>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.ParentPhone == cleanPhone && r.DateOfBirth.Date == targetDob);

            if (req == null || string.IsNullOrWhiteSpace(req.BaptismCertificatePath))
            {
                return NotFound();
            }

            string safeFileName = Path.GetFileName(req.BaptismCertificatePath);
            string fullPath = Path.Combine(_env.ContentRootPath, "PrivateUploads", "baptism_certs", safeFileName);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            string ext = Path.GetExtension(safeFileName).ToLowerInvariant();
            string contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                _ => "image/jpeg"
            };

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(fileBytes, contentType);
        }

        /// <summary>
        /// Kiểm tra chữ ký số (Magic Bytes) của file JPEG, PNG, PDF
        /// </summary>
        private static async Task<bool> IsValidMagicBytesAsync(IFormFile file, string ext)
        {
            var header = new byte[8];
            await using var stream = file.OpenReadStream();
            int bytesRead = await stream.ReadAsync(header, 0, header.Length);
            if (bytesRead < 4) return false;

            return ext switch
            {
                ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                _ => false
            };
        }
    }
}