using System.Diagnostics;

namespace ManagerStudentCaltholic.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var request = context.Request;
            var path = request.Path.Value;

            // Bỏ qua log cho các static files (css, js, images) để tránh làm rác file log
            if (path != null && (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") || path.EndsWith(".ico")))
            {
                await _next(context);
                return;
            }

            var method = request.Method;
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            try
            {
                await _next(context);
                stopwatch.Stop();

                var statusCode = context.Response.StatusCode;
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                // Phân loại mức độ log dựa trên Status Code
                if (statusCode >= 500)
                {
                    _logger.LogError("HTTP {Method} {Path} phản hồi {StatusCode} từ IP {ClientIp} sau {ElapsedMs} ms",
                        method, path, statusCode, clientIp, elapsedMs);
                }
                else if (statusCode >= 400)
                {
                    _logger.LogWarning("HTTP {Method} {Path} phản hồi {StatusCode} từ IP {ClientIp} sau {ElapsedMs} ms",
                        method, path, statusCode, clientIp, elapsedMs);
                }
                else
                {
                    _logger.LogInformation("HTTP {Method} {Path} phản hồi {StatusCode} trong {ElapsedMs} ms",
                        method, path, statusCode, elapsedMs);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HTTP {Method} {Path} phát sinh ngoại lệ không xử lý sau {ElapsedMs} ms",
                    method, path, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
