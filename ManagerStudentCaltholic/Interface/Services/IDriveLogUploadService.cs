using ManagerStudentCaltholic.Models.DTOs;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IDriveLogUploadService
    {
        Task<DriveLogUploadRunResultDto> RunUploadCycleAsync(string triggerSource = "SCHEDULED", CancellationToken ct = default);
        Task<List<CloudMediaSummaryDto>> GetRecentLogUploadsAsync(int take = 30, CancellationToken ct = default);
    }
}
