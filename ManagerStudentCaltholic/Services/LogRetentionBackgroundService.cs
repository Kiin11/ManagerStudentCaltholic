using ManagerStudentCaltholic.Interface.Services;

namespace ManagerStudentCaltholic.Services
{
    /// <summary>
    /// TASK-804 & TASK-P3-10.3: Dịch vụ dọn dẹp file Serilog quá 90 ngày và file Export tạm quá 24 giờ (Hỗ trợ Hangfire Recurring Job)
    /// </summary>
    public class LogRetentionBackgroundService : BackgroundService, ILogMaintenanceJobService
    {
        private readonly ILogger<LogRetentionBackgroundService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

        public LogRetentionBackgroundService(ILogger<LogRetentionBackgroundService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                using var timer = new PeriodicTimer(_checkInterval);

                do
                {
                    try
                    {
                        CleanUpOldLogsAndTempExports();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi xảy ra trong quá trình tự động dọn dẹp file log.");
                    }
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
            }
        }

        /// <summary>
        /// Hàm public được Hangfire Recurring Job kích hoạt định kỳ lúc 02:00 sáng Chúa Nhật hàng tuần
        /// </summary>
        public void CleanUpOldLogsAndTempExports()
        {
            // 1. Dọn dẹp file log cũ hơn 90 ngày (theo múi giờ Việt Nam UTC+7)
            var logsDirectory = Path.Combine(_env.ContentRootPath, "logs");
            if (Directory.Exists(logsDirectory))
            {
                var logCutoffDateVn = VietnamTimeHelper.Today.AddDays(-90);
                CleanDirectory(
                    logsDirectory,
                    "*.log",
                    info => VietnamTimeHelper.ToVietnamTime(DateTime.SpecifyKind(info.LastWriteTimeUtc, DateTimeKind.Utc)).Date < logCutoffDateVn,
                    "[Hangfire Maintenance] Đã xóa {Count} file log cũ hơn 90 ngày.");
            }

            // 2. Dọn dẹp các file báo cáo Excel tạm quá 24 giờ trong PrivateUploads/exports
            var exportsDirectory = Path.Combine(_env.ContentRootPath, "PrivateUploads", "exports");
            var exportCutoffUtc = DateTime.UtcNow.AddHours(-24);
            CleanDirectory(
                exportsDirectory,
                "*.xlsx",
                info => info.LastWriteTimeUtc < exportCutoffUtc,
                "[Hangfire Maintenance] Đã dọn dẹp {Count} file báo cáo Excel tạm quá hạn 24h.");

            // 3. Dọn dẹp các tệp tạm mồ côi quá 2 giờ trong PrivateUploads/tmp_logs và PrivateUploads/cloud_tmp
            var tempCutoffUtc = DateTime.UtcNow.AddHours(-2);
            CleanDirectory(
                Path.Combine(_env.ContentRootPath, "PrivateUploads", "tmp_logs"),
                "*",
                info => info.LastWriteTimeUtc < tempCutoffUtc,
                "[Hangfire Maintenance] Đã dọn dẹp {Count} file tạm quá 2h trong PrivateUploads/tmp_logs.");

            CleanDirectory(
                Path.Combine(_env.ContentRootPath, "PrivateUploads", "cloud_tmp"),
                "*",
                info => info.LastWriteTimeUtc < tempCutoffUtc,
                "[Hangfire Maintenance] Đã dọn dẹp {Count} file tạm quá 2h trong PrivateUploads/cloud_tmp.");
        }

        private void CleanDirectory(
            string directoryPath,
            string searchPattern,
            Func<FileInfo, bool> shouldDelete,
            string deletedSummaryLogTemplate)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directoryPath, searchPattern);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Không thể liệt kê thư mục dọn dẹp {Directory}: {Message}", directoryPath, ex.Message);
                return;
            }

            int deletedCount = 0;
            foreach (var filePath in files)
            {
                try
                {
                    var info = new FileInfo(filePath);
                    if (shouldDelete(info))
                    {
                        info.Delete();
                        deletedCount++;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Không thể xóa tệp {FilePath}: {Message}", filePath, ex.Message);
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation(deletedSummaryLogTemplate, deletedCount);
            }
        }
    }
}