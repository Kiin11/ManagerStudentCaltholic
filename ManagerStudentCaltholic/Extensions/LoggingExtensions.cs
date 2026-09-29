using ManagerStudentCaltholic.Middlewares;
using Serilog;

namespace ManagerStudentCaltholic.Extensions
{
    public static class LoggingExtensions
    {
        /// <summary>
        /// Cấu hình Serilog đọc config, ghi Console và Rolling File hàng ngày
        /// </summary>
        public static ConfigureHostBuilder AddSerilogLogging(this ConfigureHostBuilder host)
        {
            host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: "logs/parish_app-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30, // Giữ lại tối đa 30 ngày log
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
            });

            return host;
        }

        /// <summary>
        /// Kích hoạt Middleware RequestLogging tùy chỉnh vào Pipeline
        /// </summary>
        public static IApplicationBuilder UseCustomRequestLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
