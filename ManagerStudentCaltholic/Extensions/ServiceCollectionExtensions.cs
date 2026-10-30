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
using System.Net;
using System.Text;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

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
        /// SEC-NEW-07 FIX: Chống giả mạo X-Forwarded-For bằng cách giới hạn ForwardLimit = 1
        /// và chỉ tin cậy các dải IP Reverse Proxy nội bộ (Loopback & Private Docker Networks)
        /// </summary>
        public static IServiceCollection AddReverseProxyConfiguration(this IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1; // Chỉ đọc đúng 1 hop gần nhất do Nginx gắn vào
                options.RequireHeaderSymmetry = false;

                options.KnownProxies.Clear();
                options.KnownNetworks.Clear();

                // Chỉ tin tưởng Proxy nằm trong mạng Loopback hoặc mạng Private (Docker bridge / nội bộ)
                options.KnownProxies.Add(IPAddress.Loopback);
                options.KnownProxies.Add(IPAddress.IPv6Loopback);
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
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
            services.AddScoped<IGradeCalculationService, GradeCalculationService>();
            services.AddScoped<IGradePermissionService, GradePermissionService>();
            services.AddScoped<IStudentPlacementService, StudentPlacementService>();
            services.AddScoped<IPromotionEvaluationService, PromotionEvaluationService>();
            // FEAT-P3-03 (TASK-P3-03.2): Đăng ký Service quản lý Sổ bộ Bí tích điện tử
            services.AddScoped<ISacramentRegistryService, SacramentRegistryService>();
            services.AddScoped<ISacramentEvaluationService, SacramentEvaluationService>();

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
            var secretKey = jwtSettings["SecretKey"];

            // SEC-01 FIX: Không dùng khóa fallback cứng, kiểm tra độ dài tối thiểu 32 ký tự (256-bit)
            if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
            {
                throw new InvalidOperationException(
                    "Lỗi cấu hình bảo mật: 'JwtSettings:SecretKey' chưa được thiết lập trong biến môi trường hoặc User Secrets, hoặc có độ dài nhỏ hơn 32 ký tự.");
            }

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

                // SEC-06 FIX: Kiểm tra trạng thái hoạt động và vai trò thực tế của User trong DB
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = async context =>
                    {
                        var userIdStr = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        if (!long.TryParse(userIdStr, out var userId))
                        {
                            context.RejectPrincipal();
                            await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(
                                context.HttpContext, CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<ParishDbContext>();
                        var user = await dbContext.Users
                            .AsNoTracking()
                            .Select(u => new { u.Id, u.IsActive, u.Role, u.ManagedGradeLevel, u.LockoutEnd })
                            .FirstOrDefaultAsync(u => u.Id == userId);

                        // 1. Nếu tài khoản không tồn tại, bị khóa (IsActive = false) hoặc đang bị Lockout -> Hủy phiên ngay lập tức
                        if (user == null || !user.IsActive || (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow))
                        {
                            context.RejectPrincipal();
                            await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(
                                context.HttpContext, CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        // 2. Nếu Vai trò (Role) hoặc Khối phụ trách (ManagedGradeLevel) trong Cookie lệch với DB -> Bắt đăng nhập lại hoặc cập nhật Claims
                        var currentCookieRole = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                        var currentCookieGrade = context.Principal?.FindFirst("ManagedGradeLevel")?.Value ?? string.Empty;
                        var dbGrade = user.ManagedGradeLevel ?? string.Empty;

                        if (currentCookieRole != user.Role || currentCookieGrade != dbGrade)
                        {
                            context.RejectPrincipal();
                            await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(
                                context.HttpContext, CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    }
                };
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
