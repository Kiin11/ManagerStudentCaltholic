using System.Globalization;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Options;
using Microsoft.Extensions.Options;

namespace ManagerStudentCaltholic.Services
{
    public class DriveLogUploadBackgroundService : BackgroundService
    {
        private const string DefaultCronExpression = "0 2 * * *";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DriveLogUploadOptions _options;
        private readonly ILogger<DriveLogUploadBackgroundService> _logger;

        public DriveLogUploadBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptions<DriveLogUploadOptions> options,
            ILogger<DriveLogUploadBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("DriveLogUploadBackgroundService đã tắt trong cấu hình (DriveLogUpload:Enabled = false).");
                return;
            }

            var vnTimeZone = GetVietnamTimeZone();
            var configuredCron = string.IsNullOrWhiteSpace(_options.CronExpression)
                ? DefaultCronExpression
                : _options.CronExpression.Trim();

            if (_options.IntervalMinutes <= 0 && !CronScheduleCalculator.TryParse(configuredCron, out _))
            {
                _logger.LogWarning(
                    "Biểu thức CronExpression '{Cron}' không hợp lệ, hệ thống tự động dùng lịch mặc định '{DefaultCron}'.",
                    configuredCron,
                    DefaultCronExpression);
                configuredCron = DefaultCronExpression;
            }

            _logger.LogInformation(
                "DriveLogUploadBackgroundService đã khởi động. Chế độ lịch: {Schedule}.",
                _options.IntervalMinutes > 0
                    ? $"Mỗi {_options.IntervalMinutes} phút"
                    : $"Cron '{configuredCron}' (Múi giờ Việt Nam UTC+7)");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var nowUtc = DateTime.UtcNow;
                    DateTime nextRunUtc = _options.IntervalMinutes > 0
                        ? nowUtc.AddMinutes(_options.IntervalMinutes)
                        : CronScheduleCalculator.GetNextOccurrenceUtc(configuredCron, nowUtc, vnTimeZone);

                    await DelayUntilUtcAsync(nextRunUtc, stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var uploadService = scope.ServiceProvider.GetRequiredService<IDriveLogUploadService>();
                    var runResult = await uploadService.RunUploadCycleAsync("SCHEDULED", stoppingToken);

                    _logger.LogInformation(
                        "Hoàn tất chu kỳ tự động upload log lên Google Drive: Scanned={Scanned}, Uploaded={Uploaded}, DeletedLocal={Deleted}, Failed={Failed}, SkippedActive={Skipped}.",
                        runResult.ScannedFiles,
                        runResult.UploadedCount,
                        runResult.DeletedLocalCount,
                        runResult.FailedCount,
                        runResult.SkippedActiveCount);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi không mong muốn trong DriveLogUploadBackgroundService; dịch vụ sẽ tiếp tục chu kỳ tiếp theo.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        private static async Task DelayUntilUtcAsync(DateTime targetUtc, CancellationToken ct)
        {
            var maxDelayChunk = TimeSpan.FromHours(12);
            while (!ct.IsCancellationRequested)
            {
                var remaining = targetUtc - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    return;
                }

                var chunk = remaining > maxDelayChunk ? maxDelayChunk : remaining;
                await Task.Delay(chunk, ct);
            }
        }

        internal static TimeZoneInfo GetVietnamTimeZone()
        {
            var preferredId = OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh";
            var fallbackId = OperatingSystem.IsWindows() ? "Asia/Ho_Chi_Minh" : "SE Asia Standard Time";

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(preferredId);
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(fallbackId);
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Vietnam Standard Time", "Vietnam Standard Time");
                }
            }
        }
    }

    internal static class CronScheduleCalculator
    {
        public static DateTime GetNextOccurrenceUtc(string cronExpression, DateTime afterUtc, TimeZoneInfo timeZone)
        {
            if (!TryParse(cronExpression, out var schedule))
            {
                TryParse("0 2 * * *", out schedule);
            }

            var utcBase = afterUtc.Kind == DateTimeKind.Utc
                ? afterUtc
                : DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);

            var localBase = TimeZoneInfo.ConvertTimeFromUtc(utcBase, timeZone);
            var candidate = new DateTime(
                localBase.Year,
                localBase.Month,
                localBase.Day,
                localBase.Hour,
                localBase.Minute,
                0,
                DateTimeKind.Unspecified).AddMinutes(1);

            var maxSearchDate = candidate.AddYears(8);
            while (candidate <= maxSearchDate)
            {
                if (!schedule.Months.Contains(candidate.Month))
                {
                    candidate = new DateTime(candidate.Year, candidate.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
                    continue;
                }

                if (!schedule.MatchesDay(candidate))
                {
                    candidate = candidate.Date.AddDays(1);
                    continue;
                }

                if (!schedule.Hours.Contains(candidate.Hour))
                {
                    candidate = new DateTime(candidate.Year, candidate.Month, candidate.Day, candidate.Hour, 0, 0, DateTimeKind.Unspecified).AddHours(1);
                    continue;
                }

                if (!schedule.Minutes.Contains(candidate.Minute))
                {
                    candidate = candidate.AddMinutes(1);
                    continue;
                }

                return ConvertLocalToUtcSafe(candidate, timeZone);
            }

            return utcBase.AddDays(1);
        }

        public static bool TryParse(string? expression, out CronSchedule schedule)
        {
            schedule = default;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            var parts = expression.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
            {
                return false;
            }

            if (!TryParseField(parts[0], 0, 59, normalizeSunday: false, out var minutes)
                || !TryParseField(parts[1], 0, 23, normalizeSunday: false, out var hours)
                || !TryParseField(parts[2], 1, 31, normalizeSunday: false, out var daysOfMonth)
                || !TryParseField(parts[3], 1, 12, normalizeSunday: false, out var months)
                || !TryParseField(parts[4], 0, 7, normalizeSunday: true, out var daysOfWeek))
            {
                return false;
            }

            schedule = new CronSchedule(
                minutes,
                hours,
                daysOfMonth,
                months,
                daysOfWeek,
                parts[2] == "*",
                parts[4] == "*");

            return true;
        }

        private static bool TryParseField(
            string field,
            int min,
            int max,
            bool normalizeSunday,
            out HashSet<int> values)
        {
            values = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(field))
            {
                return false;
            }

            var segments = field.Split(',', StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment))
                {
                    return false;
                }

                var slashParts = segment.Split('/');
                if (slashParts.Length > 2)
                {
                    return false;
                }

                int step = 1;
                if (slashParts.Length == 2)
                {
                    if (!int.TryParse(slashParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out step) || step <= 0)
                    {
                        return false;
                    }
                }

                string rangePart = slashParts[0];
                int rangeStart;
                int rangeEnd;

                if (rangePart == "*")
                {
                    rangeStart = min;
                    rangeEnd = max;
                }
                else if (rangePart.Contains('-', StringComparison.Ordinal))
                {
                    var bounds = rangePart.Split('-');
                    if (bounds.Length != 2
                        || !int.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out rangeStart)
                        || !int.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out rangeEnd))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!int.TryParse(rangePart, NumberStyles.None, CultureInfo.InvariantCulture, out rangeStart))
                    {
                        return false;
                    }

                    rangeEnd = slashParts.Length == 2 ? max : rangeStart;
                }

                if (rangeStart < min || rangeEnd > max || rangeStart > rangeEnd)
                {
                    return false;
                }

                for (int val = rangeStart; val <= rangeEnd; val += step)
                {
                    int normalized = normalizeSunday && val == 7 ? 0 : val;
                    values.Add(normalized);
                }
            }

            return values.Count > 0;
        }

        private static DateTime ConvertLocalToUtcSafe(DateTime localUnspecified, TimeZoneInfo timeZone)
        {
            if (timeZone.IsInvalidTime(localUnspecified))
            {
                localUnspecified = localUnspecified.AddHours(1);
            }

            return TimeZoneInfo.ConvertTimeToUtc(localUnspecified, timeZone);
        }

        internal readonly record struct CronSchedule(
            HashSet<int> Minutes,
            HashSet<int> Hours,
            HashSet<int> DaysOfMonth,
            HashSet<int> Months,
            HashSet<int> DaysOfWeek,
            bool IsDayOfMonthWildcard,
            bool IsDayOfWeekWildcard)
        {
            public bool MatchesDay(DateTime candidate)
            {
                if (IsDayOfMonthWildcard && IsDayOfWeekWildcard)
                {
                    return true;
                }

                if (!IsDayOfMonthWildcard && IsDayOfWeekWildcard)
                {
                    return DaysOfMonth.Contains(candidate.Day);
                }

                if (IsDayOfMonthWildcard && !IsDayOfWeekWildcard)
                {
                    return DaysOfWeek.Contains((int)candidate.DayOfWeek);
                }

                return DaysOfMonth.Contains(candidate.Day) || DaysOfWeek.Contains((int)candidate.DayOfWeek);
            }
        }
    }
}
