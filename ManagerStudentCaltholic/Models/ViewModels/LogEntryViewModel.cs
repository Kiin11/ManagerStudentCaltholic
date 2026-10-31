namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class LogEntryItem
    {
        public int Index { get; set; }
        public DateTime? Timestamp { get; set; }
        public string TimestampDisplay => Timestamp.HasValue ? Timestamp.Value.ToString("dd/MM/yyyy HH:mm:ss.fff") : "--";
        public string Level { get; set; } = "INF"; // INF, WRN, ERR, FTL, DBG
        public string Module { get; set; } = "HỆ THỐNG";
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public string RawLine { get; set; } = string.Empty;

        // Các thuộc tính bóc tách tự động từ Structured Log
        public string? PerformedBy { get; set; }
        public string? IpAddress { get; set; }
        public string? HttpMethod { get; set; }
        public string? RequestPath { get; set; }
        public int? StatusCode { get; set; }
        public long? ElapsedMs { get; set; }
        public bool HasException => !string.IsNullOrWhiteSpace(Exception);
    }

    public class SystemHealthSummaryDto
    {
        // 1. Trạng thái PostgreSQL
        public bool IsDatabaseUp { get; set; }
        public long DatabaseLatencyMs { get; set; }
        public string DatabaseStatusText => IsDatabaseUp ? $"Hoạt động ({DatabaseLatencyMs} ms)" : "Mất kết nối";

        // 2. Trạng thái Redis Distributed Cache
        public bool IsRedisUp { get; set; }
        public long RedisLatencyMs { get; set; }
        public string RedisStatusText => IsRedisUp ? $"Hoạt động ({RedisLatencyMs} ms)" : "Fallback MemoryCache";

        // 3. Tài nguyên Máy chủ & Tiến trình
        public double MemoryUsageMb { get; set; }
        public string UptimeDisplay { get; set; } = string.Empty;
        public double RequestsPerMinute { get; set; }
        public double AverageResponseMs { get; set; }
        public long TotalRequestsSinceStart { get; set; }

        // 4. Dung lượng thư mục Logs
        public int TotalLogFilesCount { get; set; }
        public double TotalLogsSizeMb { get; set; }
    }

    public class HourlyLogStatPointDto
    {
        public int Hour { get; set; }
        public string Label => $"{Hour:D2}:00";
        public int InfoCount { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
    }

    public class SystemLogsViewModel
    {
        // Bộ lọc tìm kiếm
        public List<string> AvailableLogFiles { get; set; } = new();
        public string SelectedFile { get; set; } = string.Empty;
        public string LevelFilter { get; set; } = "ALL";
        public string ModuleFilter { get; set; } = "ALL";
        public string? SearchKeyword { get; set; }
        public TimeSpan? FromTime { get; set; }
        public TimeSpan? ToTime { get; set; }

        // Phân trang
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalPages { get; set; } = 1;
        public int TotalLines { get; set; }
        public int FilteredCount { get; set; }

        // Thống kê nhanh trong tệp Log đang chọn
        public int InfoCount { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public int FatalCount { get; set; }
        public int AuditCount { get; set; }

        // Sức khỏe hệ thống & Biểu đồ 24h
        public SystemHealthSummaryDto Health { get; set; } = new();
        public List<HourlyLogStatPointDto> HourlyStats { get; set; } = new();
        public Dictionary<string, int> ModuleBreakdown { get; set; } = new();

        // Danh sách dòng log trên trang hiện tại
        public List<LogEntryItem> Entries { get; set; } = new();
    }
}