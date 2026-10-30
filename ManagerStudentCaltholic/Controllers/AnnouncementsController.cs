using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class AnnouncementsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<AnnouncementsController> _logger;

        public AnnouncementsController(ParishDbContext context, ILogger<AnnouncementsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private long CurrentUserId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        /// <summary>
        /// Lấy danh sách các lớp mà người dùng hiện tại được phép đăng/quản lý bảng tin
        /// </summary>
        private async Task<List<ClassRoom>> GetAuthorizedClassesAsync(int currentYearId)
        {
            var query = _context.Classes.Where(c => currentYearId == 0 || c.AcademicYearId == currentYearId);

            if (IsLeadership)
            {
                return await query.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).AsNoTracking().ToListAsync();
            }

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    query = query.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
                }
                else
                {
                    query = query.Where(c => c.GradeLevel == managedGrade || c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
                }
            }
            else
            {
                query = query.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
            }

            return await query.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).AsNoTracking().ToListAsync();
        }

        /// <summary>
        /// Kiểm tra người dùng có quyền ghim hoặc xóa một bài thông báo cụ thể hay không
        /// </summary>
        private async Task<bool> CanManageAnnouncementAsync(Announcement item, int currentYearId)
        {
            if (IsLeadership) return true;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrWhiteSpace(managedGrade))
                {
                    if (item.Scope == "GRADE" && string.Equals(item.TargetGradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (item.Scope == "CLASS" && item.TargetClassRoomId.HasValue)
                    {
                        var cls = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == item.TargetClassRoomId.Value);
                        if (cls != null && string.Equals(cls.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            if (item.Scope == "CLASS" && item.TargetClassRoomId.HasValue)
            {
                return await _context.ClassTeachers.AnyAsync(ct =>
                    ct.ClassRoomId == item.TargetClassRoomId.Value &&
                    ct.UserId == CurrentUserId &&
                    (currentYearId == 0 || ct.AcademicYearId == currentYearId));
            }

            return false;
        }

        /// <summary>
        /// 1. GET: /Announcements (Xem & Lọc Bảng Tin Toàn Xứ / Khối / Lớp - TASK-P3-07.1)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(string? scope, string? gradeLevel, int? classId, string? search)
        {
            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var authorizedClasses = await GetAuthorizedClassesAsync(yearId);
            var authorizedClassIds = authorizedClasses.Select(c => c.Id).ToHashSet();

            var nowUtc = DateTime.UtcNow;
            var query = _context.Announcements
                .Include(a => a.TargetClassRoom)
                .Where(a => a.IsActive && (!a.ExpireAt.HasValue || a.ExpireAt.Value >= nowUtc));

            if (!string.IsNullOrEmpty(scope) && scope != "ALL")
            {
                if (scope == "PARISH")
                {
                    query = query.Where(a => a.Scope == "ALL" || a.Scope == "PARISH");
                }
                else
                {
                    query = query.Where(a => a.Scope == scope);
                }
            }

            if (!string.IsNullOrEmpty(gradeLevel) && gradeLevel != "ALL")
            {
                query = query.Where(a => a.TargetGradeLevel == gradeLevel ||
                                         (a.TargetClassRoom != null && a.TargetClassRoom.GradeLevel == gradeLevel));
            }

            if (classId.HasValue && classId.Value > 0)
            {
                query = query.Where(a => a.TargetClassRoomId == classId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                query = query.Where(a => a.Title.Contains(kw) || a.Content.Contains(kw));
            }

            var announcements = await query
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Classes = new SelectList(authorizedClasses, "Id", "Name", classId);
            ViewBag.AuthorizedClassIds = authorizedClassIds;
            ViewBag.CanPostAll = IsLeadership;
            ViewBag.CanPostGrade = IsLeadership || User.IsInRole(UserRole.BranchHead);
            ViewBag.CanPostClass = IsLeadership || authorizedClasses.Any();
            ViewBag.ManagedGradeLevel = User.FindFirst("ManagedGradeLevel")?.Value;

            ViewBag.SelectedScope = scope ?? "ALL";
            ViewBag.SelectedGrade = gradeLevel ?? "ALL";
            ViewBag.SelectedClassId = classId;
            ViewBag.SearchKeyword = search;

            return View(announcements);
        }

        /// <summary>
        /// 2. POST: /Announcements/Create (Hỗ trợ BĐH đăng Toàn xứ, Trưởng khối đăng theo Khối, GLV đăng dặn dò theo Lớp - TASK-P3-07.1)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> Create(
            [Bind("Title,Content,Scope,TargetGradeLevel,TargetClassRoomId,Priority,IsPinned,ExpireAt")] Announcement model)
        {
            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Content))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ Tiêu đề và Nội dung thông báo.";
                return RedirectToAction(nameof(Index));
            }

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;
            var authorizedClasses = await GetAuthorizedClassesAsync(yearId);

            // Kiểm tra và ép phạm vi đăng bài (Scope) theo đúng vai trò
            if (IsLeadership)
            {
                model.Scope = string.IsNullOrWhiteSpace(model.Scope) ? "ALL" : model.Scope.Trim().ToUpperInvariant();
            }
            else if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (model.Scope == "GRADE")
                {
                    if (string.IsNullOrWhiteSpace(managedGrade))
                    {
                        TempData["ErrorMessage"] = "Tài khoản Trưởng khối chưa được gán khối phụ trách.";
                        return RedirectToAction(nameof(Index));
                    }
                    model.TargetGradeLevel = managedGrade;
                    model.TargetClassRoomId = null;
                }
                else
                {
                    model.Scope = "CLASS";
                    if (!model.TargetClassRoomId.HasValue || !authorizedClasses.Any(c => c.Id == model.TargetClassRoomId.Value))
                    {
                        TempData["ErrorMessage"] = "Bạn chỉ được đăng thông báo cho các lớp thuộc khối mình phụ trách.";
                        return RedirectToAction(nameof(Index));
                    }
                    var targetCls = authorizedClasses.First(c => c.Id == model.TargetClassRoomId.Value);
                    model.TargetGradeLevel = targetCls.GradeLevel;
                }
            }
            else
            {
                // Giáo lý viên (Teacher): Chỉ được đăng thông báo dặn dò cho Lớp mình phụ trách (Scope = CLASS)
                model.Scope = "CLASS";
                if (!model.TargetClassRoomId.HasValue || !authorizedClasses.Any(c => c.Id == model.TargetClassRoomId.Value))
                {
                    TempData["ErrorMessage"] = "Bạn chỉ được phép đăng thông báo dặn dò cho lớp học mà mình được phân công phụ trách!";
                    return RedirectToAction(nameof(Index));
                }

                var targetCls = authorizedClasses.First(c => c.Id == model.TargetClassRoomId.Value);
                model.TargetGradeLevel = targetCls.GradeLevel;
            }

            if (model.Scope == "ALL" || model.Scope == "PARISH")
            {
                model.TargetGradeLevel = null;
                model.TargetClassRoomId = null;
            }
            else if (model.Scope == "GRADE")
            {
                model.TargetClassRoomId = null;
            }

            model.Title = model.Title.Trim();
            model.Content = model.Content.Trim();
            model.Priority = string.IsNullOrWhiteSpace(model.Priority) ? "NORMAL" : model.Priority.Trim().ToUpperInvariant();
            model.IsActive = true;
            model.CreatedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Giáo lý viên";
            model.CreatedAt = DateTime.UtcNow;

            if (model.ExpireAt.HasValue)
            {
                model.ExpireAt = DateTime.SpecifyKind(model.ExpireAt.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
            }

            _context.Announcements.Add(model);
            await _context.SaveChangesAsync();

            _logger.LogInformation("AUDIT ANNOUNCEMENT_CREATE: User {User} posted '{Title}' (Scope={Scope}, ClassId={ClassId})",
                User.Identity?.Name, model.Title, model.Scope, model.TargetClassRoomId);

            TempData["SuccessMessage"] = "Đã đăng bài thông báo / dặn dò thành công!";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 3. POST: /Announcements/Delete/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> Delete(long id)
        {
            var item = await _context.Announcements.FindAsync(id);
            if (item == null) return RedirectToAction(nameof(Index));

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            if (!await CanManageAnnouncementAsync(item, currentYear?.Id ?? 0))
            {
                return Forbid();
            }

            _context.Announcements.Remove(item);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa bài thông báo.";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 4. POST: /Announcements/TogglePin/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> TogglePin(long id)
        {
            var item = await _context.Announcements.FindAsync(id);
            if (item == null) return RedirectToAction(nameof(Index));

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            if (!await CanManageAnnouncementAsync(item, currentYear?.Id ?? 0))
            {
                return Forbid();
            }

            item.IsPinned = !item.IsPinned;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = item.IsPinned ? "Đã ghim bài dặn dò lên đầu bảng tin." : "Đã bỏ ghim bài viết.";

            return RedirectToAction(nameof(Index));
        }
    }
}