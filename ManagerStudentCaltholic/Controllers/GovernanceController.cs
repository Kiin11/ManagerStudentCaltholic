using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard}")]
    public class GovernanceController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<GovernanceController> _logger;

        public GovernanceController(ParishDbContext context, ILogger<GovernanceController> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        /// <summary>
        /// 1. GET: /Governance (Màn hình Điều hành Thâm niên 
        /// </summary>
        /// <param name="academicYearId"></param>
        /// <returns></returns>& Luân chuyển)
        [HttpGet]
        public async Task<IActionResult> Index(int? academicYearId)
        {
            var years = await _context.AcademicYears.OrderByDescending(y => y.StartDate).ToListAsync();
            var currentYear = years.FirstOrDefault(y => y.IsCurrent) ?? years.FirstOrDefault();
            var selectedYearId = academicYearId ?? currentYear?.Id ?? 0;

            ViewBag.AcademicYears = years;
            ViewBag.SelectedYearId = selectedYearId;

            // 1. Lấy danh sách 4 Trưởng khối của niên khóa được chọn (TASK-906)
            var branchHeadAssignments = await _context.BranchHeadAssignments
                .Include(b => b.User)
                .Include(b => b.AssignedByUser)
                .Where(b => b.AcademicYearId == selectedYearId)
                .ToListAsync();

            ViewBag.BranchHeadAssignments = branchHeadAssignments;

            // 2. Danh sách Ban Điều Hành hiện hành
            var executiveMembers = await _context.Users
                .Where(u => u.Role == UserRole.ExecutiveBoard && u.IsActive)
                .OrderBy(u => u.FirstName)
                .ToListAsync();

            ViewBag.ExecutiveMembers = executiveMembers;

            // 3. Lịch sử Audit bàn giao vai trò BĐH (TASK-907)
            var roleHistories = await _context.UserRoleHistories
                .Include(h => h.User)
                .Include(h => h.AcademicYear)
                .Include(h => h.ChangedByUser)
                .OrderByDescending(h => h.ChangedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.RoleHistories = roleHistories;

            // 4. Danh sách tài khoản GLV hợp lệ để chỉ định bổ nhiệm
            var candidateTeachers = await _context.Users
                .Where(u => u.IsActive && u.Role != UserRole.Admin && u.Role != UserRole.SpiritualDirector)
                .OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, u.Username, FullName = (u.ChristianName + " " + u.FirstName + " " + u.LastName).Trim(), u.Role, u.ManagedGradeLevel })
                .ToListAsync();

            ViewBag.CandidateTeachers = candidateTeachers;

            return View();
        }

        /// <summary>
        /// 2. POST: /Governance/AssignBranchHead (Luân chuyển Trưởng khối - TASK-906)
        /// </summary>
        /// <param name="academicYearId"></param>
        /// <param name="gradeLevel"></param>
        /// <param name="newUserId"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignBranchHead([FromForm] int academicYearId, [FromForm] string gradeLevel, [FromForm] long newUserId, [FromForm] string? notes)
        {
            try
            {
                if (academicYearId <= 0 || newUserId <= 0 || string.IsNullOrWhiteSpace(gradeLevel))
                {
                    return Json(new { success = false, message = "Dữ liệu bổ nhiệm không hợp lệ." });
                }

                var operatorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                long.TryParse(operatorIdStr, out var operatorUserId);

                // 1. Trong AssignBranchHead: Chặn gán nhầm Admin, Cha Tuyên Úy hoặc BĐH làm Trưởng khối
                var targetUser = await _context.Users.FindAsync(newUserId);
                if (targetUser == null || !targetUser.IsActive)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng hợp lệ được chỉ định." });
                }

                // SEC-03 FIX: Chỉ cho phép bổ nhiệm Trưởng khối từ tài khoản Teacher hoặc BranchHead
                if (targetUser.Role != UserRole.Teacher && targetUser.Role != UserRole.BranchHead)
                {
                    return Json(new { success = false, message = "Chỉ có thể bổ nhiệm Trưởng khối đối với các tài khoản đang có vai trò Giáo lý viên hoặc Trưởng khối." });
                }

                var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);

                // Kiểm tra phân công hiện có của khối trong niên khóa
                var assignment = await _context.BranchHeadAssignments
                    .FirstOrDefaultAsync(b => b.AcademicYearId == academicYearId && b.ManagedGradeLevel == gradeLevel);

                if (assignment != null)
                {
                    // Nếu đã có phân công, kiểm tra xem có cần thay đổi người được chỉ định không
                    if (assignment.UserId == newUserId)
                    {
                        return Json(new { success = false, message = $"Người dùng đã là Trưởng khối {gradeLevel} trong niên khóa này." });
                    }
                    else
                    {
                        // Ghi nhận Audit Log cho việc thay đổi Trưởng khối
                        _context.UserRoleHistories.Add(new UserRoleHistory
                        {
                            UserId = assignment.UserId,
                            OldRole = UserRole.BranchHead,
                            NewRole = UserRole.Teacher, // người cũ trở về Teacher
                            AcademicYearId = academicYearId,
                            ChangedByUserId = operatorUserId,
                            ChangedAt = DateTime.UtcNow,
                            Reason = $"Thay đổi Trưởng khối {gradeLevel} sang người dùng khác."
                        });
                        // Nếu gán vào Niên khóa đang kích hoạt: Đồng bộ cột Role và ManagedGradeLevel trên Users
                        if (currentYear != null && currentYear.Id == academicYearId)
                        {
                            var oldUser = await _context.Users.FindAsync(assignment.UserId);
                            if (oldUser != null)
                            {
                                oldUser.Role = UserRole.Teacher; // người cũ trở về Teacher
                                oldUser.ManagedGradeLevel = null;
                            }
                        }
                    }


                    // Cập nhật phân công mới
                    assignment.UserId = newUserId;
                    assignment.AssignedByUserId = operatorUserId;
                    assignment.AssignedAt = DateTime.UtcNow;
                    assignment.Notes = notes;
                }
                else
                {
                    // Ghi nhận Audit Log cho việc thay đổi Trưởng khối
                    _context.UserRoleHistories.Add(new UserRoleHistory
                    {
                        UserId = newUserId,
                        OldRole = targetUser.Role,
                        NewRole = UserRole.BranchHead,
                        AcademicYearId = academicYearId,
                        ChangedByUserId = operatorUserId,
                        ChangedAt = DateTime.UtcNow,
                        Reason = $"Bổ nhiệm Trưởng khối {gradeLevel} cho niên khóa."
                    });

                    // Nếu chưa có phân công, tạo mới
                    _context.BranchHeadAssignments.Add(new BranchHeadAssignment
                    {
                        AcademicYearId = academicYearId,
                        ManagedGradeLevel = gradeLevel,
                        UserId = newUserId,
                        AssignedByUserId = operatorUserId,
                        AssignedAt = DateTime.UtcNow,
                        Notes = notes
                    });
                }

                // Nếu gán vào Niên khóa đang kích hoạt: Đồng bộ cột Role và ManagedGradeLevel trên Users
                if (currentYear != null && currentYear.Id == academicYearId)
                {
                    targetUser.Role = UserRole.BranchHead;
                    targetUser.ManagedGradeLevel = gradeLevel;
                }

                await _context.SaveChangesAsync();

                if (currentYear != null && currentYear.Id == academicYearId)
                {
                    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var affectedUserIds = new List<long> { newUserId };
                    if (assignment != null && assignment.UserId != newUserId)
                    {
                        affectedUserIds.Add(assignment.UserId);
                    }

                    var activeTokens = await _context.RefreshTokens
                        .Where(r => affectedUserIds.Contains(r.UserId) && r.RevokedAt == null)
                        .ToListAsync();

                    foreach (var t in activeTokens)
                    {
                        t.RevokedAt = DateTime.UtcNow;
                        t.RevokedByIp = clientIp;
                        t.ReplacedByToken = "REVOKED_ON_BRANCH_HEAD_CHANGE";
                    }
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = $"Bổ nhiệm thành công Trưởng khối {gradeLevel} cho niên khóa!" });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Lỗi Database: " + inner });
            }
        }

        /// <summary>
        /// 3. POST: /Governance/ChangeExecutiveRole (Bàn giao/Mãn nhiệm BĐH - TASK-907)
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="targetRole"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeExecutiveRole(long userId, string targetRole, string? reason)
        {
            var operatorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            long.TryParse(operatorIdStr, out var operatorUserId);

            // SEC-03 FIX 1: Chỉ cho phép chuyển đổi giữa đúng 2 vai trò: ExecutiveBoard <-> Teacher
            // Tuyệt đối chặn truyền targetRole = "Admin" hoặc "SpiritualDirector"
            if (targetRole != UserRole.ExecutiveBoard && targetRole != UserRole.Teacher)
            {
                _logger.LogWarning("Cảnh báo bảo mật: User ID {OperatorId} truyền targetRole không hợp lệ ({TargetRole}).",
                    operatorUserId, targetRole);
                return Json(new { success = false, message = "Vai trò bàn giao không hợp lệ. Chỉ cho phép bổ nhiệm Ban Điều Hành hoặc mãn nhiệm về Giáo Lý Viên." });
            }

            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản người dùng." });
            }

            // SEC-03 FIX 2: Không bao giờ cho phép tác động lên tài khoản Admin hoặc Cha Tuyên Úy
            if (targetUser.Role == UserRole.Admin || targetUser.Role == UserRole.SpiritualDirector)
            {
                _logger.LogWarning("Cảnh báo bảo mật: User ID {OperatorId} cố gắng thay đổi vai trò của {TargetUser} ({Role}).",
                    operatorUserId, targetUser.Username, targetUser.Role);
                return Json(new { success = false, message = "Không được phép thay đổi vai trò của Quản trị viên hệ thống hoặc Cha Tuyên Úy." });
            }

            // SEC-03 FIX 3: Không cho phép tự mãn nhiệm hoặc tự thay đổi vai trò của chính mình
            if (targetUser.Id == operatorUserId)
            {
                return Json(new { success = false, message = "Bạn không thể tự thay đổi vai trò của chính mình." });
            }

            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var oldRole = targetUser.Role;

            // Ghi nhận Audit Log
            _context.UserRoleHistories.Add(new UserRoleHistory
            {
                UserId = userId,
                OldRole = oldRole,
                NewRole = targetRole,
                AcademicYearId = currentYear?.Id,
                ChangedByUserId = operatorUserId,
                ChangedAt = DateTime.UtcNow,
                Reason = reason ?? "Bàn giao nhân sự đầu niên khóa"
            });

            targetUser.Role = targetRole;
            if (targetRole == UserRole.ExecutiveBoard || targetRole == UserRole.Teacher)
            {
                targetUser.ManagedGradeLevel = null;
            }

            // Khi thay đổi vai trò: Thu hồi toàn bộ Refresh Token cũ
            var tokens = await _context.RefreshTokens
                .Where(r => r.UserId == userId && r.RevokedAt == null)
                .ToListAsync();

            foreach (var t in tokens)
            {
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                t.ReplacedByToken = "REVOKED_ON_ROLE_CHANGE";
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Bàn giao vai trò User ID {UserId} từ {OldRole} -> {NewRole} bởi Operator ID {OperatorId}",
                userId, oldRole, targetRole, operatorUserId);

            return Json(new { success = true, message = $"Đã cập nhật vai trò người dùng sang {targetRole} thành công!" });
        }
    }
}
