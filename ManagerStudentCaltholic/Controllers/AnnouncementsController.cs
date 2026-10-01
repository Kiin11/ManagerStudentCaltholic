using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    public class AnnouncementsController : Controller
    {
        private readonly ParishDbContext _context;
        public AnnouncementsController(ParishDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET: /Announcements
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="gradeLevel"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(string? scope, string? gradeLevel, string? search)
        {
            var query = _context.Announcements
                .Include(a => a.TargetClassRoom)
                .Where(a => a.IsActive);

            if (!string.IsNullOrEmpty(scope) && scope != "ALL")
            {
                query = query.Where(a => a.Scope == scope);
            }

            if (!string.IsNullOrEmpty(gradeLevel) && gradeLevel != "ALL")
            {
                query = query.Where(a => a.TargetGradeLevel == gradeLevel);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => a.Title.Contains(search) || a.Content.Contains(search));
            }

            var announcements = await query
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var classes = currentYear != null
                ? await _context.Classes.Where(c => c.AcademicYearId == currentYear.Id).OrderBy(c => c.Name).ToListAsync()
                : new List<ClassRoom>();

            ViewBag.Classes = new SelectList(classes, "Id", "Name");
            ViewBag.SelectedScope = scope ?? "ALL";
            ViewBag.SelectedGrade = gradeLevel ?? "ALL";
            ViewBag.SearchKeyword = search;

            return View(announcements);
        }

        /// <summary>
        /// POST: /Announcements/Create (Chỉ Admin và BĐH được đăng bài)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireExecutiveBoard")]
        public async Task<IActionResult> Create(Announcement model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Ban Quản Trị";
                model.CreatedAt = DateTime.UtcNow;

                _context.Announcements.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đăng thông báo mới thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Dữ liệu thông báo không hợp lệ. Vui lòng kiểm tra lại.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Announcements/Delete/5
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireExecutiveBoard")]
        public async Task<IActionResult> Delete(long id)
        {
            var item = await _context.Announcements.FindAsync(id);
            if (item != null)
            {
                _context.Announcements.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa bài thông báo.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Announcements/TogglePin/5
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireExecutiveBoard")]
        public async Task<IActionResult> TogglePin(long id)
        {
            var item = await _context.Announcements.FindAsync(id);
            if (item != null)
            {
                item.IsPinned = !item.IsPinned;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = item.IsPinned ? "Đã ghim bài viết lên đầu trang." : "Đã bỏ ghim bài viết.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
