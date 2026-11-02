using ManagerStudentCaltholic.Interface.Services;
using Microsoft.AspNetCore.Mvc;

namespace ManagerStudentCaltholic.Controllers
{
    public class HomeController : Controller
    {
        private readonly IFeatureNavigationService _navService;
        private readonly IDeviceStudentRecognitionService _deviceRecognitionService;

        public HomeController(
            IFeatureNavigationService navService,
            IDeviceStudentRecognitionService deviceRecognitionService)
        {
            _navService = navService;
            _deviceRecognitionService = deviceRecognitionService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? master = "COMMON")
        {
            var workspace = await _navService.GetAuthorizedWorkspaceAsync(User, master);

            // FEAT-P4-07: Tự động nhận diện (Detect) thiết bị đã tra cứu học sinh nào để hiển thị thông báo của lớp đó
            workspace.DeviceStudentContext = await _deviceRecognitionService.DetectStudentsAndLoadAnnouncementsAsync(HttpContext);

            return View(workspace);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DetachRememberedStudent(string? studentCode)
        {
            _deviceRecognitionService.DetachStudentFromDevice(HttpContext, studentCode);

            TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(studentCode)
                ? "Đã gỡ toàn bộ thông tin thiếu nhi ghi nhớ trên thiết bị này."
                : $"Đã gỡ ghi nhớ mã thiếu nhi {studentCode} khỏi thiết bị.";

            return RedirectToAction(nameof(Index), new { master = "COMMON" });
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