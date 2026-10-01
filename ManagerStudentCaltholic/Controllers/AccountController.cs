using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    public class AccountController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IPasswordHasherService _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ParishDbContext context, IPasswordHasherService passwordHasher, ITokenService tokenService, ILogger<AccountController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _logger = logger;
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
            return View();
        }

        /// <summary>
        /// 2. POST: /Account/Login (TASK-605)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyAuth)] // Chặn spam đăng nhập
        [IgnoreAntiforgeryToken] // Bỏ qua kiểm tra Anti-CSRF riêng cho form đăng nhập ban đầu
        public async Task<IActionResult> Login(LoginRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new AuthResponseDto { Success = false, Message = "Dữ liệu không hợp lệ." });
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
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
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
                SameSite = SameSiteMode.Lax,
                Expires = expires
            };
            Response.Cookies.Append("refreshToken", token, cookieOptions);
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
        /// </summary>
        /// <returns></returns
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction(nameof(Login));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
            if (user == null) return NotFound("Không tìm thấy thông tin tài khoản.");

            // Lấy danh sách lớp được phân công (tương ứng với TeacherName hoặc FullName)
            var assignedClasses = await _context.ClassTeachers
                .Include(ct => ct.ClassRoom)
                    .ThenInclude(c => c.AcademicYear)
                .Include(ct => ct.ClassRoom)
                    .ThenInclude(c => c.Enrollments)
                .Where(ct => ct.TeacherName.ToLower() == user.Username.ToLower() ||
                             ct.TeacherName.ToLower() == user.FirstName.ToLower() + " " + user.LastName.ToLower() ||
                             (!string.IsNullOrEmpty(user.PhoneNumber) && ct.PhoneNumber == user.PhoneNumber))
                .OrderByDescending(ct => ct.ClassRoom.AcademicYear.StartDate)
                .Select(ct => new AssignedClassItemDto
                {
                    ClassId = ct.ClassRoomId,
                    ClassName = ct.ClassRoom.Name,
                    GradeLevel = ct.ClassRoom.GradeLevel,
                    AcademicYearName = ct.ClassRoom.AcademicYear.Name,
                    RoleInClass = ct.RoleInClass == "HEAD" ? "Chủ nhiệm" : (ct.RoleInClass == "MEMBER" ? "Đồng hành" : "Dự bị"),
                    RoomNumber = ct.ClassRoom.RoomName,
                    TotalStudents = ct.ClassRoom.Enrollments.Count
                })
                .ToListAsync();

            var viewModel = new UserProfileViewModel
            {
                Id = user.Id,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ChristianName = user.ChristianName,
                DateOfBirth = user.DateOfBirth,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role,
                LastLoginAt = user.LastLoginAt,
                AssignedClasses = assignedClasses
            };

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
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username!.ToLower());
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu nhập chưa hợp lệ, vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Profile));
            }

            user.ChristianName = model.ChristianName?.Trim();
            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            user.DateOfBirth = model.DateOfBirth.HasValue
                ? DateTime.SpecifyKind(model.DateOfBirth.Value.Date, DateTimeKind.Utc)
                : null;
            user.Email = model.Email?.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            user.Address = model.Address?.Trim();

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật hồ sơ cá nhân thành công!";
            return RedirectToAction(nameof(Profile));
        }

        /// <summary>
        /// 8. POST: /Account/ChangePassword (Ajax) (TASK-616)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
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

            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username!.ToLower());
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

            _logger.LogInformation("Người dùng {Username} đã đổi mật khẩu thành công.", user.Username);

            return Json(new { success = true, message = "Đổi mật khẩu thành công! Vui lòng ghi nhớ mật khẩu mới." });
        }
    }
}

