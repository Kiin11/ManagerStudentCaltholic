using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireExecutiveBoard")]
    public class AcademicYearsController : Controller
    {
        private readonly ILogger<AcademicYearsController> _logger;
        private readonly ParishDbContext _dbContext;
        private readonly ICacheService _cache;

        public AcademicYearsController(
            ILogger<AcademicYearsController> logger,
            ParishDbContext dbContext,
            ICacheService cache)
        {
            _logger = logger;
            _dbContext = dbContext;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var academicYears = await _cache.GetOrSetAsync(
                CacheKeys.AllAcademicYears,
                async () => await _dbContext.AcademicYears
                    .OrderByDescending(y => y.StartDate)
                    .AsNoTracking()
                    .ToListAsync(),
                TimeSpan.FromHours(24));

            return View(academicYears);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AcademicYear academicYear)
        {
            if (academicYear.EndDate <= academicYear.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn ngày bắt đầu.");
            }

            var isDuplicate = await _dbContext.AcademicYears
                .AnyAsync(y => y.Name.Trim().ToLower() == academicYear.Name.Trim().ToLower());

            if (isDuplicate)
            {
                ModelState.AddModelError("Name", $"Niên khóa '{academicYear.Name}' đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu nhập không hợp lệ, vui lòng kiểm tra lại.";
                var list = await _dbContext.AcademicYears.OrderByDescending(y => y.StartDate).AsNoTracking().ToListAsync();
                return View(nameof(Index), list);
            }

            try
            {
                academicYear.StartDate = DateTime.SpecifyKind(academicYear.StartDate, DateTimeKind.Utc);
                academicYear.EndDate = DateTime.SpecifyKind(academicYear.EndDate, DateTimeKind.Utc);
                var strategy = _dbContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                    if (academicYear.IsCurrent)
                    {
                        var currentYears = await _dbContext.AcademicYears
                            .Where(y => y.IsCurrent)
                            .ToListAsync();

                        foreach (var y in currentYears)
                        {
                            y.IsCurrent = false;
                        }
                    }

                    _dbContext.AcademicYears.Add(academicYear);
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                // TASK-P3-09.4: Xóa Cache Niên khóa ngay sau khi tạo mới
                await _cache.InvalidateAcademicYearsCacheAsync();

                _logger.LogInformation("Tạo mới thành công niên khóa: {Name} (Current={IsCurrent})",
                    academicYear.Name, academicYear.IsCurrent);

                TempData["SuccessMessage"] = $"Tạo niên khóa {academicYear.Name} thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi lưu niên khóa {Name}", academicYear.Name);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu niên khóa vào cơ sở dữ liệu.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCurrent(int id)
        {
            try
            {
                var strategy = _dbContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                    var targetYear = await _dbContext.AcademicYears.FindAsync(id);
                    if (targetYear == null) return;

                    var allYears = await _dbContext.AcademicYears.ToListAsync();
                    foreach (var year in allYears)
                    {
                        year.IsCurrent = (year.Id == id);
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                // TASK-P3-09.4: Xóa Cache Niên khóa ngay khi đổi niên khóa hiện hành
                await _cache.InvalidateAcademicYearsCacheAsync();

                TempData["SuccessMessage"] = "Đã kích hoạt niên khóa thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kích hoạt niên khóa ID {Id}", id);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi kích hoạt niên khóa.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}