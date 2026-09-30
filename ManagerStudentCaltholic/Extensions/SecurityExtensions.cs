using System.Threading.RateLimiting;

namespace ManagerStudentCaltholic.Extensions
{
    public static class SecurityExtensions
    {
        public const string PolicyGlobal = "PolicyGlobal";
        public const string PolicyAuth = "PolicyAuth";

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
            });

            return services;
        }
    }
}
