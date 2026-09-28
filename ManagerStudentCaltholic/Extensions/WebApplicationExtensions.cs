using ManagerStudentCaltholic.Data;
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

            try
            {
                logger.LogInformation("Đang kiểm tra và áp dụng Migrations cơ sở dữ liệu...");
                dbContext.Database.Migrate();
                logger.LogInformation("Áp dụng Migrations thành công!");
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
