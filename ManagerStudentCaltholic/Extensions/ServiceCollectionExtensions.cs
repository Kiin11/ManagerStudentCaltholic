using ManagerStudentCaltholic.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the ParishDbContext with the specified connection string from the configuration.
        /// PostGreSQL 
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ParishDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    // Tự động retry khi kết nối DB bị gián đoạn tạm thời
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                }));

            return services;
        }

        /// <summary>
        /// Config Nginx Reverse Proxy headers to get the correct client IP and protocol.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddReverseProxyConfiguration(this IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            return services;
        }

        /// <summary>
        /// Registry Bussiness Services for the application, such as IAttendanceService, IStudentService, etc.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Sẽ đăng ký các Service như IAttendanceService, IStudentService ở đây
            return services;
        }
    }
}
