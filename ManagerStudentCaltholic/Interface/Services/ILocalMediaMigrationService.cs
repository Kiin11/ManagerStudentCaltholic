using ManagerStudentCaltholic.Models.DTOs;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface ILocalMediaMigrationService
    {
        Task<LocalMigrationReportDto> RunMigrationAsync(
            bool dryRun = true,
            bool deleteLocalAfterVerified = false,
            string? operatorName = null,
            CancellationToken ct = default);
    }
}
