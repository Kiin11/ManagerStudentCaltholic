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
        /// SEC-NEW-01 FIX: Hàm kiểm tra và chuẩn hóa đường dẫn file log an toàn tuyệt đối
        /// </summary>
        private bool TryGetSafeLogPath(string? fileName, out string fullPath, out string safeFileName)
        {
            fullPath = string.Empty;
            safeFileName = string.Empty;

            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // 1. Tước bỏ hoàn toàn mọi ký tự đường dẫn thư mục (../, ..\, /, \, C:\...)
            safeFileName = Path.GetFileName(fileName.Trim());
            if (string.IsNullOrWhiteSpace(safeFileName) ||
                !string.Equals(safeFileName, fileName.Trim(), StringComparison.Ordinal) ||
                !safeFileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var logsRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "logs"));
            if (!logsRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                logsRoot += Path.DirectorySeparatorChar;
            }

            var candidatePath = Path.GetFullPath(Path.Combine(logsRoot, safeFileName));

            // 2. Đảm bảo đường dẫn đích bắt buộc nằm bên trong thư mục logsRoot
            if (!candidatePath.StartsWith(logsRoot, StringComparison.OrdinalIgnoreCase) ||
                !System.IO.File.Exists(candidatePath))
            {
                return false;
            }

            fullPath = candidatePath;
            return true;
        }

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

            var files = Directory.GetFiles(logsPath, "*.log")
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .OrderByDescending(f => f)
                .ToList();

            model.AvailableLogFiles = files!;

            var requestedFile = string.IsNullOrEmpty(fileName) ? files.FirstOrDefault() : fileName;
            if (string.IsNullOrEmpty(requestedFile))
            {
                return View(model);
            }

            // SEC-NEW-01 FIX: Xác thực tên file an toàn trước khi mở FileStream
            if (!TryGetSafeLogPath(requestedFile, out var fullPath, out var safeFileName))
            {
                _logger.LogWarning("Cảnh báo bảo mật: Phát hiện truy cập file log không hợp lệ '{FileName}' từ Admin {User}",
                    fileName, User.Identity?.Name);
                return BadRequest("Tên tệp nhật ký không hợp lệ.");
            }

            model.SelectedFile = safeFileName;
            var entries = new List<LogEntryItem>();

            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string? line;
                LogEntryItem? currentEntry = null;
                var logHeaderRegex = new Regex(@"^(?<ts>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}(\.\d{3})?(\s[+-]\d{2}:\d{2})?|\[\d{2}:\d{2}:\d{2})\s?\[(?<lvl>[A-Z]{3})\]\s(?<msg>.*)\$");

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var match = logHeaderRegex.Match(line);
                    if (match.Success)
                    {
                        if (currentEntry != null) entries.Add(currentEntry);

                        var lvl = match.Groups["lvl"].Value.ToUpperInvariant();
                        var msg = match.Groups["msg"].Value;
                        var tsRaw = match.Groups["ts"].Value.Trim('[', ']');
                        DateTime? parsedTs = DateTime.TryParse(tsRaw, out var dt) ? dt : null;

                        currentEntry = new LogEntryItem
                        {
                            Timestamp = parsedTs,
                            Level = lvl,
                            Message = msg,
                            RawLine = line
                        };
                    }
                    else if (currentEntry != null)
                    {
                        currentEntry.Exception = string.IsNullOrEmpty(currentEntry.Exception)
                            ? line
                            : currentEntry.Exception + Environment.NewLine + line;
                    }
                }

                if (currentEntry != null) entries.Add(currentEntry);
            }

            model.TotalLines = entries.Count;

            if (!string.IsNullOrEmpty(level))
            {
                entries = entries.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                entries = entries.Where(e =>
                    e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (e.Exception != null && e.Exception.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            entries.Reverse();
            model.Entries = entries.Take(1000).ToList();

            return View(model);
        }

        [HttpGet]
        [Route("Admin/Logs/Download")]
        public IActionResult Download(string fileName)
        {
            // SEC-NEW-01 FIX: Dùng chung hàm kiểm tra Path Traversal nghiêm ngặt
            if (!TryGetSafeLogPath(fileName, out var fullPath, out var safeFileName))
            {
                _logger.LogWarning("Cảnh báo bảo mật: Phát hiện tải file log trái phép '{FileName}' bởi {User}",
                    fileName, User.Identity?.Name);
                return BadRequest("Tên file log không hợp lệ hoặc không tồn tại.");
            }

            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return File(fileStream, "text/plain", safeFileName);
        }
    }
}