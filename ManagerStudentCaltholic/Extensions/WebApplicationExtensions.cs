using System.Security.Cryptography;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Extensions
{
    public static class WebApplicationExtensions
    {
        public static WebApplication ApplyDatabaseMigrations(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ParishDbContext>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ParishDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

            try
            {
                logger.LogInformation("Đang kiểm tra và áp dụng Migrations cơ sở dữ liệu...");
                dbContext.Database.Migrate();
                logger.LogInformation("Áp dụng Migrations thành công!");

                if (!dbContext.Users.Any())
                {
                    logger.LogInformation("Khởi tạo tài khoản mặc định ban đầu...");
                    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                    var adminInitPass = config["SeedSettings:AdminPassword"];
                    bool isGeneratedAdminPass = false;
                    if (string.IsNullOrWhiteSpace(adminInitPass))
                    {
                        adminInitPass = "Adm!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(9));
                        isGeneratedAdminPass = true;
                    }

                    var directorInitPass = config["SeedSettings:SpiritualDirectorPassword"];
                    bool isGeneratedDirectorPass = false;
                    if (string.IsNullOrWhiteSpace(directorInitPass))
                    {
                        directorInitPass = "Cha!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(9));
                        isGeneratedDirectorPass = true;
                    }

                    var adminUser = new User
                    {
                        Username = "admin",
                        PasswordHash = passwordHasher.HashPassword(adminInitPass),
                        FirstName = "Quản Trị Viên Kỹ Thuật",
                        LastName = "Hệ Thống",
                        ChristianName = "",
                        Role = UserRole.Admin,
                        Email = "admin@parish.local",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var spiritualDirector = new User
                    {
                        Username = "chatienuy",
                        PasswordHash = passwordHasher.HashPassword(directorInitPass),
                        FirstName = "Cha",
                        LastName = "Tuyên Úy",
                        ChristianName = "",
                        Role = UserRole.SpiritualDirector,
                        Email = "chatienuy@parish.local",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.Users.AddRange(adminUser, spiritualDirector);
                    dbContext.SaveChanges();

                    // SEC-NEW-12 FIX 2: Tuyệt đối không ghi mật khẩu cleartext vào Serilog (tránh lộ trên /Admin/Logs)
                    // Chỉ in một lần duy nhất ra Standard Console nếu chưa cấu hình biến môi trường SeedSettings
                    if (isGeneratedAdminPass || isGeneratedDirectorPass)
                    {
                        Console.WriteLine("==================================================================");
                        Console.WriteLine("[INIT SEED] Tài khoản khởi tạo lần đầu (Vui lòng đổi mật khẩu ngay):");
                        if (isGeneratedAdminPass)
                            Console.WriteLine($"  - Username: admin      | Password: {adminInitPass}");
                        if (isGeneratedDirectorPass)
                            Console.WriteLine($"  - Username: chatienuy  | Password: {directorInitPass}");
                        Console.WriteLine("==================================================================");
                    }

                    logger.LogInformation("Đã khởi tạo thành công 2 tài khoản hệ thống mặc định: 'admin' và 'chatienuy'.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Đã xảy ra lỗi trong quá trình tự động Migrate Database.");
                throw;
            }

            return app;
        }
    }
}