using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Model.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    public class AcademicYearsController : Controller
    {
        private readonly ILogger<AcademicYearsController> _logger;
        private readonly ParishDbContext _dbContext;
        public AcademicYearsController(ILogger<AcademicYearsController> logger, ParishDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        /// <summary>
        /// 1. GET: /AcademicYears (Danh sách niên khóa)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Truy xuất danh sách niên khóa giáo lý");

            var academicYears = await _dbContext.AcademicYears
                .OrderByDescending(y => y.StartDate)
                .AsNoTracking()
                .ToListAsync();

            return View(academicYears);
        }

        /// <summary>
        /// 2. POST: /AcademicYears/Create (Thêm niên khóa mới)
        /// </summary>
        /// <param name="academicYear"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,StartDate,EndDate,IsCurrent")] AcademicYear academicYear)
        {
            // Validate the model state
            if (academicYear.EndDate <= academicYear.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc niên khóa phải lớn hơn ngày bắt đầu.");
            }

            var isDuplicateName = await _dbContext.AcademicYears
                .AnyAsync(y => y.Name.Trim().ToLower() == academicYear.Name.Trim().ToLower());

            if (isDuplicateName)
            {
                ModelState.AddModelError("Name", $"Niên khóa '{academicYear.Name}' đã tồn tại trong hệ thống.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu nhập không hợp lệ, vui lòng kiểm tra lại.";
                var list = await _dbContext.AcademicYears.OrderByDescending(y => y.StartDate).ToListAsync();
                return View(nameof(Index), list);
            }
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Nếu niên khóa mới được kích hoạt là Current -> Chuyển tất cả niên khóa cũ về false
                if (academicYear.IsCurrent)
                {
                    var oldCurrentYears = await _dbContext.AcademicYears
                        .Where(y => y.IsCurrent)
                        .ToListAsync();

                    foreach (var old in oldCurrentYears)
                    {
                        old.IsCurrent = false;
                    }
                }

                _dbContext.AcademicYears.Add(academicYear);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Tạo mới thành công niên khóa: {Name} (Current={IsCurrent})",
                    academicYear.Name, academicYear.IsCurrent);

                TempData["SuccessMessage"] = $"Tạo niên khóa {academicYear.Name} thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi xảy ra khi lưu niên khóa mới {Name}", academicYear.Name);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu niên khóa vào cơ sở dữ liệu.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCurrent(int id)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var targetYear = await _dbContext.AcademicYears.FindAsync(id);
                if (targetYear == null)
                {
                    _logger.LogWarning("Không tìm thấy niên khóa có ID: {Id} để kích hoạt", id);
                    TempData["ErrorMessage"] = "Không tìm thấy niên khóa yêu cầu.";
                    return RedirectToAction(nameof(Index));
                }

                // Chuyển toàn bộ danh sách về false, chỉ bật duy nhất niên khóa target
                var allYears = await _dbContext.AcademicYears.ToListAsync();  
                foreach (var year in allYears)
                {
                    year.IsCurrent = (year.Id == id);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Đã kích hoạt niên khóa {Name} (ID={Id}) làm niên khóa hoạt động chính",
                    targetYear.Name, targetYear.Id);

                TempData["SuccessMessage"] = $"Đã kích hoạt niên khóa {targetYear.Name} làm niên khóa chính thức!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi kích hoạt niên khóa ID {Id}", id);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi đổi niên khóa hoạt động.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
