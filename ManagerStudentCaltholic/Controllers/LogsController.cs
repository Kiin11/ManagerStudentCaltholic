using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Options;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = UserRole.Admin)]
    public class LogsController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ParishDbContext _context;
        private readonly IDistributedCache? _distributedCache;
        private readonly SystemMetricsCollector? _metricsCollector;
        private readonly IGoogleDriveStorageService? _driveStorageService;
        private readonly IDriveLogUploadService? _driveLogUploadService;
        private readonly ILocalMediaMigrationService? _localMediaMigrationService;
        private readonly DriveLogUploadOptions _driveLogOptions;
        private readonly ILogger<LogsController> _logger;

        private const long MaxLogTailBytes = 5L * 1024 * 1024; // 5 MB tail read cap for 1 GiB VM safety
        private const int MaxParsedLogEntries = 5000;
        private const int MaxExceptionChars = 64 * 1024;

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
            SystemMetricsCollector? metricsCollector = null,
            IGoogleDriveStorageService? driveStorageService = null,
            IDriveLogUploadService? driveLogUploadService = null,
            ILocalMediaMigrationService? localMediaMigrationService = null,
            IOptions<DriveLogUploadOptions>? driveLogOptions = null)
        {
            _env = env;
            _context = context;
            _logger = logger;
            _distributedCache = distributedCache;
            _metricsCollector = metricsCollector;
            _driveStorageService = driveStorageService;
            _driveLogUploadService = driveLogUploadService;
            _localMediaMigrationService = localMediaMigrationService;
            _driveLogOptions = driveLogOptions?.Value ?? new DriveLogUploadOptions();
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

            await PopulateDriveStorageSectionAsync(model, HttpContext.RequestAborted);

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
            var allEntries = await ParseLogFileEntriesAsync(fullPath, HttpContext.RequestAborted);

            model.TotalLines = allEntries.Count;

            // Tổng hợp số liệu theo cấp độ, 24 khung giờ và từng Module trong một vòng lặp
            foreach (var entry in allEntries)
            {
                switch (entry.Level)
                {
                    case "INF": model.InfoCount++; break;
                    case "WRN": model.WarningCount++; break;
                    case "ERR": model.ErrorCount++; break;
                    case "FTL": model.FatalCount++; break;
                }

                if (entry.Module == "KIỂM TOÁN (AUDIT)")
                {
                    model.AuditCount++;
                }

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

            // Áp dụng bộ lọc đa tiêu chí (TASK-P3-11.2) không cấp phát bản sao Reverse().ToList()
            var kw = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            bool MatchesFilters(LogEntryItem e)
            {
                if (model.LevelFilter != "ALL")
                {
                    if (model.LevelFilter == "ERR_FTL")
                    {
                        if (e.Level != "ERR" && e.Level != "FTL") return false;
                    }
                    else if (!e.Level.Equals(model.LevelFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                if (model.ModuleFilter != "ALL" && !e.Module.Equals(model.ModuleFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (fromTime.HasValue && (!e.Timestamp.HasValue || e.Timestamp.Value.TimeOfDay < fromTime.Value))
                {
                    return false;
                }

                if (toTime.HasValue && (!e.Timestamp.HasValue || e.Timestamp.Value.TimeOfDay > toTime.Value))
                {
                    return false;
                }

                if (kw != null)
                {
                    bool matched =
                        e.Message.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                        (e.Exception != null && e.Exception.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (e.PerformedBy != null && e.PerformedBy.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (e.IpAddress != null && e.IpAddress.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (e.RequestPath != null && e.RequestPath.Contains(kw, StringComparison.OrdinalIgnoreCase));
                    if (!matched) return false;
                }

                return true;
            }

            int filteredCount = 0;
            for (int i = 0; i < allEntries.Count; i++)
            {
                if (MatchesFilters(allEntries[i]))
                {
                    filteredCount++;
                }
            }

            model.FilteredCount = filteredCount;
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)pageSize));
            model.CurrentPage = Math.Min(model.CurrentPage, model.TotalPages);

            int skip = (model.CurrentPage - 1) * pageSize;
            var pagedEntries = new List<LogEntryItem>(Math.Min(pageSize, Math.Max(0, filteredCount - skip)));
            int matchedIndex = 0;

            // Duyệt ngược từ cuối danh sách để dòng log mới nhất hiển thị lên đầu mà không cần .Reverse().ToList()
            for (int i = allEntries.Count - 1; i >= 0 && pagedEntries.Count < pageSize; i--)
            {
                var item = allEntries[i];
                if (!MatchesFilters(item)) continue;

                if (matchedIndex >= skip)
                {
                    pagedEntries.Add(item);
                }
                matchedIndex++;
            }

            model.Entries = pagedEntries;

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

        /// <summary>
        /// 4. POST: /Admin/Logs/TriggerDriveLogUpload (Kích hoạt thủ công upload log cũ lên Google Drive)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Admin/Logs/TriggerDriveLogUpload")]
        public async Task<IActionResult> TriggerDriveLogUpload(CancellationToken ct = default)
        {
            if (_driveLogUploadService == null)
            {
                return StatusCode(500, new { success = false, message = "Dịch vụ upload log lên Google Drive chưa được đăng ký." });
            }

            try
            {
                var triggerSource = $"MANUAL:{User.Identity?.Name ?? "Admin"}";
                var result = await _driveLogUploadService.RunUploadCycleAsync(triggerSource, ct);

                _logger.LogInformation(
                    "AUDIT DRIVE_LOG_UPLOAD_MANUAL: User={User}, Scanned={Scanned}, Uploaded={Uploaded}, DeletedLocal={Deleted}, Failed={Failed}, SkippedActive={Skipped}",
                    User.Identity?.Name,
                    result.ScannedFiles,
                    result.UploadedCount,
                    result.DeletedLocalCount,
                    result.FailedCount,
                    result.SkippedActiveCount);

                bool isSuccess = result.FailedCount == 0;
                string message = isSuccess
                    ? $"Hoàn tất kiểm tra {result.ScannedFiles} tệp log cũ: đã upload {result.UploadedCount}, xóa cục bộ {result.DeletedLocalCount}, bỏ qua {result.SkippedActiveCount} tệp đang hoạt động."
                    : $"Đã xử lý {result.ScannedFiles} tệp log: thành công {result.UploadedCount}, thất bại {result.FailedCount}. Vui lòng kiểm tra chi tiết bên dưới.";

                return Json(new { success = isSuccess, message, data = result });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return StatusCode(499, new { success = false, message = "Yêu cầu đã bị hủy." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy thủ công upload log lên Google Drive bởi {User}.", User.Identity?.Name);
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống khi upload log: {ex.Message}" });
            }
        }

        /// <summary>
        /// 5. POST: /Admin/Logs/RetryFallbackSync (Đồng bộ lại toàn bộ các tệp LOCAL_FALLBACK lên Google Drive)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Admin/Logs/RetryFallbackSync")]
        public async Task<IActionResult> RetryFallbackSync(CancellationToken ct = default)
        {
            if (_driveStorageService == null)
            {
                return StatusCode(500, new { success = false, message = "Dịch vụ Google Drive Storage chưa được đăng ký." });
            }

            try
            {
                int syncedCount = await _driveStorageService.RetryAllPendingFallbackFilesAsync(ct);
                var health = await _driveStorageService.GetHealthStatusAsync(ct);

                _logger.LogInformation(
                    "AUDIT DRIVE_RETRY_FALLBACK_SYNC: User={User}, SyncedCount={SyncedCount}, RemainingPending={Pending}",
                    User.Identity?.Name,
                    syncedCount,
                    health.LocalFallbackPendingCount);

                return Json(new
                {
                    success = true,
                    message = $"Đã đồng bộ lại {syncedCount} tệp fallback lên Google Drive. Còn chờ: {health.LocalFallbackPendingCount} tệp.",
                    data = new { syncedCount, health }
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return StatusCode(499, new { success = false, message = "Yêu cầu đã bị hủy." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ lại các tệp LOCAL_FALLBACK bởi {User}.", User.Identity?.Name);
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống khi đồng bộ file fallback: {ex.Message}" });
            }
        }

        /// <summary>
        /// 6. POST: /Admin/Logs/RunLocalMediaMigration (Kiểm tra / Di chuyển dữ liệu cục bộ cũ lên Google Drive)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Admin/Logs/RunLocalMediaMigration")]
        public async Task<IActionResult> RunLocalMediaMigration(
            [FromForm] bool dryRun = true,
            [FromForm] bool deleteLocalAfterVerified = false,
            CancellationToken ct = default)
        {
            if (_localMediaMigrationService == null)
            {
                return StatusCode(500, new { success = false, message = "Dịch vụ di chuyển dữ liệu cục bộ chưa được đăng ký." });
            }

            try
            {
                var report = await _localMediaMigrationService.RunMigrationAsync(
                    dryRun,
                    deleteLocalAfterVerified,
                    User.Identity?.Name,
                    ct);

                _logger.LogInformation(
                    "AUDIT LOCAL_MEDIA_MIGRATION: User={User}, DryRun={DryRun}, DeleteLocal={DeleteLocal}, Scanned={Scanned}, Migrated={Migrated}, FallbackRetried={Fallback}, Skipped={Skipped}, Failed={Failed}, FreedBytes={FreedBytes}",
                    User.Identity?.Name,
                    report.DryRun,
                    report.DeleteLocalAfterVerified,
                    report.ScannedFiles,
                    report.MigratedCount,
                    report.FallbackRetriedCount,
                    report.SkippedCount,
                    report.FailedCount,
                    report.FreedBytes);

                bool isSuccess = report.FailedCount == 0;
                string message = report.DryRun
                    ? $"[Dry-Run] Đã quét {report.ScannedFiles} mục cần kiểm tra (không thay đổi dữ liệu)."
                    : $"Hoàn tất di chuyển: thành công {report.MigratedCount}, đồng bộ fallback {report.FallbackRetriedCount}, bỏ qua {report.SkippedCount}, thất bại {report.FailedCount}, giải phóng {Math.Round(report.FreedBytes / 1024.0, 2)} KB.";

                return Json(new { success = isSuccess, message, data = report });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return StatusCode(499, new { success = false, message = "Yêu cầu đã bị hủy." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy di chuyển dữ liệu cũ lên Google Drive bởi {User}.", User.Identity?.Name);
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống khi di chuyển dữ liệu cũ: {ex.Message}" });
            }
        }

        private async Task PopulateDriveStorageSectionAsync(SystemLogsViewModel model, CancellationToken ct)
        {
            model.DriveLogEnabled = _driveLogOptions.Enabled;
            model.DriveLogCronSchedule = _driveLogOptions.IntervalMinutes > 0
                ? $"Mỗi {_driveLogOptions.IntervalMinutes} phút"
                : (string.IsNullOrWhiteSpace(_driveLogOptions.CronExpression)
                    ? "0 2 * * *"
                    : _driveLogOptions.CronExpression.Trim());

            try
            {
                if (_driveStorageService != null)
                {
                    model.CloudStorageHealth = await _driveStorageService.GetHealthStatusAsync(ct);
                }

                if (_driveLogUploadService != null)
                {
                    model.DriveLogUploads = await _driveLogUploadService.GetRecentLogUploadsAsync(30, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải thông tin trạng thái Google Drive trên trang Admin/Logs.");
            }
        }

        #region Helpers Phân tích Structured Log & Kiểm tra Sức khỏe Hạ tầng
        private async Task<List<LogEntryItem>> ParseLogFileEntriesAsync(string fullPath, CancellationToken ct = default)
        {
            var entries = new Queue<LogEntryItem>(Math.Min(256, MaxParsedLogEntries));
            int idx = 1;

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 65536,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            bool seekedTail = stream.Length > MaxLogTailBytes;
            if (seekedTail)
            {
                stream.Seek(-MaxLogTailBytes, SeekOrigin.End);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: !seekedTail);

            if (seekedTail)
            {
                // Bỏ qua dòng đầu tiên có thể bị cắt ngang khi seek trực tiếp vào giữa tệp UTF-8
                await reader.ReadLineAsync(ct);
            }

            string? line;
            LogEntryItem? currentEntry = null;
            StringBuilder? currentException = null;
            bool exceptionTruncated = false;

            void FinalizeCurrentEntry()
            {
                if (currentEntry == null) return;

                if (currentException != null && currentException.Length > 0)
                {
                    currentEntry.Exception = currentException.ToString();
                }

                currentException = null;
                exceptionTruncated = false;
                EnrichLogEntryMetadata(currentEntry);

                if (entries.Count >= MaxParsedLogEntries)
                {
                    entries.Dequeue();
                }
                entries.Enqueue(currentEntry);
            }

            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = LogHeaderRegex.Match(line);
                if (match.Success)
                {
                    FinalizeCurrentEntry();

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
                    currentException ??= new StringBuilder();
                    if (!exceptionTruncated)
                    {
                        if (currentException.Length + line.Length + 2 <= MaxExceptionChars)
                        {
                            if (currentException.Length > 0)
                            {
                                currentException.Append(Environment.NewLine);
                            }
                            currentException.Append(line);
                        }
                        else
                        {
                            if (currentException.Length > 0)
                            {
                                currentException.Append(Environment.NewLine);
                            }
                            currentException.Append("... [truncated]");
                            exceptionTruncated = true;
                        }
                    }
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

            FinalizeCurrentEntry();

            return entries.ToList();
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