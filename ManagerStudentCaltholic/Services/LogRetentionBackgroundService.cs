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

        /// <summary>
        /// Hàm public được Hangfire Recurring Job kích hoạt định kỳ lúc 02:00 sáng Chúa Nhật hàng tuần
        /// </summary>
        public void CleanUpOldLogsAndTempExports()
        {
            // 1. Dọn dẹp file log cũ hơn 90 ngày
            var logsDirectory = Path.Combine(_env.ContentRootPath, "logs");
            if (Directory.Exists(logsDirectory))
            {
                var logCutoffDate = DateTime.UtcNow.Date.AddDays(-90);
                var logFiles = Directory.GetFiles(logsDirectory, "*.log");
                int deletedLogs = 0;

                foreach (var filePath in logFiles)
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.LastWriteTimeUtc.Date < logCutoffDate)
                    {
                        try
                        {
                            fileInfo.Delete();
                            deletedLogs++;
                        }
                        catch (IOException ioEx)
                        {
                            _logger.LogWarning("Không thể xóa file log {FileName}: {Message}", fileInfo.Name, ioEx.Message);
                        }
                    }
                }

                if (deletedLogs > 0)
                {
                    _logger.LogInformation("[Hangfire Maintenance] Đã xóa {Count} file log cũ hơn 90 ngày.", deletedLogs);
                }
            }

            // 2. Dọn dẹp các file báo cáo Excel tạm quá 24 giờ trong PrivateUploads/exports
            var exportsDirectory = Path.Combine(_env.ContentRootPath, "PrivateUploads", "exports");
            if (Directory.Exists(exportsDirectory))
            {
                var exportCutoff = DateTime.UtcNow.AddHours(-24);
                var exportFiles = Directory.GetFiles(exportsDirectory, "*.xlsx");
                int deletedExports = 0;

                foreach (var filePath in exportFiles)
                {
                    var info = new FileInfo(filePath);
                    if (info.LastWriteTimeUtc < exportCutoff)
                    {
                        try
                        {
                            info.Delete();
                            deletedExports++;
                        }
                        catch { }
                    }
                }

                if (deletedExports > 0)
                {
                    _logger.LogInformation("[Hangfire Maintenance] Đã dọn dẹp {Count} file báo cáo Excel tạm quá hạn 24h.", deletedExports);
                }
            }
        }
    }
}