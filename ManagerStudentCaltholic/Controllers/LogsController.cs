using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = UserRole.Admin)]
    public class LogsController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LogsController> _logger;

        public LogsController(IWebHostEnvironment env, ILogger<LogsController> logger)
        {
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// GET: /Admin/Logs hoặc /Logs
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="level"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Admin/Logs")]
        [Route("Logs")]
        public async Task<IActionResult> Index(string? fileName, string? level, string? search)
        {
            var logsPath = Path.Combine(_env.ContentRootPath, "logs");
            var model = new SystemLogsViewModel
            {
                LevelFilter = level,
                SearchKeyword = search
            };

            if (!Directory.Exists(logsPath))
            {
                return View(model);
            }

            // Lấy danh sách các file .log xếp mới nhất lên đầu
            var files = Directory.GetFiles(logsPath, "*.log")
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .OrderByDescending(f => f)
                .ToList();

            model.AvailableLogFiles = files!;

            // Mặc định chọn file log mới nhất nếu không chỉ định
            var targetFile = string.IsNullOrEmpty(fileName) ? files.FirstOrDefault() : fileName;
            model.SelectedFile = targetFile ?? string.Empty;

            if (string.IsNullOrEmpty(targetFile))
            {
                return View(model);
            }

            var fullPath = Path.Combine(logsPath, targetFile);
            if (!System.IO.File.Exists(fullPath))
            {
                return View(model);
            }

            var entries = new List<LogEntryItem>();

            // Đọc file log an toàn với FileShare.ReadWrite để tránh xung đột với tiến trình ghi của Serilog
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string? line;
                LogEntryItem? currentEntry = null;

                // Pattern chuẩn: 2026-10-01 05:40:12.345 +07:00 [INF] Message... hoặc [05:40:12 INF]
                var logHeaderRegex = new Regex(@"^(?<ts>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}(\.\d{3})?(\s[+-]\d{2}:\d{2})?|\[\d{2}:\d{2}:\d{2})\s?\[(?<lvl>[A-Z]{3})\]\s(?<msg>.*)$");

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var match = logHeaderRegex.Match(line);
                    if (match.Success)
                    {
                        if (currentEntry != null)
                        {
                            entries.Add(currentEntry);
                        }

                        var lvl = match.Groups["lvl"].Value.ToUpperInvariant();
                        var msg = match.Groups["msg"].Value;
                        var tsRaw = match.Groups["ts"].Value.Trim('[', ']');
                        DateTime? parsedTs = null;

                        if (DateTime.TryParse(tsRaw, out var dt))
                        {
                            parsedTs = dt;
                        }

                        currentEntry = new LogEntryItem
                        {
                            Timestamp = parsedTs,
                            Level = lvl,
                            Message = msg,
                            RawLine = line
                        };
                    }
                    else
                    {
                        // Nếu là dòng Exception StackTrace tiếp nối
                        if (currentEntry != null)
                        {
                            currentEntry.Exception = string.IsNullOrEmpty(currentEntry.Exception)
                                ? line
                                : currentEntry.Exception + Environment.NewLine + line;
                        }
                    }
                }

                if (currentEntry != null)
                {
                    entries.Add(currentEntry);
                }
            }

            model.TotalLines = entries.Count;

            // Bộ lọc Level (ERR, WRN, INF, DBG)
            if (!string.IsNullOrEmpty(level))
            {
                entries = entries.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Bộ lọc từ khóa tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                entries = entries.Where(e =>
                    e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (e.Exception != null && e.Exception.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            // Đảo ngược thứ tự để log mới nhất hiển thị lên đầu tiên
            entries.Reverse();

            // Giới hạn hiển thị 1.000 dòng log gần nhất để tránh giật lag trình duyệt
            model.Entries = entries.Take(1000).ToList();

            return View(model);
        }

        /// <summary>
        /// GET: /Admin/Logs/Download?fileName=...
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Admin/Logs/Download")]
        public IActionResult Download(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Contains(".."))
            {
                return BadRequest("Tên file không hợp lệ.");
            }

            var logsPath = Path.Combine(_env.ContentRootPath, "logs");
            var fullPath = Path.Combine(logsPath, fileName);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("Không tìm thấy file log yêu cầu.");
            }

            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return File(fileStream, "text/plain", fileName);
        }
    }
}
