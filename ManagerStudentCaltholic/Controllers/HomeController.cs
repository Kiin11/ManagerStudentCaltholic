using System.Security.Claims;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ManagerStudentCaltholic.Controllers
{
    public class HomeController : Controller
    {
        private readonly IFeatureNavigationService _navService;
        private readonly IDeviceStudentRecognitionService _deviceRecognitionService;
        private readonly IStaffStudentLookupService _lookupService;
        private readonly ICacheService _cacheService;

        public HomeController(
            IFeatureNavigationService navService,
            IDeviceStudentRecognitionService deviceRecognitionService,
            IStaffStudentLookupService lookupService,
            ICacheService cacheService)
        {
            _navService = navService;
            _deviceRecognitionService = deviceRecognitionService;
            _lookupService = lookupService;
            _cacheService = cacheService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? master = "COMMON")
        {
            var workspace = await _navService.GetAuthorizedWorkspaceAsync(User, master);

            // FEAT-P4-07: Tự động nhận diện (Detect) thiết bị đã tra cứu học sinh nào để hiển thị thông báo của lớp đó
            workspace.DeviceStudentContext = await _deviceRecognitionService.DetectStudentsAndLoadAnnouncementsAsync(HttpContext);

            // FEAT-P5-01: Nếu GLV/BĐH đã đăng nhập, tải trước danh sách tùy chọn tra cứu & danh sách đã tra cứu gần đây
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (long.TryParse(userIdClaim, out long userId))
                {
                    workspace.RecentStaffLookups = await _lookupService.GetRecentLookupsAsync(userId, 5);
                }
                workspace.LookupOptions = await _lookupService.GetViewOptionsAsync();
            }

            return View(workspace);
        }

        // FEAT-P5-01: API Gợi ý tìm kiếm tức thời cho GLV theo Tên, Tên Thánh, Mã hiện hành hoặc Mã năm cũ
        [HttpGet]
        public async Task<IActionResult> StaffSearchSuggestions(string? term)
        {
            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để tra cứu nội bộ." });

            if (string.IsNullOrWhiteSpace(term))
                return Json(new { success = true, data = new List<object>() });

            var results = await _lookupService.SearchStudentsQuickAsync(term, 8);
            return Json(new { success = true, data = results });
        }

        // FEAT-P5-01: API Tra cứu 360 độ Thiếu nhi kèm bộ chuyển Option (OVERVIEW, ATTENDANCE_DETAIL, ACADEMIC_2Y_SACRAMENT, PARENT_CONTACT)
        // Kết hợp Redis Cache TTL 5 phút để chuyển Option tức thời không query lại DB
        [HttpGet]
        public async Task<IActionResult> StaffStudentLookup(long studentId, string? option = "OVERVIEW", string? searchTerm = null)
        {
            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để tra cứu nội bộ." });

            if (studentId <= 0)
                return BadRequest(new { success = false, message = "Mã thiếu nhi không hợp lệ." });

            var chosenOption = string.IsNullOrWhiteSpace(option) ? "OVERVIEW" : option.ToUpperInvariant();

            // Sub-task 01.3.2: Cache ngắn hạn 5 phút với RedisCacheService
            var cacheKey = $"staff_lookup:student_360:{studentId}";
            var student360 = await _cacheService.GetOrSetAsync(
                cacheKey,
                async () => await _lookupService.GetStudent360DetailAsync(studentId, chosenOption),
                TimeSpan.FromMinutes(5)
            );

            if (student360 == null)
                return NotFound(new { success = false, message = "Không tìm thấy thông tin thiếu nhi." });

            // Gán option hiện hành được người dùng chọn
            student360.ActiveOption = chosenOption;

            // Ghi nhận lịch sử tra cứu của GLV
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(userIdClaim, out long userId))
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
                await _lookupService.RecordLookupLogAsync(
                    userId,
                    studentId,
                    searchTerm ?? student360.BasicInfo.StudentCode,
                    chosenOption,
                    clientIp);
            }

            return Json(new { success = true, message = "Tra cứu thành công", data = student360 });
        }

        // FEAT-P5-01: API lấy danh sách học sinh vừa tra cứu gần đây của GLV
        [HttpGet]
        public async Task<IActionResult> StaffRecentLookups()
        {
            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập." });

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdClaim, out long userId))
                return Json(new { success = true, data = new List<object>() });

            var recents = await _lookupService.GetRecentLookupsAsync(userId, 6);
            return Json(new { success = true, data = recents });
        }

        // FEAT-P5-01: API lấy danh sách chế độ xem
        [HttpGet]
        public async Task<IActionResult> StaffLookupOptions()
        {
            var options = await _lookupService.GetViewOptionsAsync();
            return Json(new { success = true, data = options });
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
