using ClosedXML.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Enum;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

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
            UserRole.ProbationaryTeacher,
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
            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                // Thu thập toàn bộ thông báo lỗi validation nếu có
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();
                var str = errors.Any()
                    ? "Dữ liệu nhập không hợp lệ: " + string.Join("; ", errors)
                    : "Dữ liệu nhập không hợp lệ.";

                return Json(new { success = false, message = str });
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

            if (string.IsNullOrWhiteSpace(model.ChristianName) ||
                string.IsNullOrWhiteSpace(model.FirstName) ||
                string.IsNullOrWhiteSpace(model.LastName))
            {
                return Json(new { success = false, message = "Tên Thánh, Họ và Tên là bắt buộc." });
            }

            var normalizedFirstName = model.FirstName.Trim();
            var normalizedLastName = model.LastName.Trim();
            var lastNameParts = normalizedLastName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (lastNameParts.Length > 1)
            {
                normalizedFirstName = $"{normalizedFirstName} {string.Join(" ", lastNameParts[..^1])}".Trim();
                normalizedLastName = lastNameParts[^1];
            }

            if (normalizedFirstName.Length > 100)
            {
                return Json(new { success = false, message = "Họ và tên đệm không được vượt quá 100 ký tự." });
            }

            if (normalizedLastName.Length > 10)
            {
                return Json(new { success = false, message = "Tên chính không được vượt quá 10 ký tự." });
            }

            if (model.Role == UserRole.BranchHead && string.IsNullOrWhiteSpace(model.ManagedGradeLevel))
            {
                return Json(new { success = false, message = "Vui lòng chọn Khối phụ trách cho Trưởng khối." });
            }

            var managedGradeLevel = (model.Role == UserRole.BranchHead || model.Role == UserRole.Teacher) &&
                                    !string.IsNullOrWhiteSpace(model.ManagedGradeLevel)
                ? model.ManagedGradeLevel.Trim()
                : null;

            var newUser = new User
            {
                Username = usernameClean,
                PasswordHash = _passwordHasher.HashPassword(model.Password),
                FirstName = normalizedFirstName,
                LastName = normalizedLastName,
                ChristianName = model.ChristianName.Trim(),
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Role = model.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ManagedGradeLevel = managedGradeLevel
            };

            bool hasOldCode = !string.IsNullOrWhiteSpace(model.OldStudentCode);
            if (newUser.Role == UserRole.ProbationaryTeacher || hasOldCode)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                string? conflictMessage = null;
                long? operatorUserId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var opId) ? opId : null;
                var operatorName = User.Identity?.Name ?? "System";

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    Student? linkedStudent = null;
                    if (hasOldCode)
                    {
                        linkedStudent = await FindStudentByIdOrOldCodeAsync(null, model.OldStudentCode);
                        if (linkedStudent == null)
                        {
                            conflictMessage = $"Không tìm thấy hồ sơ thiếu nhi với mã học sinh '{model.OldStudentCode!.Trim()}'.";
                            return;
                        }
                    }
                    else
                    {
                        linkedStudent = await _context.Students
                            .FirstOrDefaultAsync(s => s.StudentCode.ToLower() == usernameClean);
                    }

                    if (linkedStudent != null)
                    {
                        bool alreadyLinked = await _context.Users
                            .AnyAsync(u => u.LinkedStudentId == linkedStudent.Id || u.StudentId == linkedStudent.Id);
                        if (alreadyLinked)
                        {
                            conflictMessage = $"Hồ sơ thiếu nhi '{linkedStudent.StudentCode}' đã được liên kết với một tài khoản khác.";
                            return;
                        }

                        var oldCode = linkedStudent.StudentCode;
                        SyncStudentFieldsToUser(linkedStudent, newUser);

                        if (newUser.Role == UserRole.Teacher)
                        {
                            if (!AgeValidationHelper.IsAtLeastAge(linkedStudent.DateOfBirth, VietnamTimeHelper.Today, 18))
                            {
                                int currentAge = AgeValidationHelper.CalculateAgeAtDate(linkedStudent.DateOfBirth, VietnamTimeHelper.Today);
                                conflictMessage = $"Học viên '{linkedStudent.StudentCode}' mới {currentAge} tuổi, chưa đủ 18 tuổi để tạo tài khoản Giáo lý viên chính thức.";
                                return;
                            }

                            if (linkedStudent.CatechistPromiseDate.HasValue &&
                                !AgeValidationHelper.IsAtLeastAge(linkedStudent.DateOfBirth, linkedStudent.CatechistPromiseDate.Value, 18))
                            {
                                int ageAtPromise = AgeValidationHelper.CalculateAgeAtDate(linkedStudent.DateOfBirth, linkedStudent.CatechistPromiseDate.Value);
                                conflictMessage = $"Học viên '{linkedStudent.StudentCode}' mới {ageAtPromise} tuổi tại ngày Tuyên hứa GLV ({linkedStudent.CatechistPromiseDate.Value:dd/MM/yyyy}), chưa đủ 18 tuổi theo quy định.";
                                return;
                            }
                        }

                        if (newUser.Role == UserRole.ProbationaryTeacher)
                        {
                            linkedStudent.StudentCode = newUser.Username;
                        }

                        _context.Users.Add(newUser);
                        await _context.SaveChangesAsync();

                        if (newUser.Role == UserRole.ProbationaryTeacher &&
                            !string.IsNullOrWhiteSpace(oldCode) &&
                            !string.Equals(oldCode, newUser.Username, StringComparison.OrdinalIgnoreCase))
                        {
                            var fallbackYearId = await GetFallbackAcademicYearIdAsync();
                            if (fallbackYearId.HasValue)
                            {
                                _context.StudentCodeHistories.Add(new StudentCodeHistory
                                {
                                    StudentId = linkedStudent.Id,
                                    SourceAcademicYearId = fallbackYearId.Value,
                                    TargetAcademicYearId = fallbackYearId.Value,
                                    OriginalCode = oldCode.Length > 50 ? oldCode[..50] : oldCode,
                                    ArchivedYearCode = oldCode.Length > 50 ? oldCode[..50] : oldCode,
                                    NewCode = newUser.Username,
                                    RolloverOutcome = "PROBATIONARY_ACCOUNT",
                                    TargetClassName = "Khối Dự Bị",
                                    ChangedBy = operatorName,
                                    ChangedAt = DateTime.UtcNow
                                });
                            }
                        }

                        _context.UserRoleHistories.Add(new UserRoleHistory
                        {
                            UserId = newUser.Id,
                            OldRole = "Student",
                            NewRole = newUser.Role,
                            AcademicYearId = await GetFallbackAcademicYearIdAsync(),
                            ChangedByUserId = operatorUserId,
                            ChangedAt = DateTime.UtcNow,
                            Reason = $"Tạo tài khoản và gắn hồ sơ thiếu nhi mã {oldCode}"
                        });

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    else
                    {
                        var probationaryStudent = new Student
                        {
                            StudentCode = newUser.Username,
                            ChristianName = newUser.ChristianName ?? string.Empty,
                            FirstName = normalizedFirstName.Length > 90 ? normalizedFirstName[..90] : normalizedFirstName,
                            LastName = normalizedLastName,
                            Gender = InferGenderFromName(newUser.ChristianName, normalizedFirstName),
                            DateOfBirth = newUser.DateOfBirth.HasValue
                                ? DateTime.SpecifyKind(newUser.DateOfBirth.Value.Date, DateTimeKind.Utc)
                                : new DateTime(VietnamTimeHelper.Today.Year - 16, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                            ParentPhone = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                            IsActive = true
                        };

                        _context.Students.Add(probationaryStudent);
                        await _context.SaveChangesAsync();

                        newUser.LinkedStudentId = probationaryStudent.Id;
                        newUser.StudentId = probationaryStudent.Id;

                        _context.Users.Add(newUser);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                });

                if (!string.IsNullOrEmpty(conflictMessage))
                {
                    return Json(new { success = false, message = conflictMessage });
                }

                _logger.LogInformation("Người dùng {CurrentUser} đã tạo tài khoản mới: {NewUsername} ({Role}, LinkedStudentId={StudentId})",
                    User.Identity?.Name, newUser.Username, newUser.Role, newUser.LinkedStudentId);

                return Json(new
                {
                    success = true,
                    message = newUser.LinkedStudentId.HasValue
                        ? $"Đã tạo tài khoản '{newUser.Username}' và liên kết lịch sử thiếu nhi thành công!"
                        : $"Đã tạo tài khoản '{newUser.Username}' thành công!"
                });
            }

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
            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();
                var message = errors.Any()
                    ? "Dữ liệu mật khẩu mới không hợp lệ: " + string.Join("; ", errors)
                    : "Dữ liệu mật khẩu mới không hợp lệ.";
                return Json(new { success = false, message });
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
        /// <summary>
        /// GET: /Users/GetBaoDongGraduatesForBatchAccount?scope=BAO_DONG&amp;search=...
        /// Lấy danh sách học viên đã hoàn thành Bao Đồng / Tuyên hứa chưa có tài khoản để BĐH tạo tài khoản hàng loạt
        /// </summary>
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> GetBaoDongGraduatesForBatchAccount(string? scope = "BAO_DONG", string? search = null, string? targetRole = UserRole.ProbationaryTeacher)
        {
            try
            {
                var linkedStudentIds = (await _context.Users
                    .AsNoTracking()
                    .Where(u => u.LinkedStudentId.HasValue || u.StudentId.HasValue)
                    .Select(u => u.LinkedStudentId ?? u.StudentId!.Value)
                    .ToListAsync())
                    .ToHashSet();

                var sacBaoDongIds = (await _context.StudentSacramentRecords
                    .AsNoTracking()
                    .Where(r => r.SacramentType == (int)SacramentTypeEnum.ProfessionOfFaith)
                    .Select(r => r.StudentId)
                    .ToListAsync())
                    .ToHashSet();

                var enrolledBaoDongMap = await _context.Enrollments
                    .AsNoTracking()
                    .Include(e => e.ClassRoom)
                        .ThenInclude(c => c.AcademicYear)
                    .Where(e => e.ClassRoom != null)
                    .OrderByDescending(e => e.ClassRoom.AcademicYear.StartDate)
                    .ToListAsync();

                var latestClassByStudent = enrolledBaoDongMap
                    .GroupBy(e => e.StudentId)
                    .ToDictionary(g => g.Key, g => g.First().ClassRoom);

                var baoDongStudentIds = enrolledBaoDongMap
                    .Where(e => e.ClassRoom != null && e.ClassRoom.GradeLevel == GradeLevel.BaoDong)
                    .Select(e => e.StudentId)
                    .ToHashSet();

                var allUnlinkedActiveStudents = await _context.Students
                    .AsNoTracking()
                    .Where(s => s.IsActive && !linkedStudentIds.Contains(s.Id))
                    .OrderByDescending(s => s.BaoDongPromiseDate.HasValue)
                    .ThenByDescending(s => s.BaoDongPromiseDate)
                    .ThenBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .ToListAsync();

                bool onlyBaoDong = !string.Equals(scope, "ALL", StringComparison.OrdinalIgnoreCase);

                var existingUsernames = await _context.Users
                    .AsNoTracking()
                    .Select(u => u.Username.ToLower())
                    .ToListAsync();
                var usedSet = new HashSet<string>(existingUsernames);

                string resolvedRole = targetRole == UserRole.Teacher ? UserRole.Teacher : UserRole.ProbationaryTeacher;
                string? term = string.IsNullOrWhiteSpace(search) ? null : VietnameseStringHelper.RemoveDiacritics(search.Trim()).ToLowerInvariant();

                var results = new List<BaoDongGraduateCandidateDto>();

                foreach (var s in allUnlinkedActiveStudents)
                {
                    bool hasBaoDongRecord = s.BaoDongPromiseDate.HasValue ||
                                            sacBaoDongIds.Contains(s.Id) ||
                                            baoDongStudentIds.Contains(s.Id);

                    if (onlyBaoDong && !hasBaoDongRecord) continue;

                    latestClassByStudent.TryGetValue(s.Id, out var latestCls);

                    if (!string.IsNullOrEmpty(term))
                    {
                        var combined = VietnameseStringHelper.RemoveDiacritics(
                            $"{s.StudentCode} {s.ChristianName} {s.FirstName} {s.LastName} {s.ParentPhone ?? ""} {latestCls?.Name ?? ""}")
                            .ToLowerInvariant();
                        if (!combined.Contains(term)) continue;
                    }

                    string suggested = GenerateSuggestedUsername(s.FirstName, s.LastName, resolvedRole, usedSet, null);

                    results.Add(new BaoDongGraduateCandidateDto
                    {
                        StudentId = s.Id,
                        OldStudentCode = s.StudentCode,
                        ChristianName = s.ChristianName,
                        FirstName = s.FirstName,
                        LastName = s.LastName,
                        FullName = $"{s.ChristianName} {s.FirstName} {s.LastName}".Trim(),
                        DateOfBirth = s.DateOfBirth.ToString("dd/MM/yyyy"),
                        Gender = s.Gender,
                        ParentPhone = s.ParentPhone,
                        ClassName = latestCls?.Name ?? "Chưa xếp lớp",
                        GradeLevel = latestCls?.GradeLevel ?? string.Empty,
                        BaoDongPromiseDate = s.BaoDongPromiseDate?.ToString("dd/MM/yyyy"),
                        IsBaoDongCompleted = hasBaoDongRecord,
                        SuggestedUsername = suggested
                    });

                    if (results.Count >= 200) break;
                }

                return Json(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải danh sách học viên hoàn thành Bao Đồng để cấp tài khoản hàng loạt.");
                return Json(new { success = false, message = "Không thể tải danh sách học viên Bao Đồng lúc này." });
            }
        }

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
        "Số Điện Thoại",
        "Email",
        "Khối Dự Kiến (Dự Bị / Khai Tâm / Rước Lễ...)",
        "Mã Học Sinh Cũ (Tùy chọn - gắn lịch sử)"
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
            worksheet.Cell(2, 6).Value = "Dự Bị";
            worksheet.Cell(2, 7).Value = "BD3A001";

            worksheet.Cell(3, 1).Value = "Maria";
            worksheet.Cell(3, 2).Value = "Trần Thị";
            worksheet.Cell(3, 3).Value = "Bình";
            worksheet.Cell(3, 4).Value = "0987654321";
            worksheet.Cell(3, 5).Value = "";
            worksheet.Cell(3, 6).Value = "Khai Tâm";
            worksheet.Cell(3, 7).Value = "";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_DanhSach_TaiKhoan_TuyenHua.xlsx");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="defaultPassword"></param>
        /// <param name="targetRole"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> ImportTeachersFromExcel(IFormFile file, [FromForm] string? defaultPassword, [FromForm] string? targetRole)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file Excel danh sách tuyên hứa." });
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
                await using var stream = file.OpenReadStream();
                XLWorkbook workbook;
                if (stream.CanSeek)
                {
                    if (!await FileUploadSecurityHelper.ValidateStreamMagicBytesAsync(stream, ".xlsx", HttpContext.RequestAborted))
                    {
                        return Json(new { success = false, message = "Nội dung file Excel (.xlsx) không hợp lệ hoặc sai chữ ký định dạng." });
                    }
                    workbook = new XLWorkbook(stream);
                }
                else
                {
                    if (!await FileUploadSecurityHelper.ValidateFormFileMagicBytesAsync(file, ".xlsx", HttpContext.RequestAborted))
                    {
                        return Json(new { success = false, message = "Nội dung file Excel (.xlsx) không hợp lệ hoặc sai chữ ký định dạng." });
                    }
                    await using var readStream = file.OpenReadStream();
                    workbook = new XLWorkbook(readStream);
                }

                using (workbook)
                {
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
                        var oldStudentCode = row.Cell(7).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(christian) &&
                            string.IsNullOrWhiteSpace(first) &&
                            string.IsNullOrWhiteSpace(last) &&
                            string.IsNullOrWhiteSpace(oldStudentCode))
                            continue;

                        teachers.Add(new BatchTeacherItemDto
                        {
                            ChristianName = christian,
                            FirstName = first,
                            LastName = last,
                            PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
                            Email = string.IsNullOrWhiteSpace(email) ? null : email,
                            ManagedGradeLevel = string.IsNullOrWhiteSpace(grade) ? null : grade,
                            OldStudentCode = string.IsNullOrWhiteSpace(oldStudentCode) ? null : oldStudentCode
                        });
                    }
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
                TargetRole = targetRole == UserRole.Teacher ? UserRole.Teacher : UserRole.ProbationaryTeacher,
                Teachers = teachers
            };

            return await ExecuteBatchCreate(request);
        }

        /// <summary>
        /// 3. API TẠO HÀNG LOẠT TÀI KHOẢN DỰ BỊ / GLV (BATCH JSON) (TASK-814 + PHASE 6)
        /// </summary>
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
                return Json(new BatchCreateTeachersResultDto { Success = false, Message = "Danh sách để tạo tài khoản rỗng." });
            }

            var result = new BatchCreateTeachersResultDto();
            var customDefaultPass = string.IsNullOrWhiteSpace(request.DefaultPassword) ? null : request.DefaultPassword.Trim();

            if (customDefaultPass != null &&
                !Regex.IsMatch(customDefaultPass, PasswordPolicy.ComplexityPattern))
            {
                return Json(new BatchCreateTeachersResultDto
                {
                    Success = false,
                    Message = PasswordPolicy.ComplexityErrorMessage
                });
            }

            string defaultTargetRole = request.TargetRole == UserRole.Teacher
                ? UserRole.Teacher
                : UserRole.ProbationaryTeacher;

            var existingUsernames = await _context.Users.Select(u => u.Username.ToLower()).ToListAsync();
            var usernameSet = new HashSet<string>(existingUsernames);

            var linkedStudentIdSet = (await _context.Users
                .AsNoTracking()
                .Where(u => u.LinkedStudentId.HasValue || u.StudentId.HasValue)
                .Select(u => u.LinkedStudentId ?? u.StudentId!.Value)
                .ToListAsync())
                .ToHashSet();

            var fallbackYearId = await GetFallbackAcademicYearIdAsync();
            long? operatorUserId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var opId) ? opId : null;
            string operatorName = User.Identity?.Name ?? "System";

            var pendingCreates = new List<(User User, Student? LinkedStudent, string? OldStudentCode)>();

            foreach (var item in request.Teachers)
            {
                string itemRole = item.Role == UserRole.Teacher || item.Role == UserRole.ProbationaryTeacher
                    ? item.Role
                    : defaultTargetRole;

                Student? linkedStudent = null;
                if (item.StudentId.HasValue || !string.IsNullOrWhiteSpace(item.OldStudentCode))
                {
                    linkedStudent = await FindStudentByIdOrOldCodeAsync(item.StudentId, item.OldStudentCode);
                    if (linkedStudent == null)
                    {
                        result.Errors.Add($"Bỏ qua: Không tìm thấy hồ sơ học sinh mã '{item.OldStudentCode ?? item.StudentId?.ToString()}'");
                        continue;
                    }

                    if (!linkedStudentIdSet.Add(linkedStudent.Id))
                    {
                        result.Errors.Add($"Bỏ qua: Học sinh '{linkedStudent.ChristianName} {linkedStudent.FirstName} {linkedStudent.LastName}' ({linkedStudent.StudentCode}) đã có tài khoản liên kết hoặc bị trùng trong danh sách.");
                        continue;
                    }

                    if (itemRole == UserRole.Teacher)
                    {
                        bool isUnder18Today = !AgeValidationHelper.IsAtLeastAge(linkedStudent.DateOfBirth, VietnamTimeHelper.Today, 18);
                        bool isUnder18AtPromise = linkedStudent.CatechistPromiseDate.HasValue &&
                            !AgeValidationHelper.IsAtLeastAge(linkedStudent.DateOfBirth, linkedStudent.CatechistPromiseDate.Value, 18);

                        if (isUnder18Today || isUnder18AtPromise)
                        {
                            result.Errors.Add($"Bỏ qua: Học viên '{linkedStudent.ChristianName} {linkedStudent.FirstName} {linkedStudent.LastName}' ({linkedStudent.StudentCode}) chưa đủ 18 tuổi để tạo tài khoản Giáo lý viên chính thức.");
                            linkedStudentIdSet.Remove(linkedStudent.Id);
                            continue;
                        }
                    }
                }

                string christian = !string.IsNullOrWhiteSpace(item.ChristianName)
                    ? item.ChristianName.Trim()
                    : (linkedStudent?.ChristianName ?? string.Empty);
                string firstName = !string.IsNullOrWhiteSpace(item.FirstName)
                    ? item.FirstName.Trim()
                    : (linkedStudent?.FirstName ?? string.Empty);
                string lastName = !string.IsNullOrWhiteSpace(item.LastName)
                    ? item.LastName.Trim()
                    : (linkedStudent?.LastName ?? string.Empty);

                var lastParts = lastName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (lastParts.Length > 1)
                {
                    firstName = $"{firstName} {string.Join(" ", lastParts[..^1])}".Trim();
                    lastName = lastParts[^1];
                }

                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                {
                    result.Errors.Add($"Bỏ qua dòng: Thiếu Họ hoặc Tên ({christian} {firstName} {lastName})");
                    continue;
                }

                if (firstName.Length > 100) firstName = firstName[..100];
                if (lastName.Length > 10) lastName = lastName[..10];
                if (christian.Length > 50) christian = christian[..50];

                var initialPass = customDefaultPass ?? GenerateSecureInitialPassword();
                var passwordHash = _passwordHasher.HashPassword(initialPass);
                var finalUsername = GenerateSuggestedUsername(firstName, lastName, itemRole, usernameSet, item.CustomUsername);

                string? oldCode = linkedStudent?.StudentCode;
                string? managedGrade = itemRole == UserRole.Teacher && !string.IsNullOrWhiteSpace(item.ManagedGradeLevel)
                    ? item.ManagedGradeLevel.Trim()
                    : null;

                var newUser = new User
                {
                    Username = finalUsername,
                    PasswordHash = passwordHash,
                    ChristianName = christian,
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = !string.IsNullOrWhiteSpace(item.PhoneNumber) ? item.PhoneNumber.Trim() : linkedStudent?.ParentPhone,
                    Email = string.IsNullOrWhiteSpace(item.Email) ? null : item.Email.Trim(),
                    Role = itemRole,
                    ManagedGradeLevel = managedGrade,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                if (linkedStudent != null)
                {
                    SyncStudentFieldsToUser(linkedStudent, newUser);
                }

                pendingCreates.Add((newUser, linkedStudent, oldCode));

                result.CreatedTeachers.Add(new CreatedTeacherResultItem
                {
                    Username = finalUsername,
                    FullName = $"{firstName} {lastName}".Trim(),
                    ChristianName = christian,
                    PhoneNumber = newUser.PhoneNumber ?? "",
                    InitialPassword = initialPass,
                    ManagedGradeLevel = managedGrade,
                    OldStudentCode = oldCode,
                    Role = itemRole == UserRole.ProbationaryTeacher ? "Dự Bị" : "Giáo Lý Viên"
                });
            }

            if (!pendingCreates.Any())
            {
                result.Success = false;
                result.Message = result.Errors.Any()
                    ? string.Join("; ", result.Errors.Take(3))
                    : "Không có tài khoản nào hợp lệ để tạo mới.";
                return Json(result);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    await _context.Users.AddRangeAsync(pendingCreates.Select(p => p.User));
                    await _context.SaveChangesAsync();

                    foreach (var entry in pendingCreates)
                    {
                        if (entry.LinkedStudent != null)
                        {
                            if (entry.User.Role == UserRole.ProbationaryTeacher &&
                                !string.Equals(entry.LinkedStudent.StudentCode, entry.User.Username, StringComparison.Ordinal))
                            {
                                entry.LinkedStudent.StudentCode = entry.User.Username;
                            }

                            if (!string.IsNullOrWhiteSpace(entry.OldStudentCode) &&
                                !string.Equals(entry.OldStudentCode, entry.User.Username, StringComparison.OrdinalIgnoreCase) &&
                                fallbackYearId.HasValue)
                            {
                                _context.StudentCodeHistories.Add(new StudentCodeHistory
                                {
                                    StudentId = entry.LinkedStudent.Id,
                                    SourceAcademicYearId = fallbackYearId.Value,
                                    TargetAcademicYearId = fallbackYearId.Value,
                                    OriginalCode = entry.OldStudentCode.Length > 50 ? entry.OldStudentCode[..50] : entry.OldStudentCode,
                                    ArchivedYearCode = entry.OldStudentCode.Length > 50 ? entry.OldStudentCode[..50] : entry.OldStudentCode,
                                    NewCode = entry.User.Username,
                                    RolloverOutcome = "PROBATIONARY_ACCOUNT",
                                    TargetClassName = entry.User.Role == UserRole.ProbationaryTeacher ? "Khối Dự Bị" : "Giáo Lý Viên",
                                    ChangedBy = operatorName,
                                    ChangedAt = DateTime.UtcNow
                                });
                            }
                        }

                        _context.UserRoleHistories.Add(new UserRoleHistory
                        {
                            UserId = entry.User.Id,
                            OldRole = entry.LinkedStudent != null ? "Student" : "New",
                            NewRole = entry.User.Role,
                            AcademicYearId = fallbackYearId,
                            ChangedByUserId = operatorUserId,
                            ChangedAt = DateTime.UtcNow,
                            Reason = entry.LinkedStudent != null
                                ? $"Tạo tài khoản hàng loạt sau tuyên hứa (gắn mã HS cũ: {entry.OldStudentCode})"
                                : "Tạo tài khoản hàng loạt"
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                result.Success = true;
                result.TotalCreated = pendingCreates.Count;
                string roleLabel = defaultTargetRole == UserRole.ProbationaryTeacher ? "Dự Bị" : "Giáo lý viên";
                result.Message = $"Đã tạo thành công {pendingCreates.Count} tài khoản {roleLabel} mới!" +
                                 (result.Errors.Any() ? $" (Bỏ qua {result.Errors.Count} dòng lỗi/trùng)." : "");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu danh sách tài khoản hàng loạt vào Database.");
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

        private async Task<Student?> FindStudentByIdOrOldCodeAsync(long? studentId, string? oldCode)
        {
            if (studentId.HasValue && studentId.Value > 0)
            {
                return await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId.Value);
            }

            if (string.IsNullOrWhiteSpace(oldCode)) return null;

            var cleanCode = oldCode.Trim().ToLowerInvariant();
            var direct = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentCode.ToLower() == cleanCode);
            if (direct != null) return direct;

            var history = await _context.StudentCodeHistories
                .AsNoTracking()
                .Include(h => h.Student)
                .Where(h => h.OriginalCode.ToLower() == cleanCode ||
                            h.ArchivedYearCode.ToLower() == cleanCode ||
                            h.NewCode.ToLower() == cleanCode)
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            return history?.Student;
        }

        private async Task<int?> GetFallbackAcademicYearIdAsync()
        {
            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            if (currentYear != null) return currentYear.Id;

            var latestYear = await _context.AcademicYears
                .AsNoTracking()
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefaultAsync();

            return latestYear?.Id;
        }

        private static void SyncStudentFieldsToUser(Student student, User user)
        {
            user.LinkedStudentId = student.Id;
            user.StudentId = student.Id;
            user.DateOfBirth ??= student.DateOfBirth;
            user.FeastDay ??= student.FeastDay;
            user.BaptismDate ??= student.BaptismDate;
            user.FirstCommunionDate ??= student.FirstCommunionDate;
            user.ConfirmationDate ??= student.ConfirmationDate;
            user.BaoDongPromiseDate ??= student.BaoDongPromiseDate;
            user.CatechistPromiseDate ??= student.CatechistPromiseDate;

            if (string.IsNullOrWhiteSpace(user.PhoneNumber) && !string.IsNullOrWhiteSpace(student.ParentPhone))
            {
                user.PhoneNumber = student.ParentPhone;
            }
        }

        private static string GenerateSuggestedUsername(
            string firstName,
            string lastName,
            string role,
            HashSet<string> usedSet,
            string? customUsername)
        {
            string baseUsername;
            if (!string.IsNullOrWhiteSpace(customUsername))
            {
                var cleanedCustom = Regex.Replace(customUsername.Trim().ToLowerInvariant(), @"[^a-z0-9_.]", "");
                if (cleanedCustom.Length >= 3)
                {
                    baseUsername = cleanedCustom.Length > 45 ? cleanedCustom[..45] : cleanedCustom;
                }
                else
                {
                    baseUsername = BuildPrefixUsername(firstName, lastName, role);
                }
            }
            else
            {
                baseUsername = BuildPrefixUsername(firstName, lastName, role);
            }

            string finalUsername = baseUsername;
            int counter = 1;
            while (usedSet.Contains(finalUsername))
            {
                finalUsername = $"{baseUsername}{counter++}";
            }
            usedSet.Add(finalUsername);
            return finalUsername;
        }

        private static string BuildPrefixUsername(string firstName, string lastName, string role)
        {
            string prefix = role == UserRole.ProbationaryTeacher ? "db_" : "glv_";
            var cleanFirst = VietnameseStringHelper.RemoveDiacritics(firstName ?? "");
            var cleanLast = VietnameseStringHelper.RemoveDiacritics(lastName ?? "");
            var firstLetters = string.Concat(cleanFirst
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w[0]));
            var raw = Regex.Replace($"{prefix}{firstLetters}{cleanLast}".ToLowerInvariant(), @"[^a-z0-9_]", "");
            if (raw.Length < 3) raw = $"{prefix}tv";
            return raw.Length > 45 ? raw[..45] : raw;
        }

        private static string InferGenderFromName(string? christianName, string? firstName)
        {
            string combined = $" {christianName ?? ""} {firstName ?? ""} ".ToLowerInvariant();
            string[] femaleMarkers =
            {
                " maria ", " têrêsa ", " teresa ", " anna ", " lucia ", " lusia ",
                " agatha ", " agnes ", " marta ", " catarina ", " rosa ", " mônica ",
                " monica ", " elisabeth ", " madalena ", " cêcilia ", " clara ", " thị "
            };

            return femaleMarkers.Any(m => combined.Contains(m)) ? "Nữ" : "Nam";
        }

        // Kiểm tra xem người thao tác hiện tại có quyền tác động (Tạo / Đổi MK / Khóa) lên vai trò đích không
        private bool CanManageTargetRole(string targetRole)
        {
            // Admin có toàn quyền
            if (User.IsInRole(UserRole.Admin))
                return true;

            // Ban Điều Hành (ExecutiveBoard) chỉ được phép quản lý Trưởng khối (BranchHead), Giáo lý viên (Teacher / ProbationaryTeacher) và Phụ huynh (Parent)
            // Không được phép tạo/sửa/khóa Admin, Cha Tuyên Úy (SpiritualDirector) hoặc thành viên Ban Điều Hành khác
            return targetRole == UserRole.BranchHead ||
                   targetRole == UserRole.Teacher ||
                   targetRole == UserRole.ProbationaryTeacher ||
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
