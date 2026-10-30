using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagerStudentCaltholic.Controllers
{
    [AllowAnonymous]
    public class PwaController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public PwaController(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// GET: /Offline (Trang hiển thị dự phòng khi thiết bị mất kết nối mạng)
        /// </summary>
        [HttpGet("/Offline")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public IActionResult Offline()
        {
            return View();
        }

        /// <summary>
        /// GET: /icons/{fileName} (Đảm bảo luôn trả về Icon hợp lệ cho trình cài đặt PWA trên Android & iOS)
        /// </summary>
        [HttpGet("/icons/{fileName}")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public IActionResult GetAppIcon(string fileName)
        {
            var safeName = Path.GetFileName(fileName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                return NotFound();
            }

            var iconsDir = Path.Combine(_env.WebRootPath, "icons");
            var requestedIconPath = Path.Combine(iconsDir, safeName);

            if (System.IO.File.Exists(requestedIconPath))
            {
                return PhysicalFile(requestedIconPath, "image/png");
            }

            // Fallback về logo giáo xứ hiện có nếu chưa tạo file ảnh riêng trong thư mục wwwroot/icons
            var fallbackLogoPath = Path.Combine(_env.WebRootPath, "image", "logo.jpg");
            if (System.IO.File.Exists(fallbackLogoPath))
            {
                return PhysicalFile(fallbackLogoPath, "image/png");
            }

            return NotFound();
        }
    }
}