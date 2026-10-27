using ClosedXML.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireExecutiveBoard")]
    public class UsersController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IPasswordHasherService _passwordHasher;
        private readonly ITokenService _tokenService; // SEC-06 FIX: Bổ sung ITokenService
        private readonly ILogger<UsersController> _logger;
        private static readonly HashSet<string> ValidRoles = new()
        {
            UserRole.Admin,
            UserRole.SpiritualDirector,
            UserRole.ExecutiveBoard,
            UserRole.BranchHead,
            UserRole.Teacher,
            UserRole.Parent
        };

        public UsersController(
            ParishDbContext context,
            IPasswordHasherService passwordHasher,
            ILogger<UsersController> logger,
            ITokenService tokenService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
            _tokenService = tokenService;
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
        [ValidateAntiForgeryToken] // SEC-02 FIX: Thay thế [IgnoreAntiforgeryToken]
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

            if (!ValidRoles.Contains(model.Role))
            {
                return Json(new { success = false, message = "Vai trò được chọn không hợp lệ." });
            }

            if (!CanManageTargetRole(model.Role))
            {
                return Json(new { success = false, message = "Chỉ Admin hoặc Cha Tuyên Úy (qua màn hình Điều hành) mới có quyền cấp tài khoản cấp cao (Admin, Cha Tuyên Úy, Ban Điều Hành)." });
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
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Dữ liệu mật khẩu mới không hợp lệ." });
            }

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản yêu cầu." });
            }

            // SEC-03 FIX: Chặn ExecutiveBoard đặt lại mật khẩu của Admin, Cha Tuyên Úy hoặc ExecutiveBoard khác
            if (!CanManageTargetRole(user.Role) && user.Username.ToLower() != User.Identity?.Name?.ToLower())
            {
                _logger.LogWarning("Cảnh báo bảo mật: {Operator} cố gắng đặt lại mật khẩu của tài khoản cấp cao {TargetUser} ({Role}).",
                    User.Identity?.Name, user.Username, user.Role);
                return Json(new { success = false, message = "Bạn không đủ thẩm quyền để đặt lại mật khẩu của tài khoản này." });
            }

            user.PasswordHash = _passwordHasher.HashPassword(model.NewPassword);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            await _context.SaveChangesAsync();

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _tokenService.RevokeAllUserTokensAsync(user.Id, clientIp, "REVOKED_ON_ADMIN_RESET_PASSWORD");

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

            // SEC-03 FIX: Chặn ExecutiveBoard vô hiệu hóa tài khoản Admin, Cha Tuyên Úy hoặc ExecutiveBoard khác
            if (!CanManageTargetRole(user.Role))
            {
                _logger.LogWarning("Cảnh báo bảo mật: {Operator} cố gắng khóa/mở khóa tài khoản cấp cao {TargetUser} ({Role}).",
                    User.Identity?.Name, user.Username, user.Role);
                return Json(new { success = false, message = "Bạn không đủ thẩm quyền để thay đổi trạng thái của tài khoản cấp cao này." });
            }

            user.IsActive = !user.IsActive;
            if (user.IsActive)
            {
                user.LockoutEnd = null;
                user.AccessFailedCount = 0;
            }

            await _context.SaveChangesAsync();

            // SEC-06 FIX: Nếu khóa tài khoản thì thu hồi ngay toàn bộ Refresh Token đang hoạt động
            if (!user.IsActive)
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _tokenService.RevokeAllUserTokensAsync(user.Id, clientIp, "DEACTIVATED_BY_ADMIN");
            }

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="defaultPassword"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> ImportTeachersFromExcel(IFormFile file, [FromForm] string? defaultPassword)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file Excel danh sách GLV." });
            }

            // SEC-10 FIX 1: Giới hạn dung lượng tối đa 5MB và chỉ cho phép định dạng .xlsx
            const long maxFileSize = 5 * 1024 * 1024; // 5 MB
            if (file.Length > maxFileSize)
            {
                return Json(new { success = false, message = "Dung lượng file Excel vượt quá giới hạn cho phép (Tối đa 5MB)." });
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx")
            {
                return Json(new { success = false, message = "Định dạng file không hợp lệ. Vui lòng sử dụng file mẫu Excel (.xlsx)." });
            }

            var teachers = new List<BatchTeacherItemDto>();

            try
            {
                using var stream = new MemoryStream();
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

                // Giới hạn số dòng mỗi lần import (tối đa 500 GLV/lần) để chống treo request
                foreach (var row in rows.Take(500))
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đọc file Excel ImportTeachersFromExcel do người dùng {User} tải lên.", User.Identity?.Name);
                return Json(new { success = false, message = "Không thể đọc nội dung file Excel. Vui lòng kiểm tra lại định dạng file mẫu." });
            }

            var request = new BatchCreateTeachersRequestDto
            {
                DefaultPassword = string.IsNullOrWhiteSpace(defaultPassword) ? null : defaultPassword.Trim(),
                Teachers = teachers
            };

            return await ExecuteBatchCreate(request);
        }

        /// <summary>
        /// 3. API TẠO HÀNG LOẠT GLV (BATCH JSON) (TASK-814)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken] // SEC-02 FIX: Thay thế [IgnoreAntiforgeryToken]
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
            var customDefaultPass = string.IsNullOrWhiteSpace(request.DefaultPassword) ? null : request.DefaultPassword.Trim();

            // Nếu có truyền mật khẩu chung thì kiểm tra độ dài tối thiểu 8 ký tự
            // SEC-NEW-10 FIX: Kiểm tra độ mạnh của mật khẩu khởi tạo chung khi tạo hàng loạt GLV
            if (customDefaultPass != null &&
                !System.Text.RegularExpressions.Regex.IsMatch(customDefaultPass, PasswordPolicy.ComplexityPattern))
            {
                return Json(new BatchCreateTeachersResultDto
                {
                    Success = false,
                    Message = PasswordPolicy.ComplexityErrorMessage
                });
            }

            // Lấy tập hợp usernames và phones đã tồn tại trong CSDL để chống trùng
            var existingUsernames = await _context.Users.Select(u => u.Username.ToLower()).ToListAsync();
            var usernameSet = new HashSet<string>(existingUsernames);

            var usersToInsert = new List<User>();

            foreach (var item in request.Teachers)
            {
                // SEC-09 FIX: Mỗi GLV có một mật khẩu ngẫu nhiên riêng nếu không chỉ định DefaultPassword
                var initialPass = customDefaultPass ?? GenerateSecureInitialPassword();
                var passwordHash = _passwordHasher.HashPassword(initialPass);

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
                    InitialPassword = initialPass, // Trả về đúng mật khẩu vừa sinh của từng GLV
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
                // SEC-10 FIX: Ghi log chi tiết ở Server, không trả ex.Message về Client
                _logger.LogError(ex, "Lỗi khi lưu danh sách tài khoản GLV hàng loạt vào Database.");
                return Json(new BatchCreateTeachersResultDto
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi hệ thống khi lưu danh sách tài khoản. Vui lòng thử lại hoặc liên hệ Quản trị viên."
                });
            }
        }

        #endregion

        #region Batch Deactivate Teachers
        /// <summary>
        /// 1. API: LẤY DANH SÁCH GLV CHƯA ĐƯỢC PHÂN CÔNG LỚP TRONG NIÊN KHÓA HIỆN TẠI (TASK-815)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> GetUnassignedTeachersForCurrentYear()
        {
            try
            {
                // 1. Lấy niên khóa hiện hành
                var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
                var currentYearId = currentYear?.Id ?? 0;

                // 2. Lấy danh sách UserId đã được phân công vào bất kỳ lớp nào trong niên khóa này
                var assignedUserIds = await _context.ClassTeachers
                    .Where(ct => ct.AcademicYearId == currentYearId && ct.UserId.HasValue)
                    .Select(ct => ct.UserId!.Value)
                    .Distinct()
                    .ToListAsync();

                // 3. Lọc các GLV đang Active nhưng chưa được phân công lớp nào
                var unassignedTeachers = await _context.Users
                    .Where(u => u.IsActive &&
                                (u.Role == UserRole.Teacher || u.Role == UserRole.BranchHead) &&
                                !assignedUserIds.Contains(u.Id))
                    .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                    .Select(u => new UnassignedTeacherItemDto
                    {
                        Id = u.Id,
                        Username = u.Username,
                        ChristianName = u.ChristianName ?? "",
                        FullName = $"{u.ChristianName} {u.FirstName} {u.LastName}".Trim(),
                        PhoneNumber = u.PhoneNumber ?? "",
                        Role = u.Role,
                        ManagedGradeLevel = u.ManagedGradeLevel,
                        LastLoginAt = u.LastLoginAt
                    })
                    .AsNoTracking()
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    academicYearName = currentYear?.Name ?? "Chưa kích hoạt",
                    data = unassignedTeachers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi truy vấn danh sách GLV chưa phân công lớp.");
                return Json(new
                {
                    success = false,
                    message = "Không thể tải danh sách Giáo lý viên lúc này. Vui lòng thử lại sau."
                });
            }
        }

        /// <summary>
        /// 2. API: VÔ HIỆU HÓA (DEACTIVATE) HÀNG LOẠT VÀ REVOKE TOKEN (TASK-815)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken] // SEC-02 FIX: Thay thế [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> BatchDeactivateTeachers([FromBody] BatchDeactivateTeachersRequestDto request)
        {
            if (request == null || !request.UserIds.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một Giáo lý viên để vô hiệu hóa." });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                int deactivatedCount = 0;

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    var usersToDeactivate = await _context.Users
                        .Include(u => u.RefreshTokens)
                        .Where(u => request.UserIds.Contains(u.Id) && u.IsActive)
                        .ToListAsync();

                    // Chặn vô hiệu hóa nhầm tài khoản Admin hoặc Cha Tuyên Úy
                    usersToDeactivate = usersToDeactivate
                        .Where(u => u.Role != UserRole.Admin && u.Role != UserRole.SpiritualDirector)
                        .ToList();

                    foreach (var user in usersToDeactivate)
                    {
                        user.IsActive = false;

                        // Thu hồi toàn bộ RefreshToken còn hiệu lực để ngắt phiên đăng nhập
                        foreach (var token in user.RefreshTokens.Where(rt => rt.IsActive))
                        {
                            token.RevokedAt = DateTime.UtcNow;
                            token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                            token.ReplacedByToken = "DEACTIVATED_BY_ADMIN";
                        }

                        deactivatedCount++;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                _logger.LogInformation("Đã vô hiệu hóa hàng loạt {Count} GLV nghỉ niên khóa mới bởi {Admin}. Lý do: {Reason}",
                    deactivatedCount, User.Identity?.Name, request.Reason);

                return Json(new
                {
                    success = true,
                    message = $"Đã vô hiệu hóa thành công {deactivatedCount} tài khoản Giáo lý viên nghỉ niên khóa mới!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình BatchDeactivateTeachers");
                return Json(new
                {
                    success = false,
                    message = "Đã xảy ra lỗi trong quá trình vô hiệu hóa tài khoản. Vui lòng thử lại sau."
                });
            }
        }
        #endregion

        // Kiểm tra xem người thao tác hiện tại có quyền tác động (Tạo / Đổi MK / Khóa) lên vai trò đích không
        private bool CanManageTargetRole(string targetRole)
        {
            // Admin có toàn quyền
            if (User.IsInRole(UserRole.Admin))
                return true;

            // Ban Điều Hành (ExecutiveBoard) chỉ được phép quản lý Trưởng khối (BranchHead), Giáo lý viên (Teacher) và Phụ huynh (Parent)
            // Không được phép tạo/sửa/khóa Admin, Cha Tuyên Úy (SpiritualDirector) hoặc thành viên Ban Điều Hành khác
            return targetRole == UserRole.BranchHead ||
                   targetRole == UserRole.Teacher ||
                   targetRole == UserRole.Parent;
        }

        /// <summary>
        /// SEC-09 FIX: Sinh mật khẩu khởi tạo ngẫu nhiên an toàn (10 ký tự, đủ chữ hoa, chữ thường, số và ký tự đặc biệt)
        /// </summary>
        private static string GenerateSecureInitialPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string special = "@#$!%*?&";
            const string allChars = upper + lower + digits + special;

            Span<char> password = stackalloc char[10];
            password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
            password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
            password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
            password[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
            }

            // Trộn ngẫu nhiên vị trí các ký tự (Fisher-Yates shuffle)
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }
    }
}
