using ClosedXML.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
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
                .OrderByDescending(u => u.CreatedAt).Where(x => x.Role != UserRole.Admin)
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
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> Create([FromBody] CreateUserRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                // Thu thập toàn bộ thông báo lỗi validation nếu có
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                var str = "Dữ liệu nhập không hợp lệ: " + string.Join("; ", errors);

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
                CreatedAt = DateTime.UtcNow,
                // Gán khối phụ trách nếu là BranchHead
                ManagedGradeLevel = model.Role == UserRole.BranchHead ? model.ManagedGradeLevel : null
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

        #region Batch Create Teachers
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public IActionResult DownloadTeacherTemplate()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DS_GLV_TuyenHua");

            string[] headers = {
        "Tên Thánh (*)",
        "Họ và Tên Đệm (*)",
        "Tên (*)",
        "Số Điện Thoại (*)",
        "Email",
        "Khối Dự Kiến (Khai Tâm, Rước Lễ, Thêm Sức, Bao Đồng)"
    };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 242);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Dòng dữ liệu mẫu
            worksheet.Cell(2, 1).Value = "Phaolô";
            worksheet.Cell(2, 2).Value = "Nguyễn Văn";
            worksheet.Cell(2, 3).Value = "An";
            worksheet.Cell(2, 4).Value = "0901234567";
            worksheet.Cell(2, 5).Value = "an.nguyen@gmail.com";
            worksheet.Cell(2, 6).Value = "Khai Tâm";

            worksheet.Cell(3, 1).Value = "Maria";
            worksheet.Cell(3, 2).Value = "Trần Thị";
            worksheet.Cell(3, 3).Value = "Bình";
            worksheet.Cell(3, 4).Value = "0987654321";
            worksheet.Cell(3, 5).Value = "";
            worksheet.Cell(3, 6).Value = "Rước Lễ";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_DanhSach_GLV_Moi.xlsx");
        }

        // =========================================================================
        // 2. API BATCH IMPORT GLV TỪ FILE EXCEL (TASK-814)
        // =========================================================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> ImportTeachersFromExcel(IFormFile file, [FromForm] string? defaultPassword)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file Excel danh sách GLV." });
            }

            var teachers = new List<BatchTeacherItemDto>();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    return Json(new { success = false, message = "File Excel không chứa bất kỳ trang tính nào." });
                }

                var rows = worksheet.RangeUsed()?.RowsUsed()?.Skip(1);
                if (rows == null || !rows.Any())
                {
                    return Json(new { success = false, message = "File Excel không có dữ liệu để nạp." });
                }

                foreach (var row in rows)
                {
                    var christian = row.Cell(1).GetString().Trim();
                    var first = row.Cell(2).GetString().Trim();
                    var last = row.Cell(3).GetString().Trim();
                    var phone = row.Cell(4).GetString().Trim();
                    var email = row.Cell(5).GetString().Trim();
                    var grade = row.Cell(6).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(christian) && string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last))
                        continue;

                    teachers.Add(new BatchTeacherItemDto
                    {
                        ChristianName = christian,
                        FirstName = first,
                        LastName = last,
                        PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email,
                        ManagedGradeLevel = string.IsNullOrWhiteSpace(grade) ? null : grade
                    });
                }
            }

            var request = new BatchCreateTeachersRequestDto
            {
                DefaultPassword = string.IsNullOrWhiteSpace(defaultPassword) ? "GLV@2026!" : defaultPassword.Trim(),
                Teachers = teachers
            };

            return await ExecuteBatchCreate(request);
        }

        // =========================================================================
        // 3. API TẠO HÀNG LOẠT GLV (BATCH JSON) (TASK-814)
        // =========================================================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> BatchCreateTeachers([FromBody] BatchCreateTeachersRequestDto request)
        {
            return await ExecuteBatchCreate(request);
        }

        // Logic thực thi chung bọc trong Execution Strategy và Transaction an toàn
        private async Task<IActionResult> ExecuteBatchCreate(BatchCreateTeachersRequestDto request)
        {
            if (request == null || !request.Teachers.Any())
            {
                return Json(new BatchCreateTeachersResultDto { Success = false, Message = "Danh sách GLV để tạo rỗng." });
            }

            var result = new BatchCreateTeachersResultDto();
            var defaultPass = string.IsNullOrWhiteSpace(request.DefaultPassword) ? "GLV@2026!" : request.DefaultPassword.Trim();
            var passwordHash = _passwordHasher.HashPassword(defaultPass);

            // Lấy tập hợp usernames và phones đã tồn tại trong CSDL để chống trùng
            var existingUsernames = await _context.Users.Select(u => u.Username.ToLower()).ToListAsync();
            var usernameSet = new HashSet<string>(existingUsernames);

            var usersToInsert = new List<User>();

            foreach (var item in request.Teachers)
            {
                if (string.IsNullOrWhiteSpace(item.FirstName) || string.IsNullOrWhiteSpace(item.LastName))
                {
                    result.Errors.Add($"Bỏ qua dòng: Thiếu Họ hoặc Tên ({item.ChristianName} {item.FirstName} {item.LastName})");
                    continue;
                }

                // 1. Thuật toán sinh Username: "glv_" + chữ cái đầu FirstName + LastName
                // Ví dụ: Nguyễn Văn An -> "glv_nvan"
                var cleanFirst = VietnameseStringHelper.RemoveDiacritics(item.FirstName);
                var cleanLast = VietnameseStringHelper.RemoveDiacritics(item.LastName);

                var firstLetters = string.Concat(cleanFirst.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));
                var baseUsername = $"glv_{firstLetters}{cleanLast}".Replace(" ", "");

                var finalUsername = baseUsername;
                int counter = 1;
                while (usernameSet.Contains(finalUsername))
                {
                    finalUsername = $"{baseUsername}{counter++}";
                }
                usernameSet.Add(finalUsername); // Đánh dấu đã dùng

                var fullName = $"{item.FirstName.Trim()} {item.LastName.Trim()}";

                var newUser = new User
                {
                    Username = finalUsername,
                    PasswordHash = passwordHash,
                    ChristianName = item.ChristianName?.Trim(),
                    FirstName = item.FirstName.Trim(),
                    LastName = item.LastName.Trim(),
                    PhoneNumber = item.PhoneNumber?.Trim(),
                    Email = item.Email?.Trim(),
                    Role = UserRole.Teacher,
                    ManagedGradeLevel = item.ManagedGradeLevel?.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                usersToInsert.Add(newUser);

                result.CreatedTeachers.Add(new CreatedTeacherResultItem
                {
                    Username = finalUsername,
                    FullName = fullName,
                    ChristianName = item.ChristianName ?? "",
                    PhoneNumber = item.PhoneNumber ?? "",
                    InitialPassword = defaultPass,
                    ManagedGradeLevel = item.ManagedGradeLevel
                });
            }

            if (!usersToInsert.Any())
            {
                result.Success = false;
                result.Message = "Không có tài khoản nào hợp lệ để tạo mới.";
                return Json(result);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    await _context.Users.AddRangeAsync(usersToInsert);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                result.Success = true;
                result.TotalCreated = usersToInsert.Count;
                result.Message = $"Đã tạo thành công {usersToInsert.Count} tài khoản Giáo lý viên mới!";
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new BatchCreateTeachersResultDto
                {
                    Success = false,
                    Message = "Lỗi khi lưu tài khoản vào Database: " + ex.Message
                });
            }
            #endregion
        }
    }
}
