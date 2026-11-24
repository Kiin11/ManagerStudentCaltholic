using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IGoogleDriveStorageService
    {
        Task<CloudMediaFile> UploadAsync(CloudUploadRequest request, CancellationToken ct = default);
        Task<CloudDownloadResult?> DownloadAsync(long cloudMediaFileId, CancellationToken ct = default);
        Task<bool> DeleteAsync(long cloudMediaFileId, bool deletePhysicalOnDrive = true, CancellationToken ct = default);
        Task<string?> EnsureFolderPathAsync(IEnumerable<string> folderSegments, string? rootFolderIdOverride = null, CancellationToken ct = default);
        Task<bool> RetrySyncFallbackFileAsync(long cloudMediaFileId, CancellationToken ct = default);
        Task<int> RetryAllPendingFallbackFilesAsync(CancellationToken ct = default);
        Task<CloudStorageHealthDto> GetHealthStatusAsync(CancellationToken ct = default);
        Task<List<CloudMediaSummaryDto>> GetMediaForEntityAsync(string entityName, long entityId, string? category = null, CancellationToken ct = default);
        Task<Dictionary<long, List<CloudMediaSummaryDto>>> GetMediaMapForEntitiesAsync(string entityName, IEnumerable<long> entityIds, string? category = null, CancellationToken ct = default);
    }
}
