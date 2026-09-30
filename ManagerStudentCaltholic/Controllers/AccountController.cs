using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Login(LoginRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var cleanUsername = model.Username.Trim().ToLower();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername);

            // Kiểm tra tồn tại và kích hoạt
            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
                return View(model);
            }

            // Kiểm tra trạng thái khóa tài khoản (nếu bị khóa bởi Epic 7)
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
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    _logger.LogWarning("Tài khoản {Username} bị tạm khóa 15 phút do nhập sai 5 lần.", user.Username);
                }
                await _context.SaveChangesAsync();

                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
                return View(model);
            }

            // Đăng nhập thành công: Reset số lần nhập sai
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTime.UtcNow;

            // 1. Tạo Cookie Authentication Claims cho Razor View
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role)
            };

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
                        FullName = user.FullName,
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
    }
}

