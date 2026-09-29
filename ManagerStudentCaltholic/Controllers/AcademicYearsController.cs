using ManagerStudentCaltholic.Data;
using Microsoft.AspNetCore.Mvc;

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
        public IActionResult Index()
        {
            return View();
        }
    }
}
