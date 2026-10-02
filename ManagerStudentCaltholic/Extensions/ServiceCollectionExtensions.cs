using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Security;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
            services.AddScoped<IStudentCodeGenerator, StudentCodeGenerator>();
            services.AddScoped<IStudentExcelService, StudentExcelService>();
            services.AddScoped<IQrCodeService, QrCodeService>();
            services.AddScoped<IPasswordHasherService, PasswordHasherService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddHostedService<LogRetentionBackgroundService>();
            services.AddSingleton<IAuthorizationHandler, GradeScopeHandler>();
            services.AddScoped<ILessonPlanImportService, LessonPlanImportService>();

            return services;
        }


        /// <summary>
        /// CẤU HÌNH HYBRID AUTHENTICATION
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddHybridAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "ParishSecretKeyForAuthenticationToken2026!MustBeVeryLongAndSecureString";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            // Đăng ký cả Cookie (cho Razor) và JWT (cho API/Fetch)
            services.AddAuthentication(options =>
            {
                // Mặc định cho Web MVC là Cookie
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = "ParishSessionCookie";
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;             // Chống XSS đọc trộm Cookie
                options.Cookie.SameSite = SameSiteMode.Lax; // Cho phép chuyển hướng an toàn
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false; // Phù hợp chạy trong Docker/Proxy nội bộ
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Cấu hình các Authorization Policies
            services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdminOnly", policy =>
                    policy.RequireRole(UserRole.Admin));

                options.AddPolicy("RequireSpiritualDirector", policy =>
                    policy.RequireRole(UserRole.SpiritualDirector));

                options.AddPolicy("RequireExecutiveBoard", policy =>
                    policy.RequireRole(UserRole.ExecutiveBoard, UserRole.Admin));

                options.AddPolicy("RequireLeadership", policy =>
                    policy.RequireRole(UserRole.Admin, UserRole.SpiritualDirector, UserRole.ExecutiveBoard));

                options.AddPolicy("RequireStaff", policy =>
                    policy.RequireRole(UserRole.Admin, UserRole.SpiritualDirector, UserRole.ExecutiveBoard, UserRole.BranchHead, UserRole.Teacher));
            });

            return services;
        }

        /// <summary>
        /// Thêm cấu hình chống CSRF toàn cục và Cookie nghiêm ngặt
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddStrictSecurityConfigurations(this IServiceCollection services)
        {
            // TASK-708: Tự động kiểm tra Anti-CSRF Token cho tất cả POST, PUT, DELETE
            services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });

            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "Parish-Antiforgery";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.HeaderName = "X-CSRF-TOKEN"; // Hỗ trợ Fetch / Ajax gửi qua Header
            });

            return services;
        }
    }
}
