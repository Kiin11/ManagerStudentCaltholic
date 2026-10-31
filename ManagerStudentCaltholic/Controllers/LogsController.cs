using System.Diagnostics;
using System.Text.RegularExpressions;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = UserRole.Admin)]
    public class LogsController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ParishDbContext _context;
        private readonly IDistributedCache? _distributedCache;
        private readonly SystemMetricsCollector? _metricsCollector;
        private readonly ILogger<LogsController> _logger;

        // Regex bóc tách chuẩn dòng Header của Serilog
        private static readonly Regex LogHeaderRegex = new(
            @"^(?<ts>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(\.\d{3,7})?(\s*[+-]\d{2}:\d{2})?|\[\d{2}:\d{2}:\d{2}\]?)\s*\[(?<lvl>[A-Z]{3,4})\]\s*(?<msg>.*)$",
            RegexOptions.Compiled);

        // Regex bóc tách thông số HTTP Request (Method, Path, StatusCode, ElapsedMs, User, IP)
        private static readonly Regex HttpContextRegex = new(
            @"HTTP\s+(?<method>GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s+(?<path>\S+)\s+responded\s+(?<status>\d{3})\s+in\s+(?<elapsed>[\d\.]+)\s*ms(\s*\|\s*User=(?<user>[^|\s]+))?(\s*\|\s*IP=(?<ip>[^\s]+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UserFallbackRegex = new(
            @"(?:User=|User\s+|bởi\s+|Admin\s+)(?<user>[a-zA-Z0-9_\.\-@]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex IpFallbackRegex = new(
            @"IP[:=]\s*(?<ip>[0-9a-fA-F\.:]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public LogsController(
            IWebHostEnvironment env,
            ParishDbContext context,
            ILogger<LogsController> logger,
            IDistributedCache? distributedCache = null,
            SystemMetricsCollector? metricsCollector = null)
        {
            _env = env;
            _context = context;
            _logger = logger;
            _distributedCache = distributedCache;
            _metricsCollector = metricsCollector;
        }

        /// <summary>
        /// SEC-NEW-01: Chống Path Traversal tuyệt đối khi truy cập tệp trong thư mục logs
        /// </summary>
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

        /// <summary>
        /// 1. GET: /Admin/Logs (System Health Dashboard & Trình quản lý Log Tập trung - FEAT-P3-11)
        /// </summary>
        [HttpGet]
        [Route("Admin/Logs")]
        [Route("Logs")]
        public async Task<IActionResult> Index(
            string? fileName,
            string? level,
            string? module,
            string? search,
            TimeSpan? fromTime,
            TimeSpan? toTime,
            int page = 1,
            int pageSize = 50)
        {
            pageSize = Math.Clamp(pageSize, 20, 200);
            var logsPath = Path.Combine(_env.ContentRootPath, "logs");

            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
            }

            var logFileInfos = new DirectoryInfo(logsPath)
                .GetFiles("*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ThenByDescending(f => f.Name)
                .ToList();

            var files = logFileInfos.Select(f => f.Name).ToList();
            double totalLogsMb = Math.Round(logFileInfos.Sum(f => f.Length) / (1024.0 * 1024.0), 2);

            var model = new SystemLogsViewModel
            {
                AvailableLogFiles = files,
                LevelFilter = string.IsNullOrWhiteSpace(level) ? "ALL" : level.Trim().ToUpperInvariant(),
                ModuleFilter = string.IsNullOrWhiteSpace(module) ? "ALL" : module.Trim().ToUpperInvariant(),
                SearchKeyword = search,
                FromTime = fromTime,
                ToTime = toTime,
                CurrentPage = Math.Max(1, page),
                PageSize = pageSize,
                Health = await CheckSystemHealthAsync(files.Count, totalLogsMb)
            };

            // Khởi tạo sẵn 24 mốc giờ cho biểu đồ
            var hourlyMap = Enumerable.Range(0, 24)
                .ToDictionary(h => h, h => new HourlyLogStatPointDto { Hour = h });

            var requestedFile = string.IsNullOrEmpty(fileName) ? files.FirstOrDefault() : fileName;
            if (string.IsNullOrEmpty(requestedFile))
            {
                model.HourlyStats = hourlyMap.Values.OrderBy(x => x.Hour).ToList();
                return View(model);
            }

            if (!TryGetSafeLogPath(requestedFile, out var fullPath, out var safeFileName))
            {
                _logger.LogWarning("Cảnh báo bảo mật: Phát hiện truy cập file log không hợp lệ '{FileName}' từ Admin {User}",
                    fileName, User.Identity?.Name);
                return BadRequest("Tên tệp nhật ký không hợp lệ.");
            }

            model.SelectedFile = safeFileName;
            var allEntries = await ParseLogFileEntriesAsync(fullPath);

            model.TotalLines = allEntries.Count;
            model.InfoCount = allEntries.Count(e => e.Level == "INF");
            model.WarningCount = allEntries.Count(e => e.Level == "WRN");
            model.ErrorCount = allEntries.Count(e => e.Level == "ERR");
            model.FatalCount = allEntries.Count(e => e.Level == "FTL");
            model.AuditCount = allEntries.Count(e => e.Module == "KIỂM TOÁN (AUDIT)");

            // Tổng hợp số liệu theo 24 khung giờ và theo từng Module
            foreach (var entry in allEntries)
            {
                if (entry.Timestamp.HasValue)
                {
                    int hr = entry.Timestamp.Value.Hour;
                    if (hourlyMap.TryGetValue(hr, out var point))
                    {
                        if (entry.Level == "ERR" || entry.Level == "FTL") point.ErrorCount++;
                        else if (entry.Level == "WRN") point.WarningCount++;
                        else point.InfoCount++;
                    }
                }

                if (!model.ModuleBreakdown.ContainsKey(entry.Module))
                {
                    model.ModuleBreakdown[entry.Module] = 0;
                }
                model.ModuleBreakdown[entry.Module]++;
            }

            model.HourlyStats = hourlyMap.Values.OrderBy(x => x.Hour).ToList();

            // Áp dụng bộ lọc đa tiêu chí (TASK-P3-11.2)
            IEnumerable<LogEntryItem> filtered = allEntries;

            if (model.LevelFilter != "ALL")
            {
                if (model.LevelFilter == "ERR_FTL")
                {
                    filtered = filtered.Where(e => e.Level == "ERR" || e.Level == "FTL");
                }
                else
                {
                    filtered = filtered.Where(e => e.Level.Equals(model.LevelFilter, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (model.ModuleFilter != "ALL")
            {
                filtered = filtered.Where(e => e.Module.Equals(model.ModuleFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (fromTime.HasValue)
            {
                filtered = filtered.Where(e => e.Timestamp.HasValue && e.Timestamp.Value.TimeOfDay >= fromTime.Value);
            }

            if (toTime.HasValue)
            {
                filtered = filtered.Where(e => e.Timestamp.HasValue && e.Timestamp.Value.TimeOfDay <= toTime.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim();
                filtered = filtered.Where(e =>
                    e.Message.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    (e.Exception != null && e.Exception.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (e.PerformedBy != null && e.PerformedBy.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (e.IpAddress != null && e.IpAddress.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (e.RequestPath != null && e.RequestPath.Contains(kw, StringComparison.OrdinalIgnoreCase)));
            }

            // Đảo ngược để dòng log mới nhất hiển thị lên đầu
            var orderedList = filtered.Reverse().ToList();
            model.FilteredCount = orderedList.Count;
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.FilteredCount / (double)pageSize));
            model.CurrentPage = Math.Min(model.CurrentPage, model.TotalPages);

            model.Entries = orderedList
                .Skip((model.CurrentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(model);
        }

        /// <summary>
        /// 2. GET: /Admin/Logs/HealthStats (API lấy nhanh trạng thái Sức khỏe Hệ thống - TASK-P3-11.4)
        /// </summary>
        [HttpGet]
        [Route("Admin/Logs/HealthStats")]
        public async Task<IActionResult> HealthStats()
        {
            var logsPath = Path.Combine(_env.ContentRootPath, "logs");
            var logFileInfos = Directory.Exists(logsPath)
                ? new DirectoryInfo(logsPath).GetFiles("*.log")
                : Array.Empty<FileInfo>();

            double totalMb = Math.Round(logFileInfos.Sum(f => f.Length) / (1024.0 * 1024.0), 2);
            var health = await CheckSystemHealthAsync(logFileInfos.Length, totalMb);
            return Json(new { success = true, data = health });
        }

        /// <summary>
        /// 3. GET: /Admin/Logs/Download (Tải tệp log gốc về máy)
        /// </summary>
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

        #region Helpers Phân tích Structured Log & Kiểm tra Sức khỏe Hạ tầng
        private async Task<List<LogEntryItem>> ParseLogFileEntriesAsync(string fullPath)
        {
            var entries = new List<LogEntryItem>();
            int idx = 1;

            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            string? line;
            LogEntryItem? currentEntry = null;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = LogHeaderRegex.Match(line);
                if (match.Success)
                {
                    if (currentEntry != null)
                    {
                        EnrichLogEntryMetadata(currentEntry);
                        entries.Add(currentEntry);
                    }

                    var lvl = match.Groups["lvl"].Value.ToUpperInvariant();
                    var msg = match.Groups["msg"].Value;
                    var tsRaw = match.Groups["ts"].Value.Trim('[', ']', ' ');
                    DateTime? parsedTs = DateTime.TryParse(tsRaw, out var dt) ? dt : null;

                    currentEntry = new LogEntryItem
                    {
                        Index = idx++,
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
                    currentEntry = new LogEntryItem
                    {
                        Index = idx++,
                        Timestamp = null,
                        Level = "INF",
                        Message = line,
                        RawLine = line
                    };
                }
            }

            if (currentEntry != null)
            {
                EnrichLogEntryMetadata(currentEntry);
                entries.Add(currentEntry);
            }

            return entries;
        }

        /// <summary>
        /// Tự động nhận diện Phân hệ (Module), Người dùng, IP, HTTP Status và Thời gian phản hồi từ nội dung Log
        /// </summary>
        private static void EnrichLogEntryMetadata(LogEntryItem item)
        {
            var msg = item.Message ?? string.Empty;

            // 1. Bóc tách thông số HTTP nếu là dòng log Request
            var httpMatch = HttpContextRegex.Match(msg);
            if (httpMatch.Success)
            {
                item.HttpMethod = httpMatch.Groups["method"].Value.ToUpperInvariant();
                item.RequestPath = httpMatch.Groups["path"].Value;
                if (int.TryParse(httpMatch.Groups["status"].Value, out int st)) item.StatusCode = st;
                if (double.TryParse(httpMatch.Groups["elapsed"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double el))
                {
                    item.ElapsedMs = (long)Math.Round(el);
                }
                if (httpMatch.Groups["user"].Success) item.PerformedBy = httpMatch.Groups["user"].Value;
                if (httpMatch.Groups["ip"].Success) item.IpAddress = httpMatch.Groups["ip"].Value;
            }
            else
            {
                var uMatch = UserFallbackRegex.Match(msg);
                if (uMatch.Success) item.PerformedBy = uMatch.Groups["user"].Value;

                var ipMatch = IpFallbackRegex.Match(msg);
                if (ipMatch.Success) item.IpAddress = ipMatch.Groups["ip"].Value;
            }

            // 2. Phân loại Module nghiệp vụ
            var combined = (msg + " " + (item.RequestPath ?? "")).ToUpperInvariant();

            if (combined.Contains("AUDIT"))
                item.Module = "KIỂM TOÁN (AUDIT)";
            else if (combined.Contains("SCANQR") || combined.Contains("SCANCHECKIN") || combined.Contains("QUÉT QR") || combined.Contains("MÃ QR"))
                item.Module = "QUÉT QR";
            else if (combined.Contains("/ATTENDANCE") || combined.Contains("ĐIỂM DANH") || combined.Contains("ABSENCE"))
                item.Module = "ĐIỂM DANH";
            else if (combined.Contains("PUBLICREGISTRATION") || combined.Contains("STUDENTREGISTRATIONS") || combined.Contains("TUYỂN SINH"))
                item.Module = "TUYỂN SINH";
            else if (combined.Contains("/SACRAMENTS") || combined.Contains("BÍ TÍCH"))
                item.Module = "BÍ TÍCH";
            else if (combined.Contains("/GRADES") || combined.Contains("SỔ ĐIỂM") || combined.Contains("PROMOTION"))
                item.Module = "HỌC VỤ & ĐIỂM";
            else if (combined.Contains("/ACCOUNT") || combined.Contains("LOGIN") || combined.Contains("TOKEN") || combined.Contains("CẢNH BÁO BẢO MẬT") || combined.Contains("SECURITY"))
                item.Module = "BẢO MẬT & AUTH";
            else
                item.Module = "HỆ THỐNG";
        }

        private async Task<SystemHealthSummaryDto> CheckSystemHealthAsync(int logFilesCount, double totalLogsMb)
        {
            var health = new SystemHealthSummaryDto
            {
                TotalLogFilesCount = logFilesCount,
                TotalLogsSizeMb = totalLogsMb
            };

            // 1. Đo kết nối và độ trễ PostgreSQL
            var dbSw = Stopwatch.StartNew();
            try
            {
                health.IsDatabaseUp = await _context.Database.CanConnectAsync();
                dbSw.Stop();
                health.DatabaseLatencyMs = dbSw.ElapsedMilliseconds;
            }
            catch
            {
                dbSw.Stop();
                health.IsDatabaseUp = false;
                health.DatabaseLatencyMs = -1;
            }

            // 2. Đo kết nối và độ trễ Redis Cache
            if (_distributedCache != null)
            {
                var redisSw = Stopwatch.StartNew();
                try
                {
                    const string pingKey = "health_check_ping";
                    await _distributedCache.SetStringAsync(pingKey, "OK", new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                    });
                    var val = await _distributedCache.GetStringAsync(pingKey);
                    redisSw.Stop();
                    health.IsRedisUp = val == "OK";
                    health.RedisLatencyMs = redisSw.ElapsedMilliseconds;
                }
                catch
                {
                    redisSw.Stop();
                    health.IsRedisUp = false;
                    health.RedisLatencyMs = -1;
                }
            }

            // 3. Lấy thông số RAM, Uptime và Lưu lượng từ SystemMetricsCollector
            if (_metricsCollector != null)
            {
                health.MemoryUsageMb = _metricsCollector.GetProcessMemoryMb();
                health.RequestsPerMinute = _metricsCollector.GetCurrentRequestsPerMinute();
                health.AverageResponseMs = _metricsCollector.AverageResponseTimeMs;
                health.TotalRequestsSinceStart = _metricsCollector.TotalRequests;

                var up = _metricsCollector.Uptime;
                health.UptimeDisplay = up.TotalDays >= 1
                    ? $"{(int)up.TotalDays} ngày {up.Hours} giờ {up.Minutes} phút"
                    : $"{up.Hours} giờ {up.Minutes} phút {up.Seconds} giây";
            }
            else
            {
                using var proc = Process.GetCurrentProcess();
                health.MemoryUsageMb = Math.Round(proc.WorkingSet64 / (1024.0 * 1024.0), 1);
                health.UptimeDisplay = "Đang hoạt động";
            }

            return health;
        }
        #endregion
    }
}