using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireExecutiveBoard")]
    public class UsersController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IPasswordHasherService _passwordHasher;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            ParishDbContext context,
            IPasswordHasherService passwordHasher,
            ILogger<UsersController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        
        /// <summary>
        /// 1. GET: /Users
        /// </summary>
        /// <param name="role"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(string? role, string? search)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(role) && role != "ALL")
            {
                query = query.Where(u => u.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(s) ||
                                         u.FirstName.ToLower().Contains(s) ||
                                         u.LastName.ToLower().Contains(s) ||
                                         (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt).Where(x=>x.Role != UserRole.Admin)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.SelectedRole = role ?? "ALL";
            ViewBag.SearchKeyword = search;
            return View(users);
        }

        /// <summary>
        /// 2. POST: /Users/Create (Ajax)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateUserRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Dữ liệu nhập không hợp lệ." });
            }

            var usernameClean = model.Username.Trim().ToLower();
            var isExist = await _context.Users.AnyAsync(u => u.Username.ToLower() == usernameClean);
            if (isExist)
            {
                return Json(new { success = false, message = $"Tên đăng nhập '{model.Username}' đã tồn tại." });
            }

            // Chỉ Admin mới được quyền cấp tài khoản vai trò Admin hoặc Cha Tuyên Úy
            if ((model.Role == UserRole.Admin || model.Role == UserRole.SpiritualDirector) && !User.IsInRole(UserRole.Admin))
            {
                return Json(new { success = false, message = "Chỉ Admin hệ thống mới có quyền tạo tài khoản Quản trị viên hoặc Cha Tuyên Úy." });
            }

            var newUser = new User
            {
                Username = usernameClean,
                PasswordHash = _passwordHasher.HashPassword(model.Password),
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                ChristianName = model.ChristianName.Trim(),
                Email = model.Email?.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                Role = model.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Người dùng {CurrentUser} đã tạo tài khoản mới: {NewUsername} ({Role})",
                User.Identity?.Name, newUser.Username, newUser.Role);

            return Json(new { success = true, message = $"Đã tạo thành công tài khoản '{newUser.Username}'!" });
        }

        /// <summary>
        /// 3. POST: /Users/ResetPassword (Ajax)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản yêu cầu." });
            }

            if (user.Role == UserRole.Admin && !User.IsInRole(UserRole.Admin))
            {
                return Json(new { success = false, message = "Bạn không có quyền đổi mật khẩu của Admin." });
            }

            user.PasswordHash = _passwordHasher.HashPassword(model.NewPassword);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Mật khẩu tài khoản ID {UserId} ({Username}) đã được đặt lại bởi {CurrentUser}",
                user.Id, user.Username, User.Identity?.Name);

            return Json(new { success = true, message = $"Đặt lại mật khẩu cho '{user.Username}' thành công!" });
        }

        /// <summary>
        /// 4. POST: /Users/ToggleStatus (Ajax)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(long id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản." });
            }

            // Chống tự khóa tài khoản chính mình
            if (user.Username.ToLower() == User.Identity?.Name?.ToLower())
            {
                return Json(new { success = false, message = "Bạn không thể tự vô hiệu hóa tài khoản của chính mình." });
            }

            user.IsActive = !user.IsActive;
            if (user.IsActive)
            {
                user.LockoutEnd = null;
                user.AccessFailedCount = 0;
            }

            await _context.SaveChangesAsync();

            var statusStr = user.IsActive ? "kích hoạt" : "vô hiệu hóa";
            return Json(new { success = true, isActive = user.IsActive, message = $"Đã {statusStr} tài khoản {user.Username}!" });
        }
    }
}
