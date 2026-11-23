using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace ManagerStudentCaltholic.Services
{
    public class GoogleDriveStorageService : IGoogleDriveStorageService
    {
        private const int MemoryDownloadThresholdBytes = 2 * 1024 * 1024; // 2 MB
        private const int MaxDriveChunkBytes = 2 * 1024 * 1024; // 2 MB
        private const int DriveChunkStepBytes = ResumableUpload.MinimumChunkSize; // 256 KB
        private static readonly TimeSpan FolderCacheTtl = TimeSpan.FromMinutes(30);

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> FolderLocks = new(StringComparer.Ordinal);
        private static readonly object ServiceCacheLock = new();
        private static string? _cachedKeyPath;
        private static DateTime _cachedKeyWriteTimeUtc;
        private static DriveService? _cachedDriveService;

        private readonly ParishDbContext _context;
        private readonly GoogleDriveOptions _options;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<GoogleDriveStorageService> _logger;

        public GoogleDriveStorageService(
            ParishDbContext context,
            IOptions<GoogleDriveOptions> options,
            IWebHostEnvironment env,
            IMemoryCache memoryCache,
            ILogger<GoogleDriveStorageService> logger)
        {
            _context = context;
            _options = options.Value;
            _env = env;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<CloudMediaFile> UploadAsync(CloudUploadRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Content == null)
            {
                throw new ArgumentNullException(nameof(request.Content));
            }

            if (string.IsNullOrWhiteSpace(request.Category))
            {
                throw new ArgumentException("Category không được để trống.", nameof(request));
            }

            var safeOriginalName = SanitizeOriginalFileName(request.OriginalFileName);
            var ext = Path.GetExtension(safeOriginalName).ToLowerInvariant();
            var storedFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
            var contentType = string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : Truncate(request.ContentType.Trim(), 120)!;
            var category = Truncate(request.Category.Trim(), 50)!;

            var segments = (request.FolderSegments ?? Array.Empty<string>())
                .Select(SanitizeDriveSegment)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            var driveFolderPath = segments.Count > 0
                ? Truncate(string.Join("/", segments), 300)
                : null;

            var tmpDir = GetCloudTempDirectory();
            var tmpPath = Path.Combine(tmpDir, $"{Guid.NewGuid():N}.tmp");
            bool movedToFallback = false;

            try
            {
                var (fileSize, sha256Hash) = await SpoolToTempFileAndComputeSha256Async(request.Content, tmpPath, ct);

                string? lastError = null;
                string? externalFileId = null;
                string? targetFolderId = null;
                int retryCount = 0;

                if (TryGetDriveService(out var driveService, out var unavailableReason) && driveService != null)
                {
                    try
                    {
                        targetFolderId = await EnsureFolderPathInternalAsync(
                            driveService,
                            segments,
                            request.ExplicitParentFolderId,
                            ct);

                        if (string.IsNullOrWhiteSpace(targetFolderId))
                        {
                            lastError = "Không xác định được thư mục đích trên Google Drive (RootFolderId chưa cấu hình).";
                        }
                        else
                        {
                            var uploadOutcome = await UploadTempFileWithRetryAsync(
                                driveService,
                                tmpPath,
                                storedFileName,
                                safeOriginalName,
                                contentType,
                                targetFolderId,
                                ct);

                            if (uploadOutcome.Success && !string.IsNullOrWhiteSpace(uploadOutcome.ExternalFileId))
                            {
                                externalFileId = uploadOutcome.ExternalFileId;
                                retryCount = uploadOutcome.RetryCount;
                            }
                            else
                            {
                                retryCount = uploadOutcome.RetryCount;
                                lastError = uploadOutcome.ErrorMessage ?? "Upload Google Drive thất bại.";
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi ngoại lệ khi tải tệp {FileName} lên Google Drive.", safeOriginalName);
                        lastError = ex.Message;
                    }
                }
                else
                {
                    lastError = unavailableReason;
                }

                if (!string.IsNullOrWhiteSpace(externalFileId))
                {
                    var driveRecord = new CloudMediaFile
                    {
                        Category = category,
                        EntityName = Truncate(request.EntityName, 80),
                        EntityId = request.EntityId,
                        OriginalFileName = safeOriginalName,
                        StoredFileName = storedFileName,
                        ContentType = contentType,
                        FileSize = fileSize,
                        Sha256Hash = sha256Hash,
                        StorageProvider = CloudStorageProvider.GoogleDrive,
                        ExternalFileId = Truncate(externalFileId, 150),
                        ExternalFolderId = Truncate(targetFolderId, 150),
                        DriveFolderPath = driveFolderPath,
                        LocalFallbackPath = null,
                        SyncStatus = CloudSyncStatus.Success,
                        RetryCount = retryCount,
                        LastError = null,
                        UploadedBy = Truncate(request.UploadedBy, 100),
                        UploadedByUserId = request.UploadedByUserId,
                        CreatedAt = DateTime.UtcNow,
                        SyncedAt = DateTime.UtcNow
                    };

                    _context.CloudMediaFiles.Add(driveRecord);
                    await _context.SaveChangesAsync(ct);
                    return driveRecord;
                }

                var truncatedError = Truncate(lastError ?? "Không thể tải tệp lên Google Drive.", 1000);

                if (request.AllowLocalFallback)
                {
                    var safeCategoryDir = SanitizeLocalSegment(category);
                    var fallbackRoot = GetLocalFallbackRoot();
                    var categoryDir = EnsureContainedPath(fallbackRoot, safeCategoryDir);
                    Directory.CreateDirectory(categoryDir);

                    var fallbackFullPath = EnsureContainedPath(categoryDir, storedFileName);
                    File.Move(tmpPath, fallbackFullPath, overwrite: true);
                    movedToFallback = true;

                    var relativeFallbackPath = Truncate(
                        Path.GetRelativePath(fallbackRoot, fallbackFullPath).Replace('\\', '/'),
                        500);

                    var fallbackRecord = new CloudMediaFile
                    {
                        Category = category,
                        EntityName = Truncate(request.EntityName, 80),
                        EntityId = request.EntityId,
                        OriginalFileName = safeOriginalName,
                        StoredFileName = storedFileName,
                        ContentType = contentType,
                        FileSize = fileSize,
                        Sha256Hash = sha256Hash,
                        StorageProvider = CloudStorageProvider.LocalFallback,
                        ExternalFileId = null,
                        ExternalFolderId = Truncate(targetFolderId, 150),
                        DriveFolderPath = driveFolderPath,
                        LocalFallbackPath = relativeFallbackPath,
                        SyncStatus = CloudSyncStatus.Retry,
                        RetryCount = retryCount,
                        LastError = truncatedError,
                        UploadedBy = Truncate(request.UploadedBy, 100),
                        UploadedByUserId = request.UploadedByUserId,
                        CreatedAt = DateTime.UtcNow,
                        SyncedAt = null
                    };

                    _context.CloudMediaFiles.Add(fallbackRecord);
                    await _context.SaveChangesAsync(ct);

                    _logger.LogWarning(
                        "Đã lưu tạm fallback cục bộ cho tệp {FileName} (CloudMediaId={CloudMediaId}). Lý do: {Reason}",
                        safeOriginalName,
                        fallbackRecord.Id,
                        truncatedError);

                    return fallbackRecord;
                }

                var failedRecord = new CloudMediaFile
                {
                    Category = category,
                    EntityName = Truncate(request.EntityName, 80),
                    EntityId = request.EntityId,
                    OriginalFileName = safeOriginalName,
                    StoredFileName = storedFileName,
                    ContentType = contentType,
                    FileSize = fileSize,
                    Sha256Hash = sha256Hash,
                    StorageProvider = CloudStorageProvider.GoogleDrive,
                    ExternalFileId = null,
                    ExternalFolderId = Truncate(targetFolderId, 150),
                    DriveFolderPath = driveFolderPath,
                    LocalFallbackPath = null,
                    SyncStatus = CloudSyncStatus.Failed,
                    RetryCount = retryCount,
                    LastError = truncatedError,
                    UploadedBy = Truncate(request.UploadedBy, 100),
                    UploadedByUserId = request.UploadedByUserId,
                    CreatedAt = DateTime.UtcNow,
                    SyncedAt = null
                };

                _context.CloudMediaFiles.Add(failedRecord);
                await _context.SaveChangesAsync(ct);
                return failedRecord;
            }
            finally
            {
                if (!movedToFallback)
                {
                    SafeDeleteFile(tmpPath);
                }
            }
        }

        public async Task<CloudDownloadResult?> DownloadAsync(long cloudMediaFileId, CancellationToken ct = default)
        {
            var media = await _context.CloudMediaFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == cloudMediaFileId, ct);

            if (media == null)
            {
                return null;
            }

            if (string.Equals(media.StorageProvider, CloudStorageProvider.GoogleDrive, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(media.ExternalFileId))
            {
                if (TryGetDriveService(out var driveService, out _) && driveService != null)
                {
                    var tmpDir = GetCloudTempDirectory();
                    var tmpPath = Path.Combine(tmpDir, $"dl_{Guid.NewGuid():N}.tmp");
                    var tmpStream = new FileStream(
                        tmpPath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 65536,
                        options: FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                    try
                    {
                        var getRequest = driveService.Files.Get(media.ExternalFileId);
                        getRequest.SupportsAllDrives = true;

                        var progress = await getRequest.DownloadAsync(tmpStream, ct);
                        if (progress.Status == DownloadStatus.Completed)
                        {
                            await tmpStream.FlushAsync(ct);
                            long actualSize = tmpStream.Length;
                            tmpStream.Position = 0;

                            if (actualSize <= MemoryDownloadThresholdBytes)
                            {
                                var memoryStream = new MemoryStream((int)actualSize);
                                await tmpStream.CopyToAsync(memoryStream, 65536, ct);
                                await tmpStream.DisposeAsync();
                                memoryStream.Position = 0;

                                return new CloudDownloadResult
                                {
                                    ContentStream = memoryStream,
                                    ContentType = media.ContentType,
                                    FileName = media.OriginalFileName,
                                    FileSize = actualSize,
                                    StorageProvider = CloudStorageProvider.GoogleDrive
                                };
                            }

                            return new CloudDownloadResult
                            {
                                ContentStream = tmpStream,
                                ContentType = media.ContentType,
                                FileName = media.OriginalFileName,
                                FileSize = actualSize,
                                StorageProvider = CloudStorageProvider.GoogleDrive
                            };
                        }

                        _logger.LogWarning(
                            progress.Exception,
                            "Tải tệp từ Google Drive thất bại (CloudMediaId={Id}, ExternalFileId={ExternalId}, Status={Status}).",
                            media.Id,
                            media.ExternalFileId,
                            progress.Status);
                        await tmpStream.DisposeAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        await tmpStream.DisposeAsync();
                        throw;
                    }
                    catch (Exception ex)
                    {
                        await tmpStream.DisposeAsync();
                        _logger.LogWarning(
                            ex,
                            "Lỗi khi tải tệp từ Google Drive (CloudMediaId={Id}, ExternalFileId={ExternalId}).",
                            media.Id,
                            media.ExternalFileId);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(media.LocalFallbackPath))
            {
                if (!TryResolveContainedFallbackPath(media.LocalFallbackPath, out var safeLocalPath))
                {
                    _logger.LogWarning(
                        "Từ chối truy cập đường dẫn fallback không hợp lệ cho CloudMediaId={Id}: {Path}",
                        media.Id,
                        media.LocalFallbackPath);
                    return null;
                }

                if (File.Exists(safeLocalPath))
                {
                    var fileInfo = new FileInfo(safeLocalPath);
                    var stream = new FileStream(
                        safeLocalPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 65536,
                        options: FileOptions.Asynchronous);

                    return new CloudDownloadResult
                    {
                        ContentStream = stream,
                        ContentType = media.ContentType,
                        FileName = media.OriginalFileName,
                        FileSize = fileInfo.Length,
                        StorageProvider = CloudStorageProvider.LocalFallback
                    };
                }
            }

            return null;
        }

        public async Task<bool> DeleteAsync(long cloudMediaFileId, bool deletePhysicalOnDrive = true, CancellationToken ct = default)
        {
            var media = await _context.CloudMediaFiles
                .FirstOrDefaultAsync(f => f.Id == cloudMediaFileId, ct);

            if (media == null)
            {
                return false;
            }

            if (deletePhysicalOnDrive
                && !string.IsNullOrWhiteSpace(media.ExternalFileId)
                && TryGetDriveService(out var driveService, out _)
                && driveService != null)
            {
                try
                {
                    var deleteReq = driveService.Files.Delete(media.ExternalFileId);
                    deleteReq.SupportsAllDrives = true;
                    await deleteReq.ExecuteAsync(ct);
                }
                catch (GoogleApiException gex) when (gex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    // Bỏ qua 404 vì tệp trên Drive đã bị xóa trước đó
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Không thể xóa tệp vật lý trên Google Drive (ExternalFileId={ExternalId}), vẫn tiếp tục xóa mềm DB.",
                        media.ExternalFileId);
                }
            }

            if (!string.IsNullOrWhiteSpace(media.LocalFallbackPath)
                && TryResolveContainedFallbackPath(media.LocalFallbackPath, out var safeLocalPath))
            {
                SafeDeleteFile(safeLocalPath);
            }

            media.UpdatedAt = DateTime.UtcNow;
            _context.CloudMediaFiles.Remove(media); // Global ISoftDelete handler chuyển thành IsDeleted = true
            await _context.SaveChangesAsync(ct);

            return true;
        }

        public async Task<string?> EnsureFolderPathAsync(
            IEnumerable<string> folderSegments,
            string? rootFolderIdOverride = null,
            CancellationToken ct = default)
        {
            if (!TryGetDriveService(out var driveService, out _) || driveService == null)
            {
                return null;
            }

            var cleanedSegments = (folderSegments ?? Array.Empty<string>())
                .Select(SanitizeDriveSegment)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            return await EnsureFolderPathInternalAsync(driveService, cleanedSegments, rootFolderIdOverride, ct);
        }

        public async Task<bool> RetrySyncFallbackFileAsync(long cloudMediaFileId, CancellationToken ct = default)
        {
            var media = await _context.CloudMediaFiles
                .FirstOrDefaultAsync(f => f.Id == cloudMediaFileId, ct);

            if (media == null)
            {
                return false;
            }

            if (!string.Equals(media.StorageProvider, CloudStorageProvider.LocalFallback, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(media.SyncStatus, CloudSyncStatus.Success, StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(media.LocalFallbackPath)
                || !TryResolveContainedFallbackPath(media.LocalFallbackPath, out var localFilePath)
                || !File.Exists(localFilePath))
            {
                media.SyncStatus = CloudSyncStatus.Failed;
                media.LastError = "Không tìm thấy tệp fallback cục bộ để đồng bộ lên Google Drive.";
                media.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return false;
            }

            if (!TryGetDriveService(out var driveService, out var unavailableReason) || driveService == null)
            {
                media.RetryCount++;
                media.LastError = Truncate(unavailableReason, 1000);
                media.UpdatedAt = DateTime.UtcNow;
                media.SyncStatus = CloudSyncStatus.Retry;
                await _context.SaveChangesAsync(ct);
                return false;
            }

            try
            {
                var segments = string.IsNullOrWhiteSpace(media.DriveFolderPath)
                    ? new List<string> { media.Category }
                    : media.DriveFolderPath
                        .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(SanitizeDriveSegment)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();

                var targetFolderId = !string.IsNullOrWhiteSpace(media.ExternalFolderId)
                    ? media.ExternalFolderId
                    : await EnsureFolderPathInternalAsync(driveService, segments, null, ct);

                if (string.IsNullOrWhiteSpace(targetFolderId))
                {
                    media.RetryCount++;
                    media.LastError = "Không xác định được thư mục đích trên Google Drive.";
                    media.UpdatedAt = DateTime.UtcNow;
                    media.SyncStatus = media.RetryCount >= Math.Max(1, _options.MaxRetryAttempts)
                        ? CloudSyncStatus.Failed
                        : CloudSyncStatus.Retry;
                    await _context.SaveChangesAsync(ct);
                    return false;
                }

                var outcome = await UploadTempFileWithRetryAsync(
                    driveService,
                    localFilePath,
                    media.StoredFileName,
                    media.OriginalFileName,
                    media.ContentType,
                    targetFolderId,
                    ct);

                if (outcome.Success && !string.IsNullOrWhiteSpace(outcome.ExternalFileId))
                {
                    media.StorageProvider = CloudStorageProvider.GoogleDrive;
                    media.ExternalFileId = Truncate(outcome.ExternalFileId, 150);
                    media.ExternalFolderId = Truncate(targetFolderId, 150);
                    media.SyncStatus = CloudSyncStatus.Success;
                    media.SyncedAt = DateTime.UtcNow;
                    media.UpdatedAt = DateTime.UtcNow;
                    media.LastError = null;
                    media.LocalFallbackPath = null;
                    await _context.SaveChangesAsync(ct);

                    SafeDeleteFile(localFilePath);
                    return true;
                }

                media.RetryCount += Math.Max(1, outcome.RetryCount);
                media.LastError = Truncate(outcome.ErrorMessage ?? "Đồng bộ lại lên Google Drive thất bại.", 1000);
                media.UpdatedAt = DateTime.UtcNow;
                media.SyncStatus = media.RetryCount >= Math.Max(1, _options.MaxRetryAttempts)
                    ? CloudSyncStatus.Failed
                    : CloudSyncStatus.Retry;
                await _context.SaveChangesAsync(ct);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ lại tệp fallback CloudMediaId={Id}.", media.Id);
                media.RetryCount++;
                media.LastError = Truncate(ex.Message, 1000);
                media.UpdatedAt = DateTime.UtcNow;
                media.SyncStatus = media.RetryCount >= Math.Max(1, _options.MaxRetryAttempts)
                    ? CloudSyncStatus.Failed
                    : CloudSyncStatus.Retry;
                await _context.SaveChangesAsync(ct);
                return false;
            }
        }

        public async Task<int> RetryAllPendingFallbackFilesAsync(CancellationToken ct = default)
        {
            var pendingIds = await _context.CloudMediaFiles
                .AsNoTracking()
                .Where(f => f.StorageProvider == CloudStorageProvider.LocalFallback
                    && (f.SyncStatus == CloudSyncStatus.Retry || f.SyncStatus == CloudSyncStatus.PendingSync))
                .OrderBy(f => f.CreatedAt)
                .Select(f => f.Id)
                .ToListAsync(ct);

            int syncedCount = 0;
            foreach (var id in pendingIds)
            {
                ct.ThrowIfCancellationRequested();
                if (await RetrySyncFallbackFileAsync(id, ct))
                {
                    syncedCount++;
                }
            }

            return syncedCount;
        }

        public async Task<CloudStorageHealthDto> GetHealthStatusAsync(CancellationToken ct = default)
        {
            var groupedCounts = await _context.CloudMediaFiles
                .AsNoTracking()
                .GroupBy(f => new { f.Category, f.StorageProvider, f.SyncStatus })
                .Select(g => new
                {
                    g.Key.Category,
                    g.Key.StorageProvider,
                    g.Key.SyncStatus,
                    Count = g.Count()
                })
                .ToListAsync(ct);

            var countByCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int totalFiles = 0;
            int driveSynced = 0;
            int localFallbackPending = 0;
            int failed = 0;

            foreach (var item in groupedCounts)
            {
                totalFiles += item.Count;

                if (countByCategory.TryGetValue(item.Category, out var existing))
                {
                    countByCategory[item.Category] = existing + item.Count;
                }
                else
                {
                    countByCategory[item.Category] = item.Count;
                }

                if (string.Equals(item.StorageProvider, CloudStorageProvider.GoogleDrive, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.SyncStatus, CloudSyncStatus.Success, StringComparison.OrdinalIgnoreCase))
                {
                    driveSynced += item.Count;
                }
                else if (string.Equals(item.StorageProvider, CloudStorageProvider.LocalFallback, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(item.SyncStatus, CloudSyncStatus.Retry, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.SyncStatus, CloudSyncStatus.PendingSync, StringComparison.OrdinalIgnoreCase)))
                {
                    localFallbackPending += item.Count;
                }

                if (string.Equals(item.SyncStatus, CloudSyncStatus.Failed, StringComparison.OrdinalIgnoreCase))
                {
                    failed += item.Count;
                }
            }

            var resolvedKeyPath = ResolveConfiguredPath(_options.ServiceAccountKeyPath, string.Empty);
            bool isKeyFound = !string.IsNullOrWhiteSpace(resolvedKeyPath) && File.Exists(resolvedKeyPath);
            bool isRootConfigured = !string.IsNullOrWhiteSpace(_options.RootFolderId)
                || !string.IsNullOrWhiteSpace(_options.SharedDriveId);

            return new CloudStorageHealthDto
            {
                IsDriveEnabled = _options.Enabled,
                IsServiceAccountKeyFound = isKeyFound,
                IsRootFolderConfigured = isRootConfigured,
                RootFolderName = _options.RootFolderName,
                TotalFilesCount = totalFiles,
                DriveSyncedCount = driveSynced,
                LocalFallbackPendingCount = localFallbackPending,
                FailedCount = failed,
                CountByCategory = countByCategory
            };
        }

        public async Task<List<CloudMediaSummaryDto>> GetMediaForEntityAsync(
            string entityName,
            long entityId,
            string? category = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return new List<CloudMediaSummaryDto>();
            }

            var query = _context.CloudMediaFiles
                .AsNoTracking()
                .Where(f => f.EntityName == entityName && f.EntityId == entityId);

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(f => f.Category == category);
            }

            var items = await query
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => MapToSummaryDto(f))
                .ToListAsync(ct);

            return items;
        }

        public async Task<Dictionary<long, List<CloudMediaSummaryDto>>> GetMediaMapForEntitiesAsync(
            string entityName,
            IEnumerable<long> entityIds,
            string? category = null,
            CancellationToken ct = default)
        {
            var idList = entityIds?.Distinct().ToList() ?? new List<long>();
            if (string.IsNullOrWhiteSpace(entityName) || idList.Count == 0)
            {
                return new Dictionary<long, List<CloudMediaSummaryDto>>();
            }

            var query = _context.CloudMediaFiles
                .AsNoTracking()
                .Where(f => f.EntityName == entityName
                    && f.EntityId.HasValue
                    && idList.Contains(f.EntityId.Value));

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(f => f.Category == category);
            }

            var items = await query
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => MapToSummaryDto(f))
                .ToListAsync(ct);

            return items
                .Where(dto => dto.EntityId.HasValue)
                .GroupBy(dto => dto.EntityId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private static CloudMediaSummaryDto MapToSummaryDto(CloudMediaFile f) => new()
        {
            Id = f.Id,
            Category = f.Category,
            EntityName = f.EntityName,
            EntityId = f.EntityId,
            OriginalFileName = f.OriginalFileName,
            ContentType = f.ContentType,
            FileSize = f.FileSize,
            StorageProvider = f.StorageProvider,
            ExternalFileId = f.ExternalFileId,
            DriveFolderPath = f.DriveFolderPath,
            SyncStatus = f.SyncStatus,
            RetryCount = f.RetryCount,
            LastError = f.LastError,
            UploadedBy = f.UploadedBy,
            CreatedAt = f.CreatedAt,
            SyncedAt = f.SyncedAt,
            ProxyUrl = $"/CloudMedia/File/{f.Id}"
        };

        private bool TryGetDriveService(out DriveService? driveService, out string? unavailableReason)
        {
            driveService = null;
            unavailableReason = null;

            if (!_options.Enabled)
            {
                unavailableReason = "Google Drive storage đã tắt trong cấu hình (GoogleDrive:Enabled = false).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_options.ServiceAccountKeyPath))
            {
                unavailableReason = "Chưa cấu hình đường dẫn Service Account key (GoogleDrive:ServiceAccountKeyPath).";
                return false;
            }

            var resolvedKeyPath = ResolveConfiguredPath(_options.ServiceAccountKeyPath, string.Empty);
            if (string.IsNullOrWhiteSpace(resolvedKeyPath) || !File.Exists(resolvedKeyPath))
            {
                unavailableReason = $"Không tìm thấy tệp Service Account key tại '{resolvedKeyPath}'.";
                return false;
            }

            try
            {
                var keyWriteTimeUtc = File.GetLastWriteTimeUtc(resolvedKeyPath);
                lock (ServiceCacheLock)
                {
                    if (_cachedDriveService != null
                        && string.Equals(_cachedKeyPath, resolvedKeyPath, StringComparison.OrdinalIgnoreCase)
                        && _cachedKeyWriteTimeUtc == keyWriteTimeUtc)
                    {
                        driveService = _cachedDriveService;
                        return true;
                    }

                    var credential = CredentialFactory
                        .FromFile<ServiceAccountCredential>(resolvedKeyPath)
                        .ToGoogleCredential()
                        .CreateScoped(DriveService.Scope.Drive);

                    var createdService = new DriveService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "ManagerStudentCaltholic"
                    });

                    _cachedDriveService = createdService;
                    _cachedKeyPath = resolvedKeyPath;
                    _cachedKeyWriteTimeUtc = keyWriteTimeUtc;
                    driveService = createdService;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể khởi tạo Google DriveService từ '{KeyPath}'.", resolvedKeyPath);
                unavailableReason = $"Lỗi khởi tạo Google Drive credential: {ex.Message}";
                return false;
            }
        }

        private async Task<string?> EnsureFolderPathInternalAsync(
            DriveService driveService,
            IReadOnlyList<string> cleanedSegments,
            string? rootFolderIdOverride,
            CancellationToken ct)
        {
            string? currentParentId = null;
            var effectiveSegments = new List<string>();

            if (!string.IsNullOrWhiteSpace(rootFolderIdOverride))
            {
                currentParentId = rootFolderIdOverride.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(_options.RootFolderId))
            {
                currentParentId = _options.RootFolderId.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(_options.SharedDriveId))
            {
                currentParentId = _options.SharedDriveId.Trim();
                if (!string.IsNullOrWhiteSpace(_options.RootFolderName))
                {
                    effectiveSegments.Add(SanitizeDriveSegment(_options.RootFolderName));
                }
            }
            else if (!string.IsNullOrWhiteSpace(_options.RootFolderName))
            {
                effectiveSegments.Add(SanitizeDriveSegment(_options.RootFolderName));
            }
            else
            {
                return null;
            }

            foreach (var segment in cleanedSegments)
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    effectiveSegments.Add(segment);
                }
            }

            foreach (var segment in effectiveSegments)
            {
                currentParentId = await FindOrCreateSingleFolderAsync(driveService, currentParentId, segment, ct);
                if (string.IsNullOrWhiteSpace(currentParentId))
                {
                    return null;
                }
            }

            return currentParentId;
        }

        private async Task<string> FindOrCreateSingleFolderAsync(
            DriveService driveService,
            string? parentFolderId,
            string folderName,
            CancellationToken ct)
        {
            var cacheKey = $"gdrive:folder:{_options.SharedDriveId}:{parentFolderId ?? "ROOT"}:{folderName}";
            if (_memoryCache.TryGetValue(cacheKey, out string? cachedFolderId)
                && !string.IsNullOrWhiteSpace(cachedFolderId))
            {
                return cachedFolderId;
            }

            var gate = FolderLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                if (_memoryCache.TryGetValue(cacheKey, out cachedFolderId)
                    && !string.IsNullOrWhiteSpace(cachedFolderId))
                {
                    return cachedFolderId;
                }

                var escapedName = EscapeDriveQuery(folderName);
                var queryBuilder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(parentFolderId))
                {
                    queryBuilder.Append('\'').Append(EscapeDriveQuery(parentFolderId)).Append("' in parents and ");
                }
                queryBuilder.Append("mimeType = 'application/vnd.google-apps.folder' and name = '")
                    .Append(escapedName)
                    .Append("' and trashed = false");

                var listReq = driveService.Files.List();
                listReq.Q = queryBuilder.ToString();
                listReq.Fields = "files(id, name)";
                listReq.PageSize = 5;
                listReq.SupportsAllDrives = true;
                listReq.IncludeItemsFromAllDrives = true;

                if (!string.IsNullOrWhiteSpace(_options.SharedDriveId))
                {
                    listReq.Corpora = "drive";
                    listReq.DriveId = _options.SharedDriveId.Trim();
                }

                var listResult = await listReq.ExecuteAsync(ct);
                var existing = listResult.Files?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.Id));
                if (existing != null)
                {
                    _memoryCache.Set(cacheKey, existing.Id, FolderCacheTtl);
                    return existing.Id;
                }

                var folderMetadata = new DriveFile
                {
                    Name = folderName,
                    MimeType = "application/vnd.google-apps.folder"
                };

                if (!string.IsNullOrWhiteSpace(parentFolderId))
                {
                    folderMetadata.Parents = new List<string> { parentFolderId };
                }

                var createReq = driveService.Files.Create(folderMetadata);
                createReq.SupportsAllDrives = true;
                createReq.Fields = "id";

                var created = await createReq.ExecuteAsync(ct);
                _memoryCache.Set(cacheKey, created.Id, FolderCacheTtl);
                return created.Id;
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<(bool Success, string? ExternalFileId, int RetryCount, string? ErrorMessage)> UploadTempFileWithRetryAsync(
            DriveService driveService,
            string tempFilePath,
            string storedFileName,
            string originalFileName,
            string contentType,
            string targetFolderId,
            CancellationToken ct)
        {
            int maxAttempts = Math.Max(1, _options.MaxRetryAttempts);
            int chunkSize = ResolveValidChunkSizeBytes();
            string? lastError = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await using var fileStream = new FileStream(
                        tempFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 65536,
                        options: FileOptions.Asynchronous);

                    var fileMetadata = new DriveFile
                    {
                        Name = storedFileName,
                        Description = originalFileName,
                        Parents = new List<string> { targetFolderId }
                    };

                    var uploadRequest = driveService.Files.Create(fileMetadata, fileStream, contentType);
                    uploadRequest.SupportsAllDrives = true;
                    uploadRequest.Fields = "id, name, size, parents";
                    uploadRequest.ChunkSize = chunkSize;

                    var progress = await uploadRequest.UploadAsync(ct);
                    if (progress.Status == UploadStatus.Completed && !string.IsNullOrWhiteSpace(uploadRequest.ResponseBody?.Id))
                    {
                        return (true, uploadRequest.ResponseBody.Id, attempt - 1, null);
                    }

                    lastError = progress.Exception?.Message ?? $"Google Drive upload trả về trạng thái {progress.Status}.";
                    _logger.LogWarning(
                        progress.Exception,
                        "Upload Google Drive lần {Attempt}/{Max} thất bại cho {FileName}: {Error}",
                        attempt,
                        maxAttempts,
                        storedFileName,
                        lastError);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    _logger.LogWarning(
                        ex,
                        "Upload Google Drive lần {Attempt}/{Max} gặp ngoại lệ cho {FileName}.",
                        attempt,
                        maxAttempts,
                        storedFileName);
                }

                if (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, ct);
                }
            }

            return (false, null, maxAttempts, lastError);
        }

        private int ResolveValidChunkSizeBytes()
        {
            int configured = _options.UploadChunkSizeBytes > 0
                ? _options.UploadChunkSizeBytes
                : MaxDriveChunkBytes;

            int clamped = Math.Clamp(configured, DriveChunkStepBytes, MaxDriveChunkBytes);
            int aligned = (clamped / DriveChunkStepBytes) * DriveChunkStepBytes;
            return Math.Max(DriveChunkStepBytes, aligned);
        }

        private static async Task<(long FileSize, string Sha256Hash)> SpoolToTempFileAndComputeSha256Async(
            Stream source,
            string tempFilePath,
            CancellationToken ct)
        {
            if (source.CanSeek)
            {
                source.Position = 0;
            }

            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            long totalBytes = 0;

            try
            {
                await using var target = new FileStream(
                    tempFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous);

                while (true)
                {
                    int bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    hasher.AppendData(buffer, 0, bytesRead);
                    await target.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalBytes += bytesRead;
                }

                await target.FlushAsync(ct);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            var hashBytes = hasher.GetHashAndReset();
            return (totalBytes, Convert.ToHexString(hashBytes).ToLowerInvariant());
        }

        private string ResolveConfiguredPath(string? configuredPath, string defaultRelative)
        {
            var raw = string.IsNullOrWhiteSpace(configuredPath) ? defaultRelative : configuredPath.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(raw)
                ? Path.GetFullPath(raw)
                : Path.GetFullPath(Path.Combine(_env.ContentRootPath, raw));
        }

        private string GetCloudTempDirectory()
        {
            var dir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "PrivateUploads", "cloud_tmp"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetLocalFallbackRoot()
        {
            var root = ResolveConfiguredPath(_options.LocalFallbackRoot, "PrivateUploads/cloud_fallback");
            Directory.CreateDirectory(root);
            return root;
        }

        private bool TryResolveContainedFallbackPath(string storedFallbackPath, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(storedFallbackPath))
            {
                return false;
            }

            var fallbackRoot = GetLocalFallbackRoot();
            var normalizedRoot = EnsureTrailingSeparator(fallbackRoot);

            var sanitizedRelative = storedFallbackPath.Trim()
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(sanitizedRelative))
            {
                return false;
            }

            var candidate = Path.GetFullPath(Path.Combine(fallbackRoot, sanitizedRelative));
            if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }

        private static string EnsureContainedPath(string rootDirectory, string childSegmentOrFile)
        {
            var rootFull = Path.GetFullPath(rootDirectory);
            var normalizedRoot = EnsureTrailingSeparator(rootFull);
            var candidate = Path.GetFullPath(Path.Combine(rootFull, childSegmentOrFile));
            if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Đường dẫn tệp vượt ra ngoài thư mục cho phép.");
            }

            return candidate;
        }

        private static string EnsureTrailingSeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;

        private static string SanitizeOriginalFileName(string? fileName)
        {
            var nameOnly = Path.GetFileName((fileName ?? string.Empty).Trim());
            if (string.IsNullOrWhiteSpace(nameOnly))
            {
                return "file.bin";
            }

            return Truncate(nameOnly, 255)!;
        }

        private static string SanitizeDriveSegment(string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return string.Empty;
            }

            var cleaned = segment.Trim()
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace("..", string.Empty);

            return cleaned.Trim();
        }

        private static string SanitizeLocalSegment(string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return "OTHER";
            }

            var sb = new StringBuilder(segment.Length);
            foreach (char ch in segment.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                {
                    sb.Append(ch);
                }
            }

            return sb.Length > 0 ? sb.ToString() : "OTHER";
        }

        private static string EscapeDriveQuery(string value) =>
            value.Replace("\\", "\\\\").Replace("'", "\\'");

        private static string? Truncate(string? value, int maxLength)
        {
            if (value == null)
            {
                return null;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static void SafeDeleteFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Bỏ qua lỗi dọn file tạm
            }
        }
    }
}
