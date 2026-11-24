using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Options;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    public class AccountController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IPasswordHasherService _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AccountController> _logger;
        private readonly IProfileHistoryService _profileHistoryService;
        private readonly IGoogleDriveStorageService _storageService;
        private readonly IImageOptimizationService _imageOptimizationService;
        private readonly GoogleDriveOptions _driveOptions;

        public AccountController(
            ParishDbContext context,
            IPasswordHasherService passwordHasher,
            ITokenService tokenService,
            ILogger<AccountController> logger,
            IProfileHistoryService profileHistoryService,
            IGoogleDriveStorageService storageService,
            IImageOptimizationService imageOptimizationService,
            IOptions<GoogleDriveOptions> driveOptions)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _logger = logger;
            _profileHistoryService = profileHistoryService;
            _storageService = storageService;
            _imageOptimizationService = imageOptimizationService;
            _driveOptions = driveOptions.Value;
        }
        /// <summary>
        /// 1. GET: /Account/Login
        /// </summary>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginRequestDto { RememberMe = true, ReturnUrl = returnUrl });
        }

        /// <summary>
        /// 2. POST: /Account/Login (TASK-605)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken] // SEC-02 FIX: Bật lại kiểm tra Anti-CSRF (đã xóa [IgnoreAntiforgeryToken])
        [EnableRateLimiting(SecurityExtensions.PolicyAuth)] // Chặn spam đăng nhập
        public async Task<IActionResult> Login(LoginRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                // P2-SEC-07 FIX: Chỉ trả JSON nếu gọi bằng Ajax, còn Form POST thông thường thì trả về View(model)
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new AuthResponseDto { Success = false, Message = "Vui lòng nhập đầy đủ Tên đăng nhập và Mật khẩu." });
                }
                return View(model);
            }

            var cleanUsername = model.Username.Trim().ToLower();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername);

            // Kiểm tra tồn tại và kích hoạt
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
                return View(model);
            }

            // 1. Kiểm tra nếu tài khoản bị Admin vô hiệu hóa vĩnh viễn
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đang bị tạm khóa bởi Ban Quản Trị.");
                return View(model);
            }

            // Kiểm tra trạng thái khóa tài khoản (nếu bị khóa bởi Epic 7) 702
            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            {
                var remainingMinutes = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                ModelState.AddModelError(string.Empty, $"Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau {remainingMinutes} phút.");
                return View(model);
            }

            // Kiểm tra mật khẩu bằng thuật toán PBKDF2
            var isPasswordValid = _passwordHasher.VerifyPassword(model.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(30); // Khóa 30 phút
                    _logger.LogWarning("Tài khoản {Username} bị tạm khóa 30 phút do nhập sai 5 lần.", user.Username);
                    await _context.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty, $"Tài khoản {user.Username} bị tạm khóa 30 phút do nhập sai 5 lần.");
                    return View(model);
                }

                int attemptsLeft = 5 - user.AccessFailedCount;
                ModelState.AddModelError(string.Empty, $"Mật khẩu không chính xác. Bạn còn {attemptsLeft} lần thử trước khi tài khoản bị tạm khóa 30 phút.");
                await _context.SaveChangesAsync();
                return View(model);
            }

            // Đăng nhập thành công: Reset số lần nhập sai
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // 1. Tạo Cookie Authentication Claims cho Razor View
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", user.FirstName + " " + user.LastName),
                new Claim(ClaimTypes.Role, user.Role)
            };
            if (!string.IsNullOrEmpty(user.ManagedGradeLevel))
            {
                claims.Add(new Claim("ManagedGradeLevel", user.ManagedGradeLevel));
            }

            if (!string.IsNullOrEmpty(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddDays(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // 2. Tạo Refresh Token lưu DB và ghi vào HttpOnly Cookie
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var refreshToken = _tokenService.GenerateRefreshToken(user.Id, clientIp);
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            SetRefreshTokenCookie(refreshToken.Token, refreshToken.ExpiresAt);

            _logger.LogInformation("Người dùng {Username} ({Role}) đã đăng nhập thành công.", user.Username, user.Role);

            // Nếu là request Ajax (Header X-Requested-With) thì trả JSON kèm AccessToken
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var accessToken = _tokenService.GenerateAccessToken(user);
                return Json(new AuthResponseDto
                {
                    Success = true,
                    Message = "Đăng nhập thành công",
                    AccessToken = accessToken,
                    User = new UserInfoDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FirstName + " " + user.LastName,
                        Role = user.Role,
                        Email = user.Email
                    }
                });
            }

            return RedirectToLocal(model.ReturnUrl);
        }

       
        /// <summary>
        /// 3. POST: /Account/RefreshToken (TASK-606)
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken()
        {
            var token = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { success = false, message = "Không tìm thấy phiên đăng nhập để làm mới." });
            }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _tokenService.RotateRefreshTokenAsync(token, clientIp);

            if (!result.Success || result.NewRefreshToken == null)
            {
                Response.Cookies.Delete("refreshToken");
                return Unauthorized(new { success = false, message = result.Message });
            }

            SetRefreshTokenCookie(result.NewRefreshToken.Token, result.NewRefreshToken.ExpiresAt);

            return Ok(new
            {
                success = true,
                accessToken = result.AccessToken,
                message = "Phiên làm việc đã được gia hạn thành công."
            });
        }

        /// <summary>
        /// 4. POST: /Account/Logout (TASK-613)
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var token = Request.Cookies["refreshToken"];
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (!string.IsNullOrEmpty(token))
            {
                await _tokenService.RevokeTokenAsync(token, clientIp);
                Response.Cookies.Delete("refreshToken");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation("Người dùng {User} đã đăng xuất.", User.Identity?.Name ?? "Anonymous");
            return RedirectToAction("Login", "Account");
        }

        /// <summary>
        /// 5. GET: /Account/AccessDenied
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        #region Helpers
        private void SetRefreshTokenCookie(string token, DateTime expires)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/Account",
                Expires = expires
            };
            Response.Cookies.Append("refreshToken", token, cookieOptions);

            // P2-LOG-18 FIX: Đánh dấu thiết bị này thuộc về Nhân sự / Giáo lý viên (hiệu lực 180 ngày trên toàn trang "/")
            // Không xóa cookie này khi Logout thường để màn hình Trang chủ/Topbar luôn ưu tiên hiện nút "Quét QR Điểm Danh"
            Response.Cookies.Append("Parish_Staff_Device", "1", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(180),
                IsEssential = true
            });
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }
        #endregion

        /// <summary>
        /// 6. GET: /Account/Profile (TASK-616)
        /// Hỗ trợ xem hồ sơ cá nhân hoặc xem hồ sơ của GLV khác (Admin / BĐH / Cha Tuyên Úy)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile(long? id = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var currentUserId))
            {
                return RedirectToAction(nameof(Login));
            }

            long targetUserId = currentUserId;
            bool isViewingOther = false;
            if (id.HasValue && id.Value > 0 && id.Value != currentUserId)
            {
                // Kiểm tra phân quyền: Chỉ Admin, Ban Điều Hành hoặc Cha Tuyên Úy mới được xem hồ sơ người khác
                if (User.IsInRole(UserRole.Admin) || User.IsInRole(UserRole.ExecutiveBoard) || User.IsInRole(UserRole.SpiritualDirector))
                {
                    targetUserId = id.Value;
                    isViewingOther = true;
                }
                else
                {
                    return Forbid();
                }
            }

            // TASK-04.1 (Phase 6): Tổng hợp Profile Lịch sử Kép (Lớp học + Lớp dạy đa niên khóa) & 5 Cột mốc Đức tin
            var viewModel = await _profileHistoryService.BuildUserProfileAsync(targetUserId);
            if (viewModel == null) return NotFound("Không tìm thấy thông tin tài khoản.");

            ViewBag.IsViewingOther = isViewingOther;
            ViewBag.TargetUserId = targetUserId;

            return View(viewModel);
        }

        /// <summary>
        /// 7. POST: /Account/UpdateProfile (TASK-616)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind("Id,ChristianName,FirstName,LastName,DateOfBirth,Email,PhoneNumber,Address,FeastDay,BaptismDate,FirstCommunionDate,ConfirmationDate,BaoDongPromiseDate,CatechistPromiseDate")] UserProfileViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var currentUserId))
            {
                return RedirectToAction(nameof(Login));
            }

            long targetUserId = currentUserId;
            bool isViewingOther = false;
            if (model.Id > 0 && model.Id != currentUserId)
            {
                if (User.IsInRole(UserRole.Admin) || User.IsInRole(UserRole.ExecutiveBoard))
                {
                    targetUserId = model.Id;
                    isViewingOther = true;
                }
                else
                {
                    return Forbid();
                }
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId && u.IsActive);
            if (user == null) return NotFound();

            // Bỏ qua kiểm tra ModelState đối với các trường chỉ hiển thị không gửi lên từ Form
            ModelState.Remove(nameof(UserProfileViewModel.Username));
            ModelState.Remove(nameof(UserProfileViewModel.Role));

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu nhập chưa hợp lệ, vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Profile), isViewingOther ? new { id = targetUserId } : null);
            }

            static DateTime? ToUtcDate(DateTime? d) =>
                d.HasValue ? DateTime.SpecifyKind(d.Value.Date, DateTimeKind.Utc) : null;

            bool isLeadership = User.IsInRole(UserRole.Admin) ||
                                User.IsInRole(UserRole.SpiritualDirector) ||
                                User.IsInRole(UserRole.ExecutiveBoard);

            long? linkedStudentId = user.LinkedStudentId ?? user.StudentId;
            Student? linkedStudent = null;
            if (linkedStudentId.HasValue && linkedStudentId.Value > 0)
            {
                linkedStudent = await _context.Students
                    .FirstOrDefaultAsync(s => s.Id == linkedStudentId.Value);
            }

            var existingDobUtc = ToUtcDate(user.DateOfBirth ?? linkedStudent?.DateOfBirth);
            var existingPromiseUtc = ToUtcDate(user.CatechistPromiseDate ?? linkedStudent?.CatechistPromiseDate);
            var submittedDobUtc = ToUtcDate(model.DateOfBirth);
            var submittedPromiseUtc = ToUtcDate(model.CatechistPromiseDate);

            if (!isLeadership)
            {
                bool hasForm = Request != null && Request.HasFormContentType;
                bool dobClearedInForm = existingDobUtc.HasValue && hasForm && Request!.Form.ContainsKey("DateOfBirth") && !model.DateOfBirth.HasValue;
                bool promiseClearedInForm = existingPromiseUtc.HasValue && hasForm && Request!.Form.ContainsKey("CatechistPromiseDate") && !model.CatechistPromiseDate.HasValue;

                if ((model.DateOfBirth.HasValue && submittedDobUtc != existingDobUtc) ||
                    dobClearedInForm ||
                    (model.CatechistPromiseDate.HasValue && submittedPromiseUtc != existingPromiseUtc) ||
                    promiseClearedInForm)
                {
                    TempData["ErrorMessage"] = "Chỉ Quản trị viên hoặc Ban Điều Hành mới được phép thay đổi Ngày sinh và Ngày Tuyên hứa Giáo lý viên.";
                    return RedirectToAction(nameof(Profile), isViewingOther ? new { id = targetUserId } : null);
                }
            }

            bool dobExplicitlyCleared = isLeadership &&
                Request != null &&
                Request.HasFormContentType &&
                Request.Form.ContainsKey("DateOfBirth") &&
                !model.DateOfBirth.HasValue;

            DateTime? effectiveDob = isLeadership
                ? (dobExplicitlyCleared ? null : (submittedDobUtc ?? existingDobUtc))
                : existingDobUtc;

            DateTime? effectivePromiseDate = isLeadership
                ? submittedPromiseUtc
                : existingPromiseUtc;

            if (effectivePromiseDate.HasValue && (isLeadership || model.CatechistPromiseDate.HasValue))
            {
                if (!effectiveDob.HasValue)
                {
                    TempData["ErrorMessage"] = "Phải có Ngày sinh hợp lệ trước khi ghi nhận Ngày Tuyên hứa Giáo lý viên.";
                    return RedirectToAction(nameof(Profile), isViewingOther ? new { id = targetUserId } : null);
                }

                if (!AgeValidationHelper.IsAtLeastAge(effectiveDob.Value, effectivePromiseDate.Value, 18))
                {
                    int ageAtPromise = AgeValidationHelper.CalculateAgeAtDate(effectiveDob.Value, effectivePromiseDate.Value);
                    TempData["ErrorMessage"] = $"Ngày Tuyên hứa Giáo lý viên ({effectivePromiseDate.Value:dd/MM/yyyy}) ghi nhận mới {ageAtPromise} tuổi (quy định tối thiểu 18 tuổi trở lên).";
                    return RedirectToAction(nameof(Profile), isViewingOther ? new { id = targetUserId } : null);
                }
            }

            var cleanPhone = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            var cleanEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim().ToLowerInvariant();

            // SEC-05 FIX 1: Chặn nhập Số điện thoại trùng với tài khoản khác trong hệ thống
            if (!string.IsNullOrEmpty(cleanPhone))
            {
                var phoneExists = await _context.Users
                    .AnyAsync(u => u.Id != user.Id && u.PhoneNumber == cleanPhone);
                if (phoneExists)
                {
                    TempData["ErrorMessage"] = "Số điện thoại này đã được sử dụng bởi một tài khoản khác.";
                    return RedirectToAction(nameof(Profile), isViewingOther ? new { id = targetUserId } : null);
                }
            }

            // SEC-05 FIX 2: Chặn nhập Email trùng với tài khoản khác trong hệ thống
            if (!string.IsNullOrEmpty(cleanEmail))
            {
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Id != user.Id && u.Email != null && u.Email.ToLower() == cleanEmail);
                if (emailExists)
                {
                    TempData["ErrorMessage"] = "Địa chỉ Email này đã được sử dụng bởi một tài khoản khác.";
                    return RedirectToAction(nameof(Profile), isViewingOther ? new { id = targetUserId } : null);
                }
            }

            user.ChristianName = model.ChristianName?.Trim();
            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            if (isLeadership)
            {
                user.DateOfBirth = effectiveDob;
                user.CatechistPromiseDate = effectivePromiseDate;
            }
            user.FeastDay = ToUtcDate(model.FeastDay);
            user.BaptismDate = ToUtcDate(model.BaptismDate);
            user.FirstCommunionDate = ToUtcDate(model.FirstCommunionDate);
            user.ConfirmationDate = ToUtcDate(model.ConfirmationDate);
            user.BaoDongPromiseDate = ToUtcDate(model.BaoDongPromiseDate);
            user.Email = cleanEmail;
            user.PhoneNumber = cleanPhone;
            user.Address = model.Address?.Trim();

            // Đồng bộ 2 chiều sang hồ sơ Student liên kết (nếu có)
            if (linkedStudent != null)
            {
                if (!string.IsNullOrWhiteSpace(user.ChristianName))
                {
                    linkedStudent.ChristianName = user.ChristianName.Length > 50 ? user.ChristianName[..50] : user.ChristianName;
                }
                if (!string.IsNullOrWhiteSpace(user.FirstName))
                {
                    linkedStudent.FirstName = user.FirstName.Length > 90 ? user.FirstName[..90] : user.FirstName;
                }
                if (!string.IsNullOrWhiteSpace(user.LastName))
                {
                    linkedStudent.LastName = user.LastName.Length > 10 ? user.LastName[..10] : user.LastName;
                }
                if (isLeadership)
                {
                    if (user.DateOfBirth.HasValue)
                    {
                        linkedStudent.DateOfBirth = user.DateOfBirth.Value;
                    }
                    linkedStudent.CatechistPromiseDate = user.CatechistPromiseDate;
                }

                linkedStudent.FeastDay = user.FeastDay;
                linkedStudent.BaptismDate = user.BaptismDate;
                linkedStudent.FirstCommunionDate = user.FirstCommunionDate;
                linkedStudent.ConfirmationDate = user.ConfirmationDate;
                linkedStudent.BaoDongPromiseDate = user.BaoDongPromiseDate;
            }

            // SEC-05 FIX 3: Đồng bộ lại tên và SĐT hiển thị trên các lớp mà GLV này được gán qua đúng UserId
            var assignedRecords = await _context.ClassTeachers
                .Where(ct => ct.UserId == user.Id)
                .ToListAsync();

            var updatedDisplayName = $"{user.ChristianName} {user.FirstName} {user.LastName}".Trim();
            foreach (var ct in assignedRecords)
            {
                ct.TeacherName = updatedDisplayName;
                ct.PhoneNumber = user.PhoneNumber;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = isViewingOther 
                ? $"Cập nhật hồ sơ tài khoản @{user.Username} thành công!"
                : "Cập nhật hồ sơ cá nhân thành công!";
            return RedirectToAction(nameof(Profile), isViewingOther ? new { id = targetUserId } : null);
        }

        /// <summary>
        /// 8. POST: /Account/ChangePassword (Ajax) (TASK-616)
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join("; ", errors) });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var currentUserId))
            {
                return Json(new { success = false, message = "Phiên đăng nhập không hợp lệ." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng." });
            }

            // Kiểm tra mật khẩu cũ bằng băm PBKDF2
            if (!_passwordHasher.VerifyPassword(model.CurrentPassword, user.PasswordHash))
            {
                return Json(new { success = false, message = "Mật khẩu hiện tại không chính xác." });
            }

            // Băm mật khẩu mới
            user.PasswordHash = _passwordHasher.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            // SEC-06 FIX: Thu hồi toàn bộ Refresh Token cũ trên mọi thiết bị và cấp lại Token mới cho thiết bị hiện tại
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _tokenService.RevokeAllUserTokensAsync(user.Id, clientIp, "REVOKED_ON_PASSWORD_CHANGE");

            var newRefreshToken = _tokenService.GenerateRefreshToken(user.Id, clientIp);
            _context.RefreshTokens.Add(newRefreshToken);
            await _context.SaveChangesAsync();
            SetRefreshTokenCookie(newRefreshToken.Token, newRefreshToken.ExpiresAt);

            _logger.LogInformation("Người dùng {Username} đã đổi mật khẩu và làm mới toàn bộ phiên đăng nhập.", user.Username);

            return Json(new { success = true, message = "Đổi mật khẩu thành công! Các phiên đăng nhập trên thiết bị khác đã được đăng xuất." });
        }

        /// <summary>
        /// 9. POST: /Account/UploadAvatar (FEAT-P5-08.3)
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(long? userId, IFormFile avatarFile)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var currentUserId) || currentUserId <= 0)
            {
                return Unauthorized(new { success = false, message = "Phiên đăng nhập không hợp lệ." });
            }

            if (userId.HasValue && userId.Value < 0)
            {
                return BadRequest(new { success = false, message = "Mã người dùng không hợp lệ." });
            }

            long targetUserId = currentUserId;
            if (userId.HasValue && userId.Value > 0 && userId.Value != currentUserId)
            {
                if (User.IsInRole(UserRole.Admin) || User.IsInRole(UserRole.ExecutiveBoard))
                {
                    targetUserId = userId.Value;
                }
                else
                {
                    return Forbid();
                }
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId && u.IsActive);
            if (user == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy tài khoản người dùng." });
            }

            if (avatarFile == null || avatarFile.Length <= 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn tệp ảnh đại diện hợp lệ." });
            }

            long maxAvatarBytes = _driveOptions.MaxAvatarBytes > 0 ? _driveOptions.MaxAvatarBytes : 5 * 1024 * 1024;
            if (avatarFile.Length > maxAvatarBytes)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Dung lượng ảnh đại diện vượt quá giới hạn tối đa ({maxAvatarBytes / (1024 * 1024)} MB)."
                });
            }

            var ext = Path.GetExtension(avatarFile.FileName);
            if (!_imageOptimizationService.IsSupportedImageExtension(ext))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Định dạng ảnh không hợp lệ. Chỉ chấp nhận JPG, JPEG, PNG, WebP hoặc GIF."
                });
            }

            CloudMediaFile media;
            try
            {
                await using var inputStream = avatarFile.OpenReadStream();
                var opt = await _imageOptimizationService.OptimizeToWebpAsync(
                    inputStream,
                    maxWidth: 512,
                    maxHeight: 512,
                    quality: 82,
                    ct: HttpContext.RequestAborted);

                await using var optimizedStream = opt.OptimizedStream;
                var uploadFileName = string.IsNullOrWhiteSpace(avatarFile.FileName)
                    ? $"avatar_user_{user.Id}{opt.FileExtension}"
                    : Path.ChangeExtension(Path.GetFileName(avatarFile.FileName), opt.FileExtension);

                media = await _storageService.UploadAsync(new CloudUploadRequest
                {
                    Content = optimizedStream,
                    OriginalFileName = uploadFileName,
                    ContentType = opt.ContentType,
                    Category = CloudMediaCategory.Avatar,
                    EntityName = nameof(Models.Entities.User),
                    EntityId = user.Id,
                    FolderSegments = new[] { "Students_Avatars" },
                    UploadedBy = User.Identity?.Name,
                    UploadedByUserId = currentUserId,
                    AllowLocalFallback = true
                }, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải lên ảnh đại diện cho UserId={UserId}", user.Id);
                return StatusCode(500, new { success = false, message = "Không thể lưu ảnh đại diện. Vui lòng thử lại." });
            }

            long? oldUserAvatarId = user.AvatarCloudFileId;
            long? oldStudentAvatarId = null;

            user.AvatarCloudFileId = media.Id;
            user.AvatarUrl = $"/CloudMedia/File/{media.Id}";

            long? linkedStudentId = user.LinkedStudentId ?? user.StudentId;
            if (linkedStudentId.HasValue && linkedStudentId.Value > 0)
            {
                var linkedStudent = await _context.Students
                    .FirstOrDefaultAsync(s => s.Id == linkedStudentId.Value);

                if (linkedStudent != null)
                {
                    oldStudentAvatarId = linkedStudent.AvatarCloudFileId;
                    linkedStudent.AvatarCloudFileId = media.Id;
                }
            }

            await _context.SaveChangesAsync();

            if (oldUserAvatarId.HasValue && oldUserAvatarId.Value != media.Id)
            {
                bool stillReferenced = await _context.Students.AsNoTracking()
                        .AnyAsync(s => s.AvatarCloudFileId == oldUserAvatarId.Value)
                    || await _context.Users.AsNoTracking()
                        .AnyAsync(u => u.Id != user.Id && u.AvatarCloudFileId == oldUserAvatarId.Value);

                if (!stillReferenced)
                {
                    await _storageService.DeleteAsync(oldUserAvatarId.Value);
                }
            }

            if (oldStudentAvatarId.HasValue
                && oldStudentAvatarId.Value != media.Id
                && oldStudentAvatarId.Value != oldUserAvatarId)
            {
                bool stillReferenced = await _context.Students.AsNoTracking()
                        .AnyAsync(s => s.AvatarCloudFileId == oldStudentAvatarId.Value)
                    || await _context.Users.AsNoTracking()
                        .AnyAsync(u => u.AvatarCloudFileId == oldStudentAvatarId.Value);

                if (!stillReferenced)
                {
                    await _storageService.DeleteAsync(oldStudentAvatarId.Value);
                }
            }

            _logger.LogInformation(
                "Đã cập nhật ảnh đại diện UserId={UserId} (CloudFileId={CloudFileId}) bởi UserId={ActorId}",
                user.Id, media.Id, currentUserId);

            return Json(new
            {
                success = true,
                message = "Đã cập nhật ảnh đại diện thành công!",
                data = new
                {
                    cloudFileId = media.Id,
                    proxyUrl = user.AvatarUrl
                }
            });
        }
    }
}

