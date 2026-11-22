using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace ManagerStudentCaltholic.Extensions
{
    public static class SecurityExtensions
    {
        public const string PolicyGlobal = "PolicyGlobal";
        public const string PolicyAuth = "PolicyAuth";
        public const string PolicyPublicLookup = "PolicyPublicLookup"; // Thêm hằng số này
        public const string PolicyQrAttendance = "PolicyQrAttendance"; // TASK-INF-08: High-concurrency QR burst behind 1 shared NAT IP

        /// <summary>
        /// TASK-705: Đăng ký dịch vụ Rate Limiting
        /// </summary>
        public static IServiceCollection AddAppRateLimiter(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                // Khi vượt quá giới hạn, trả về mã HTTP 429
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Xử lý nội dung phản hồi khi bị chặn
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                    await context.HttpContext.Response.WriteAsync(
                        "{\"success\":false,\"message\":\"Bạn đang gửi yêu cầu quá nhanh. Vui lòng thử lại sau giây lát (HTTP 429).\"}",
                        cancellationToken: token);
                };

                // 1. Policy Global (Sliding Window): 60 request / phút cho mỗi Client IP
                options.AddPolicy(PolicyGlobal, httpContext =>
                {
                    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                    return RateLimitPartition.GetSlidingWindowLimiter(clientIp, _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0
                    });
                });

                // 2. Policy nghiêm ngặt cho Đăng nhập (Fixed Window): 5 lần / 15 giây
                options.AddPolicy(PolicyAuth, httpContext =>
                {
                    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                    return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromSeconds(15),
                        QueueLimit = 0
                    });
                });

                // 3. Policy cho Tra cứu công khai / Nộp đơn xin nghỉ (Chống spam SĐT / Brute-force): 10 lần / phút
                options.AddPolicy(PolicyPublicLookup, httpContext =>
                {
                    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                    return RateLimitPartition.GetSlidingWindowLimiter(clientIp, _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 2,
                        QueueLimit = 0
                    });
                });

                // 4. TASK-INF-08 / TASK-INF-07 alignment: Policy dành riêng cho luồng Điểm danh QR & Đồng bộ Offline
                // Cho phép 20-30 thiết bị GLV quét QR đồng thời qua chung 1 IP NAT Wi-Fi nhà thờ (60 req/s bền vững, burst 180 req, hàng đợi 60)
                // mà KHÔNG bao giờ bị HTTP 429, đồng thời vẫn giữ nguyên PolicyPublicLookup & PolicyAuth nghiêm ngặt.
                options.AddPolicy(PolicyQrAttendance, httpContext =>
                {
                    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                    return RateLimitPartition.GetTokenBucketLimiter(clientIp, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 180,
                        TokensPerPeriod = 60,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 60,
                        AutoReplenishment = true
                    });
                });
            });

            return services;
        }
    }
}