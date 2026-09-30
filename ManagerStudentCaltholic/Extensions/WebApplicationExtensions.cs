using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Services;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Extensions
{
    public static class WebApplicationExtensions
    {
        /// <summary>
        /// Auto registry and run Migration to PostgreSQL database when the application starts. This is useful for development and testing environments. In production, it's recommended to manage migrations manually.
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
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

                // Seed tài khoản Quản trị viên hệ thống (Admin) và Cha Tuyên Úy nếu chưa có
                if (!dbContext.Users.Any())
                {
                    logger.LogInformation("Khởi tạo tài khoản mặc định ban đầu...");

                    var adminUser = new User
                    {
                        Username = "admin",
                        PasswordHash = passwordHasher.HashPassword("Password_Adm1n"),
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
                        Username = "chaxu",
                        PasswordHash = passwordHasher.HashPassword("ChaTienUy@Ph4Trung"),
                        FirstName = "Cha",
                        LastName = "Tuyên Úy",
                        ChristianName = "",
                        Role = UserRole.SpiritualDirector,
                        Email = "chaxu@parish.local",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.Users.AddRange(adminUser, spiritualDirector);
                    dbContext.SaveChanges();
                    logger.LogInformation("Đã tạo tài khoản mặc định: admin (Admin@123) và chaxu (ChaXu@123).");
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
