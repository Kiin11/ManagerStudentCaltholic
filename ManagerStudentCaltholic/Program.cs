using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Middlewares;
using ManagerStudentCaltholic.Models.Entities;
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

// Addd Extensions for Database Configuration, Reverse Proxy, and Application Services
builder.Services.AddDatabaseConfiguration(builder.Configuration)
                .AddReverseProxyConfiguration()
                .AddApplicationServices()
                .AddHybridAuthentication(builder.Configuration)
                .AddStrictSecurityConfigurations() // task -707: Add Security Headers Middleware
                .AddAppRateLimiter(); // task-705: Add Rate Limiting

builder.Services.AddAuthorization(options =>
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
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Apply database migrations automatically on application startup
app.ApplyDatabaseMigrations();

// 5. ACTIVE CUSTOM LOGGING MIDDLEWARE
app.UseCustomRequestLogging();

// TASK-707: Active Security Headers Middleware head-pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
