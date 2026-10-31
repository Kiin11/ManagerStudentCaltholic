using System.Collections.Concurrent;
using System.Diagnostics;

namespace ManagerStudentCaltholic.Services
{
    public class HourlyTrafficMetric
    {
        public int Hour { get; set; }
        public long TotalRequests;
        public long WarningCount;
        public long ErrorCount;
        public long TotalElapsedMs;
    }

    public class SystemMetricsCollector
    {
        private static readonly DateTime ProcessStartTimeUtc = DateTime.UtcNow;
        private long _totalRequestsCount;
        private long _totalErrorResponsesCount;
        private long _totalElapsedMs;

        // Lưu trữ thống kê theo 24 giờ của ngày hiện tại (Key: "yyyyMMdd_HH")
        private readonly ConcurrentDictionary<string, HourlyTrafficMetric> _hourlyMetrics = new();

        // Lưu trữ số lượng request theo từng phút gần nhất để tính Req/phút (Key: "yyyyMMddHHmm")
        private readonly ConcurrentDictionary<string, long> _minuteBuckets = new();

        public DateTime StartTimeUtc => ProcessStartTimeUtc;
        public TimeSpan Uptime => DateTime.UtcNow - ProcessStartTimeUtc;
        public long TotalRequests => Interlocked.Read(ref _totalRequestsCount);
        public long TotalErrorResponses => Interlocked.Read(ref _totalErrorResponsesCount);

        public double AverageResponseTimeMs
        {
            get
            {
                var reqs = Interlocked.Read(ref _totalRequestsCount);
                if (reqs == 0) return 0;
                return Math.Round(Interlocked.Read(ref _totalElapsedMs) / (double)reqs, 1);
            }
        }

        public void RecordHttpRequest(int statusCode, long elapsedMs)
        {
            var nowLocal = DateTime.Now;
            Interlocked.Increment(ref _totalRequestsCount);
            Interlocked.Add(ref _totalElapsedMs, elapsedMs);

            if (statusCode >= 500)
            {
                Interlocked.Increment(ref _totalErrorResponsesCount);
            }

            // Cập nhật giỏ theo phút
            string minuteKey = nowLocal.ToString("yyyyMMddHHmm");
            _minuteBuckets.AddOrUpdate(minuteKey, 1, (_, val) => val + 1);

            // Dọn các giỏ phút cũ hơn 15 phút
            if (_minuteBuckets.Count > 20)
            {
                var cutoffKey = nowLocal.AddMinutes(-15).ToString("yyyyMMddHHmm");
                foreach (var key in _minuteBuckets.Keys)
                {
                    if (string.CompareOrdinal(key, cutoffKey) < 0)
                    {
                        _minuteBuckets.TryRemove(key, out _);
                    }
                }
            }

            // Cập nhật giỏ theo giờ trong ngày
            string hourKey = $"{nowLocal:yyyyMMdd}_{nowLocal.Hour:D2}";
            var bucket = _hourlyMetrics.GetOrAdd(hourKey, _ => new HourlyTrafficMetric { Hour = nowLocal.Hour });
            Interlocked.Increment(ref bucket.TotalRequests);
            Interlocked.Add(ref bucket.TotalElapsedMs, elapsedMs);

            if (statusCode >= 500)
            {
                Interlocked.Increment(ref bucket.ErrorCount);
            }
            else if (statusCode >= 400)
            {
                Interlocked.Increment(ref bucket.WarningCount);
            }
        }

        /// <summary>
        /// Lấy tốc độ Request trung bình mỗi phút trong 5 phút gần nhất
        /// </summary>
        public double GetCurrentRequestsPerMinute()
        {
            var nowLocal = DateTime.Now;
            long sumLast5Mins = 0;
            for (int i = 0; i < 5; i++)
            {
                string key = nowLocal.AddMinutes(-i).ToString("yyyyMMddHHmm");
                if (_minuteBuckets.TryGetValue(key, out long count))
                {
                    sumLast5Mins += count;
                }
            }
            return Math.Round(sumLast5Mins / 5.0, 1);
        }

        /// <summary>
        /// Lấy số lượng RAM tiến trình .NET đang sử dụng (MB)
        /// </summary>
        public double GetProcessMemoryMb()
        {
            using var proc = Process.GetCurrentProcess();
            return Math.Round(proc.WorkingSet64 / (1024.0 * 1024.0), 1);
        }
    }
}