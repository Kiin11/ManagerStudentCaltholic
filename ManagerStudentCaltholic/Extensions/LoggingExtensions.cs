using System.Diagnostics;
using System.Security.Claims;
using ManagerStudentCaltholic.Services;
using Serilog;
using Serilog.Events;

namespace ManagerStudentCaltholic.Extensions
{
    public static class LoggingExtensions
    {
        private const string StandardOutputTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        public static IHostBuilder AddSerilogLogging(this IHostBuilder hostBuilder)
        {
            return hostBuilder.UseSerilog((context, services, loggerConfig) =>
            {
                var logsDir = Path.Combine(context.HostingEnvironment.ContentRootPath, "logs");
                Directory.CreateDirectory(logsDir);

                loggerConfig
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(outputTemplate: StandardOutputTemplate)
                    // 1. Tệp log tổng hợp toàn hệ thống theo ngày
                    .WriteTo.File(
                        path: Path.Combine(logsDir, "parish-log-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 90,
                        shared: true,
                        outputTemplate: StandardOutputTemplate)
                    // 2. Tệp log tách riêng chỉ chứa Error & Fatal để Admin tra cứu sự cố tức thì (TASK-P3-11.1)
                    .WriteTo.Logger(lc => lc
                        .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                        .WriteTo.File(
                            path: Path.Combine(logsDir, "parish-errors-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 90,
                            shared: true,
                            outputTemplate: StandardOutputTemplate));
            });
        }

        public static IApplicationBuilder UseCustomRequestLogging(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? "/";

                // Bỏ qua ghi log cho các tệp tĩnh để tránh làm rác file log
                if (path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/image/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/icons/", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    await next();
                    return;
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    await next();
                    sw.Stop();

                    var statusCode = context.Response.StatusCode;
                    var metrics = context.RequestServices.GetService<SystemMetricsCollector>();
                    metrics?.RecordHttpRequest(statusCode, sw.ElapsedMilliseconds);

                    var user = context.User?.Identity?.IsAuthenticated == true
                        ? context.User.Identity.Name
                        : "Anonymous";
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var method = context.Request.Method;

                    if (statusCode >= 500)
                    {
                        Log.Error("HTTP {Method} {Path} responded {StatusCode} in {Elapsed} ms | User={User} | IP={IP}",
                            method, path, statusCode, sw.ElapsedMilliseconds, user, ip);
                    }
                    else if (statusCode >= 400 && statusCode != 404)
                    {
                        Log.Warning("HTTP {Method} {Path} responded {StatusCode} in {Elapsed} ms | User={User} | IP={IP}",
                            method, path, statusCode, sw.ElapsedMilliseconds, user, ip);
                    }
                    else
                    {
                        Log.Information("HTTP {Method} {Path} responded {StatusCode} in {Elapsed} ms | User={User} | IP={IP}",
                            method, path, statusCode, sw.ElapsedMilliseconds, user, ip);
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    var metrics = context.RequestServices.GetService<SystemMetricsCollector>();
                    metrics?.RecordHttpRequest(500, sw.ElapsedMilliseconds);

                    var user = context.User?.Identity?.Name ?? "Anonymous";
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    Log.Error(ex, "HTTP {Method} {Path} threw unhandled exception after {Elapsed} ms | User={User} | IP={IP}",
                        context.Request.Method, path, sw.ElapsedMilliseconds, user, ip);
                    throw;
                }
            });
        }
    }
}