using Hangfire;
using Hangfire.PostgreSql;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Middlewares;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Security;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;

// 1. Init Bootstrap Logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// TASK-706: Cconfig limit payload và timeout in Kestrel layer
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 20 * 1024 * 1024; // 20 MB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

// 2. Add Serilog Logging
builder.Host.AddSerilogLogging();

// 3. Registry Database, Reverse Proxy, Security & Rate Limiter
builder.Services
    .AddDatabaseConfiguration(builder.Configuration)
    .AddReverseProxyConfiguration()
    .AddApplicationServices()
    .AddHybridAuthentication(builder.Configuration)
    .AddStrictSecurityConfigurations() // task -707: Add Security Headers Middleware
    .AddAppRateLimiter()  // task-705: Add Rate Limiting
    .AddMemoryCache();  // Make sure IMemoryCache active for LookupController

//builder.Services.AddAuthorization(options =>
//{
//    options.AddPolicy("RequireAdminOnly", policy =>
//        policy.RequireRole(UserRole.Admin));

//    options.AddPolicy("RequireSpiritualDirector", policy =>
//        policy.RequireRole(UserRole.SpiritualDirector));

//    options.AddPolicy("RequireExecutiveBoard", policy =>
//        policy.RequireRole(UserRole.ExecutiveBoard, UserRole.Admin));

//    options.AddPolicy("RequireLeadership", policy =>
//        policy.RequireRole(UserRole.Admin, UserRole.SpiritualDirector, UserRole.ExecutiveBoard));

//    options.AddPolicy("RequireStaff", policy =>
//        policy.RequireRole(UserRole.Admin, UserRole.SpiritualDirector, UserRole.ExecutiveBoard, UserRole.BranchHead, UserRole.Teacher));
//});
//builder.Services.AddControllersWithViews();

// 4. FEAT-P3-09: Config Redis Cache Distribution (Auto Fallback )
var redisConnStr = builder.Configuration.GetConnectionString("RedisConnection");
var instancePrefix = builder.Configuration["CacheSettings:InstanceName"] ?? "ParishQLGL:";

if (!string.IsNullOrWhiteSpace(redisConnStr))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnStr;
        options.InstanceName = instancePrefix;
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// 5. FEAT-P3-10: Config Hangfire using PostgreSQL Storage
var defaultDbConn = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(defaultDbConn),
        new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(5)
        }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Math.Max(2, Environment.ProcessorCount);
    options.Queues = new[] { "default", "exports", "maintenance" };
});

var app = builder.Build();

app.ApplyDatabaseMigrations();

// 1. IP & HTTPS from Nginx Reverse Proxy
app.UseForwardedHeaders();

// 2. Logging & Security Headers
app.UseCustomRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

/*app.UseStaticFiles()*/;
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        var fileName = ctx.File.Name.ToLowerInvariant();
        if (fileName == "sw.js" || fileName == "manifest.json" || fileName.EndsWith(".webmanifest"))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        }
    }
});

app.UseRouting();

// 3. Active Rate Limiter, Authentication & Authorization 
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 4. FEAT-P3-10: Open Dashboard Hangfire Security Only for Admin at /Admin/Hangfire
app.UseHangfireDashboard("/Admin/Hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthorizationFilter() },
    DashboardTitle = "Background Task Queue Management - Phu Trung Parish"
});
var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
    OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

// 5. TASK-P3-10.3: Register Recurring Job to automatically clean up Logs > 90 days at 02:00 AM every Sunday
// TASK-P3-12.2 (Job 1): Auto Pre-warm redis Attendence & Task class data every Sunday at 05:30 AM
RecurringJob.AddOrUpdate<IRedisAttendanceService>(
    "prewarm-sunday-attendance-cache",
    service => service.PreWarmDailyAttendanceAndTasksAsync(null),
    "30 5 * * 0", // 05:30 Sunday Morning
    new RecurringJobOptions { TimeZone = vnTimeZone });

// TASK-P3-12.2 (Job 2): Automatically Pre-warm the cache for Thursday Mass at 16:30 every Thursday
RecurringJob.AddOrUpdate<IRedisAttendanceService>(
    "prewarm-thursday-attendance-cache",
    service => service.PreWarmDailyAttendanceAndTasksAsync(null),
    "30 16 * * 4", // 16:30 Thursday Afternoon
    new RecurringJobOptions { TimeZone = vnTimeZone });

// TASK-P3-12.4 (Job 3): Automatically Sync-back (Batch Upsert) attendance results from Redis to PostgreSQL every 10 minutes
RecurringJob.AddOrUpdate<IRedisAttendanceService>(
    "syncback-redis-attendance-to-db",
    service => service.SyncDirtyAttendancesToDatabaseAsync(null),
    "*/10 * * * *", // Every 10 minutes
    new RecurringJobOptions { TimeZone = vnTimeZone });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
