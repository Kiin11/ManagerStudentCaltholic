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

        private bool TryGetSafeLogPath(string? fileName, out string fullPath, out string safeFileName)
        {
            fullPath = string.Empty;
            safeFileName = string.Empty;

            if (string.IsNullOrWhiteSpace(fileName))
                return false;

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
            // Tự động ghi 1 dòng log kiểm tra để đảm bảo thư mục và file log được flush
            var logsPath = Path.Combine(_env.ContentRootPath, "logs");
            var model = new SystemLogsViewModel
            {
                LevelFilter = level,
                SearchKeyword = search
            };

            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
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

            if (!TryGetSafeLogPath(requestedFile, out var fullPath, out var safeFileName))
            {
                _logger.LogWarning("Cảnh báo bảo mật: Phát hiện truy cập file log không hợp lệ '{FileName}' từ Admin {User}",
                    fileName, User.Identity?.Name);
                return BadRequest("Tên tệp nhật ký không hợp lệ.");
            }

            model.SelectedFile = safeFileName;
            var entries = new List<LogEntryItem>();

            // FIX ERR-P3.03.5: Bỏ dấu backslash trước '$' ở cuối Regex và hỗ trợ đầy đủ định dạng Timestamp của Serilog
            var logHeaderRegex = new Regex(
                @"^(?<ts>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(\.\d{3,7})?(\s*[+-]\d{2}:\d{2})?|\[\d{2}:\d{2}:\d{2}\]?)\s*\[(?<lvl>[A-Z]{3,4})\]\s*(?<msg>.*)$",
                RegexOptions.Compiled);

            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string? line;
                LogEntryItem? currentEntry = null;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var match = logHeaderRegex.Match(line);
                    if (match.Success)
                    {
                        if (currentEntry != null) entries.Add(currentEntry);

                        var lvl = match.Groups["lvl"].Value.ToUpperInvariant();
                        var msg = match.Groups["msg"].Value;
                        var tsRaw = match.Groups["ts"].Value.Trim('[', ']', ' ');
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
                    else
                    {
                        // Fallback: Nếu dòng log không khớp chuẩn Regex vẫn giữ lại để hiển thị
                        currentEntry = new LogEntryItem
                        {
                            Timestamp = null,
                            Level = "INF",
                            Message = line,
                            RawLine = line
                        };
                    }
                }

                if (currentEntry != null) entries.Add(currentEntry);
            }

            model.TotalLines = entries.Count;

            if (!string.IsNullOrEmpty(level) && level != "ALL")
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