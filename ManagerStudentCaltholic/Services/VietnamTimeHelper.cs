namespace ManagerStudentCaltholic.Services
{
    /// <summary>
    /// Chuẩn hóa múi giờ Việt Nam (UTC+7 / Asia/Ho_Chi_Minh) xuyên suốt toàn hệ thống.
    /// Đảm bảo chốt ngày nghiệp vụ (SessionDate, AbsenceDate, Promotion...) không bị lệch khi host chạy UTC.
    /// </summary>
    public static class VietnamTimeHelper
    {
        private static readonly TimeZoneInfo _vietnamTimeZone = ResolveVietnamTimeZone();

        public static TimeZoneInfo VietnamTimeZone => _vietnamTimeZone;

        /// <summary>
        /// Thời điểm hiện tại theo giờ Việt Nam (UTC+7).
        /// </summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vietnamTimeZone);

        /// <summary>
        /// Ngày hiện tại theo giờ Việt Nam (Kind=Unspecified, phần Date lúc 00:00:00).
        /// </summary>
        public static DateTime Today => Now.Date;

        /// <summary>
        /// Ngày hiện tại theo giờ Việt Nam với DateTimeKind.Utc ở 00:00:00 để lưu/truy vấn Npgsql đồng nhất.
        /// </summary>
        public static DateTime TodayUtcKind => DateTime.SpecifyKind(Today, DateTimeKind.Utc);

        /// <summary>
        /// Quy đổi một DateTime bất kỳ về giờ Việt Nam (UTC+7).
        /// Nếu Kind == Unspecified, coi như đã là giờ Việt Nam.
        /// </summary>
        public static DateTime ToVietnamTime(DateTime dt)
        {
            return dt.Kind switch
            {
                DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(dt, _vietnamTimeZone),
                DateTimeKind.Local => TimeZoneInfo.ConvertTime(dt, _vietnamTimeZone),
                _ => dt
            };
        }

        /// <summary>
        /// Chuẩn hóa một ngày nghiệp vụ (chỉ lấy phần Date ở 00:00:00) với DateTimeKind.Utc.
        /// </summary>
        public static DateTime NormalizeBusinessDateUtcKind(DateTime date)
        {
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        }

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.CreateCustomTimeZone(
                        "Asia/Ho_Chi_Minh",
                        TimeSpan.FromHours(7),
                        "Vietnam Standard Time",
                        "Vietnam Standard Time");
                }
            }
        }
    }
}
