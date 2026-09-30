using ManagerStudentCaltholic.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    public class HomeController : Controller 
    {
        private readonly ParishDbContext _context;
        public HomeController(ParishDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            // Lấy niên khóa hiện hành
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);

            int totalStudents = await _context.Students.CountAsync(s => s.IsActive);
            int totalClasses = 0;
            int totalEnrolled = 0;

            if (currentYear != null)
            {
                totalClasses = await _context.Classes.CountAsync(c => c.AcademicYearId == currentYear.Id);
                totalEnrolled = await _context.Enrollments.CountAsync(e => e.ClassRoom.AcademicYearId == currentYear.Id);
            }

            ViewBag.CurrentYearName = currentYear?.Name ?? "Chưa thiết lập";
            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalClasses = totalClasses;
            ViewBag.TotalEnrolled = totalEnrolled;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
