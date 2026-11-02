using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
    public class FeaturePermissionsController : Controller
    {
        private readonly IFeatureNavigationService _navService;
        private readonly ParishDbContext _context;

        public FeaturePermissionsController(
            IFeatureNavigationService navService,
            ParishDbContext context)
        {
            _navService = navService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var classes = currentYear != null
                ? await _context.Classes
                    .AsNoTracking()
                    .Where(c => c.AcademicYearId == currentYear.Id)
                    .OrderBy(c => c.GradeLevel)
                    .ThenBy(c => c.Name)
                    .ToListAsync()
                : new List<ClassRoom>();

            var vm = new FeaturePermissionManageViewModel
            {
                Rules = await _navService.GetAllRulesForManagementAsync(),
                CurrentYearClasses = classes
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBatch([FromBody] List<SaveFeaturePermissionItemDto> items)
        {
            var operatorName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Admin";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            var (success, message) = await _navService.UpdatePermissionRulesBatchAsync(items, operatorName, ip);
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
    }
}