using System.Text.RegularExpressions;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [AllowAnonymous]
    public class PublicRegistrationController : Controller
    {
        private const int MaxFailedAttempts = 5;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
        private static readonly HashSet<string> AllowedCertExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

        private readonly ParishDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ICacheService _cache;
        private readonly IImageOptimizationService _imageOptimizationService;
        private readonly ILogger<PublicRegistrationController> _logger;

        public PublicRegistrationController(
            ParishDbContext context,
            IWebHostEnvironment env,
            ICacheService cache,
            IImageOptimizationService imageOptimizationService,
            ILogger<PublicRegistrationController> logger)
        {
            _context = context;
            _env = env;
            _cache = cache;
            _imageOptimizationService = imageOptimizationService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            ViewBag.CurrentYearName = currentYear?.Name ?? VietnamTimeHelper.Today.Year.ToString();
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
                _logger.LogWarning("SECURITY: Honeypot triggered on PublicRegistration from IP {IP}", GetNormalizedClientIp());
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
            var dobStartUtc = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dto.DateOfBirth);
            var dobEndUtc = dobStartUtc.AddDays(1);

            // 2. Kiểm tra trùng đơn đang chờ duyệt (PENDING) hoặc đã duyệt trong cùng niên khóa
            var duplicateExists = await _context.Set<StudentRegistrationRequest>()
                .AsNoTracking()
                .AnyAsync(r => r.ParentPhone == cleanPhone &&
                               r.DateOfBirth >= dobStartUtc && r.DateOfBirth < dobEndUtc &&
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

            // 3. Kiểm tra & Lưu file ảnh/PDF Giấy Rửa Tội vào PrivateUploads (ảnh JPEG/PNG re-encode WebP để xóa EXIF/polyglot)
            string? savedFileName = null;
            if (dto.CertificateFile != null && dto.CertificateFile.Length > 0)
            {
                var uploadResult = await SaveValidatedCertificateFileAsync(dto.CertificateFile);
                if (!uploadResult.Success)
                {
                    return BadRequest(new { success = false, message = uploadResult.ErrorMessage });
                }

                savedFileName = uploadResult.SavedFileName;
            }

            var requestEntity = new StudentRegistrationRequest
            {
                ChristianName = dto.ChristianName.Trim(),
                FirstName = cleanFirstName,
                LastName = cleanLastName,
                Gender = dto.Gender == "Nữ" ? "Nữ" : "Nam",
                DateOfBirth = dobStartUtc,
                BaptismDate = dto.BaptismDate.HasValue
                    ? VietnamTimeHelper.NormalizeBusinessDateUtcKind(dto.BaptismDate.Value)
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
                string clientIp = GetNormalizedClientIp();
                string cleanPhone = Regex.Replace(phone.Trim(), @"\s+", "");

                var (isLockedOut, ipFailed, ipKeyFailed) = await CheckLockoutAsync(clientIp, cleanPhone);
                if (isLockedOut)
                {
                    TempData["ErrorMessage"] = "Thiết bị này đã tra cứu sai thông tin quá 5 lần liên tiếp. Vui lòng thử lại sau 15 phút!";
                    return View(vm);
                }

                vm.HasSearched = true;
                var targetDobStart = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dob.Value);
                var targetDobEnd = targetDobStart.AddDays(1);

                vm.Results = await _context.Set<StudentRegistrationRequest>()
                    .Include(r => r.CreatedStudent)
                    .Include(r => r.AssignedClass)
                    .Include(r => r.AcademicYear)
                    .Where(r => r.ParentPhone == cleanPhone && r.DateOfBirth >= targetDobStart && r.DateOfBirth < targetDobEnd)
                    .OrderByDescending(r => r.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync();

                if (vm.Results.Count == 0)
                {
                    await RecordFailedAttemptAsync(clientIp, cleanPhone, ipFailed, ipKeyFailed);
                }
                else
                {
                    await ClearFailedAttemptsAsync(clientIp, cleanPhone);
                }
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

            string clientIp = GetNormalizedClientIp();
            string cleanPhone = Regex.Replace(phone.Trim(), @"\s+", "");
            var (isLockedOut, ipFailed, ipKeyFailed) = await CheckLockoutAsync(clientIp, cleanPhone);
            if (isLockedOut)
            {
                TempData["ErrorMessage"] = "Thiết bị này đã nhập sai thông tin quá 5 lần liên tiếp. Vui lòng thử lại sau 15 phút!";
                return RedirectToAction("CheckStatus");
            }

            var targetDobStart = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dob.Value);
            var targetDobEnd = targetDobStart.AddDays(1);

            var req = await _context.Set<StudentRegistrationRequest>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.ParentPhone == cleanPhone &&
                                          r.DateOfBirth >= targetDobStart && r.DateOfBirth < targetDobEnd);

            if (req == null)
            {
                await RecordFailedAttemptAsync(clientIp, cleanPhone, ipFailed, ipKeyFailed);
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ cần bổ sung hoặc thông tin xác thực không khớp.";
                return RedirectToAction("CheckStatus");
            }

            await ClearFailedAttemptsAsync(clientIp, cleanPhone);

            if (req.Status == "APPROVED")
            {
                TempData["ErrorMessage"] = "Hồ sơ này đã được phê duyệt chính thức, không thể chỉnh sửa.";
                return RedirectToAction("CheckStatus", new { phone = cleanPhone, dob = targetDobStart.ToString("yyyy-MM-dd") });
            }

            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            ViewBag.CurrentYearName = currentYear?.Name ?? VietnamTimeHelper.Today.Year.ToString();

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
                _logger.LogWarning("SECURITY: Honeypot triggered on SubmitResubmit from IP {IP}", GetNormalizedClientIp());
                return Json(new { success = true, message = "Hồ sơ đã được cập nhật." });
            }

            if (!ModelState.IsValid)
            {
                var firstErr = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                               ?? "Dữ liệu bổ sung hồ sơ không hợp lệ.";
                return BadRequest(new { success = false, message = firstErr });
            }

            string clientIp = GetNormalizedClientIp();
            string cleanOrigPhone = Regex.Replace((dto.OriginalParentPhone ?? string.Empty).Trim(), @"\s+", "");
            var (isLockedOut, ipFailed, ipKeyFailed) = await CheckLockoutAsync(clientIp, cleanOrigPhone);
            if (isLockedOut)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    success = false,
                    message = "Thiết bị này đã nhập sai thông tin quá 5 lần liên tiếp. Vui lòng thử lại sau 15 phút!"
                });
            }

            var origDobStart = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dto.OriginalDateOfBirth);
            var origDobEnd = origDobStart.AddDays(1);

            var req = await _context.Set<StudentRegistrationRequest>()
                .FirstOrDefaultAsync(r => r.Id == dto.Id && r.ParentPhone == cleanOrigPhone &&
                                          r.DateOfBirth >= origDobStart && r.DateOfBirth < origDobEnd);

            if (req == null)
            {
                await RecordFailedAttemptAsync(clientIp, cleanOrigPhone, ipFailed, ipKeyFailed);
                return BadRequest(new
                {
                    success = false,
                    message = "Không tìm thấy hồ sơ cần cập nhật hoặc thông tin xác thực không hợp lệ."
                });
            }

            await ClearFailedAttemptsAsync(clientIp, cleanOrigPhone);

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
                var uploadResult = await SaveValidatedCertificateFileAsync(dto.CertificateFile);
                if (!uploadResult.Success)
                {
                    return BadRequest(new { success = false, message = uploadResult.ErrorMessage });
                }

                // Xóa tệp cũ nếu có để tiết kiệm dung lượng đĩa
                if (!string.IsNullOrWhiteSpace(req.BaptismCertificatePath))
                {
                    var privateUploadDir = GetCertificatesDirectory();
                    if (FileUploadSecurityHelper.TryResolveContainedPath(privateUploadDir, req.BaptismCertificatePath, out var oldFullPath)
                        && System.IO.File.Exists(oldFullPath))
                    {
                        try { System.IO.File.Delete(oldFullPath); } catch { /* Bỏ qua lỗi xóa file cũ */ }
                    }
                }

                req.BaptismCertificatePath = uploadResult.SavedFileName;
            }

            string cleanNewPhone = Regex.Replace(dto.ParentPhone.Trim(), @"\s+", "");

            req.ChristianName = dto.ChristianName.Trim();
            req.FirstName = dto.FirstName.Trim();
            req.LastName = dto.LastName.Trim();
            req.Gender = dto.Gender == "Nữ" ? "Nữ" : "Nam";
            req.DateOfBirth = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dto.DateOfBirth);
            req.BaptismDate = dto.BaptismDate.HasValue
                ? VietnamTimeHelper.NormalizeBusinessDateUtcKind(dto.BaptismDate.Value)
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

            string clientIp = GetNormalizedClientIp();
            string cleanPhone = Regex.Replace(phone.Trim(), @"\s+", "");
            var (isLockedOut, ipFailed, ipKeyFailed) = await CheckLockoutAsync(clientIp, cleanPhone);
            if (isLockedOut)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests);
            }

            var targetDobStart = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dob.Value);
            var targetDobEnd = targetDobStart.AddDays(1);

            var req = await _context.Set<StudentRegistrationRequest>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.ParentPhone == cleanPhone &&
                                          r.DateOfBirth >= targetDobStart && r.DateOfBirth < targetDobEnd);

            if (req == null)
            {
                await RecordFailedAttemptAsync(clientIp, cleanPhone, ipFailed, ipKeyFailed);
                return NotFound();
            }

            await ClearFailedAttemptsAsync(clientIp, cleanPhone);

            if (string.IsNullOrWhiteSpace(req.BaptismCertificatePath))
            {
                return NotFound();
            }

            string privateUploadDir = GetCertificatesDirectory();
            if (!FileUploadSecurityHelper.TryResolveContainedPath(privateUploadDir, req.BaptismCertificatePath, out string fullPath)
                || !System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            string safeFileName = Path.GetFileName(fullPath);
            string ext = Path.GetExtension(safeFileName).ToLowerInvariant();
            string contentType = FileUploadSecurityHelper.GetSafeMimeType(ext);

            if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers["Content-Security-Policy"] = "sandbox";
                return PhysicalFile(fullPath, contentType, safeFileName);
            }

            return PhysicalFile(fullPath, contentType);
        }

        private string GetCertificatesDirectory() =>
            Path.Combine(_env.ContentRootPath, "PrivateUploads", "baptism_certs");

        private async Task<(bool Success, string? SavedFileName, string? ErrorMessage)> SaveValidatedCertificateFileAsync(IFormFile file)
        {
            if (file.Length > MaxFileSize)
            {
                return (false, null, "Kích thước ảnh Giấy Rửa Tội không được vượt quá 5MB.");
            }

            var ext = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
            if (!AllowedCertExtensions.Contains(ext))
            {
                return (false, null, "Chỉ chấp nhận tệp định dạng JPG, PNG hoặc PDF.");
            }

            if (!await FileUploadSecurityHelper.ValidateFormFileMagicBytesAsync(file, ext))
            {
                _logger.LogWarning("SECURITY: Invalid file magic bytes uploaded to PublicRegistration from IP {IP}", GetNormalizedClientIp());
                return (false, null, "Nội dung tệp tải lên không hợp lệ hoặc bị hỏng.");
            }

            var privateUploadDir = GetCertificatesDirectory();
            Directory.CreateDirectory(privateUploadDir);

            if (ext == ".pdf")
            {
                var pdfFileName = $"cert_{VietnamTimeHelper.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.pdf";
                if (!FileUploadSecurityHelper.TryResolveContainedPath(privateUploadDir, pdfFileName, out var fullPdfPath))
                {
                    return (false, null, "Đường dẫn lưu tệp không hợp lệ.");
                }

                await using var inputStream = file.OpenReadStream();
                await using var fileStream = new FileStream(fullPdfPath, FileMode.Create);
                await inputStream.CopyToAsync(fileStream);
                return (true, pdfFileName, null);
            }

            try
            {
                await using var rawImageStream = file.OpenReadStream();
                var optimized = await _imageOptimizationService.OptimizeToWebpAsync(
                    rawImageStream,
                    maxWidth: 1600,
                    maxHeight: 1600,
                    quality: 78);

                await using var optimizedStream = optimized.OptimizedStream;
                var webpFileName = $"cert_{VietnamTimeHelper.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{optimized.FileExtension}";
                if (!FileUploadSecurityHelper.TryResolveContainedPath(privateUploadDir, webpFileName, out var fullImagePath))
                {
                    return (false, null, "Đường dẫn lưu tệp không hợp lệ.");
                }

                await using var fileStream = new FileStream(fullImagePath, FileMode.Create);
                await optimizedStream.CopyToAsync(fileStream);
                return (true, webpFileName, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SECURITY: Failed to re-encode certificate image from IP {IP}", GetNormalizedClientIp());
                return (false, null, "Nội dung tệp tải lên không hợp lệ hoặc bị hỏng.");
            }
        }

        private string GetNormalizedClientIp()
        {
            var ip = HttpContext.Connection.RemoteIpAddress;
            if (ip == null) return "unknown_ip";
            return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();
        }

        private static string GetIpLockoutKey(string clientIp) => $"pubreg:fail:ip:{clientIp}";

        private static string GetIpKeyLockoutKey(string clientIp, string key) =>
            $"pubreg:fail:ip_key:{clientIp}:{key}";

        private async Task<(bool IsLockedOut, int IpFailed, int IpKeyFailed)> CheckLockoutAsync(string clientIp, string scopeKey)
        {
            int ipFailed = await _cache.GetAsync<int>(GetIpLockoutKey(clientIp));
            int ipKeyFailed = string.IsNullOrEmpty(scopeKey)
                ? 0
                : await _cache.GetAsync<int>(GetIpKeyLockoutKey(clientIp, scopeKey));
            return (ipFailed >= MaxFailedAttempts || ipKeyFailed >= MaxFailedAttempts, ipFailed, ipKeyFailed);
        }

        private async Task RecordFailedAttemptAsync(string clientIp, string scopeKey, int currentIpFailed, int currentIpKeyFailed)
        {
            await _cache.SetAsync(GetIpLockoutKey(clientIp), currentIpFailed + 1, TimeSpan.FromMinutes(15));
            if (!string.IsNullOrEmpty(scopeKey))
            {
                await _cache.SetAsync(GetIpKeyLockoutKey(clientIp, scopeKey), currentIpKeyFailed + 1, TimeSpan.FromMinutes(30));
            }
        }

        private async Task ClearFailedAttemptsAsync(string clientIp, string scopeKey)
        {
            await _cache.RemoveAsync(GetIpLockoutKey(clientIp));
            if (!string.IsNullOrEmpty(scopeKey))
            {
                await _cache.RemoveAsync(GetIpKeyLockoutKey(clientIp, scopeKey));
            }
        }
    }
}