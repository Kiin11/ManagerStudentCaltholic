namespace ManagerStudentCaltholic.Services
{
    /// <summary>
    /// TASK-804: Dịch vụ chạy ngầm tự động quét và xóa các file Serilog cũ hơn 90 ngày
    /// </summary>
    public class LogRetentionBackgroundService : BackgroundService
    {
        private readonly ILogger<LogRetentionBackgroundService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Chạy định kỳ mỗi 24 giờ

        public LogRetentionBackgroundService(ILogger<LogRetentionBackgroundService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LogRetentionBackgroundService đã khởi chạy. Định kỳ kiểm tra: {Hours} giờ.", _checkInterval.TotalHours);

            // Chờ 1 phút sau khi khởi động app rồi mới chạy quét lần đầu
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            using var timer = new PeriodicTimer(_checkInterval);

            do
            {
                try
                {
                    CleanUpOldLogs();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình tự động dọn dẹp file log.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private void CleanUpOldLogs()
        {
            var logsDirectory = Path.Combine(_env.ContentRootPath, "logs");
            if (!Directory.Exists(logsDirectory))
            {
                _logger.LogInformation("Thư mục logs chưa tồn tại, bỏ qua đợt dọn dẹp.");
                return;
            }

            var cutoffDate = DateTime.UtcNow.Date.AddDays(-90);
            var logFiles = Directory.GetFiles(logsDirectory, "*.log");
            int deletedCount = 0;

            foreach (var filePath in logFiles)
            {
                var fileInfo = new FileInfo(filePath);

                // Kiểm tra tuổi file theo LastWriteTime hoặc bóc tách ngày từ tên file
                if (fileInfo.LastWriteTimeUtc.Date < cutoffDate)
                {
                    try
                    {
                        fileInfo.Delete();
                        deletedCount++;
                        _logger.LogInformation("Đã xóa file log cũ: {FileName} (Cập nhật lần cuối: {LastWrite})",
                            fileInfo.Name, fileInfo.LastWriteTimeUtc);
                    }
                    catch (IOException ioEx)
                    {
                        _logger.LogWarning("Không thể xóa file log {FileName} vì đang bị khóa bởi tiến trình khác: {Message}",
                            fileInfo.Name, ioEx.Message);
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Hoàn tất dọn dẹp log: Đã xóa {Count} file log cũ hơn 90 ngày (trước ngày {CutoffDate:dd/MM/yyyy}).",
                    deletedCount, cutoffDate);
            }
        }
    }
}
