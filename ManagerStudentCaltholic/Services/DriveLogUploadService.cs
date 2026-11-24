using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ManagerStudentCaltholic.Services
{
    public class DriveLogUploadService : IDriveLogUploadService
    {
        private const long MaxLogFileBytes = 200L * 1024 * 1024; // 200 MB compression guard
        private static readonly TimeSpan MaxInlineRetryWait = TimeSpan.FromSeconds(30);
        private static readonly SemaphoreSlim RunLock = new(1, 1);
        private static readonly Regex LogDateRegex = new(@"(\d{4})(\d{2})(\d{2})", RegexOptions.Compiled);

        private readonly ParishDbContext _context;
        private readonly IGoogleDriveStorageService _storageService;
        private readonly DriveLogUploadOptions _options;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DriveLogUploadService> _logger;

        public DriveLogUploadService(
            ParishDbContext context,
            IGoogleDriveStorageService storageService,
            IOptions<DriveLogUploadOptions> options,
            IWebHostEnvironment env,
            ILogger<DriveLogUploadService> logger)
        {
            _context = context;
            _storageService = storageService;
            _options = options.Value;
            _env = env;
            _logger = logger;
        }

        public async Task<DriveLogUploadRunResultDto> RunUploadCycleAsync(
            string triggerSource = "SCHEDULED",
            CancellationToken ct = default)
        {
            var normalizedTrigger = string.IsNullOrWhiteSpace(triggerSource)
                ? "SCHEDULED"
                : Truncate(triggerSource.Trim(), 100)!;

            var result = new DriveLogUploadRunResultDto
            {
                StartedAtUtc = DateTime.UtcNow,
                TriggerSource = normalizedTrigger
            };

            if (!await RunLock.WaitAsync(0, ct))
            {
                result.FinishedAtUtc = DateTime.UtcNow;
                result.Messages.Add("Đang có một tiến trình đồng bộ log lên Google Drive khác đang thực thi.");
                return result;
            }

            try
            {
                var logsDir = ResolveConfiguredPath(_options.LocalLogsPath, "logs");
                if (!Directory.Exists(logsDir))
                {
                    result.Messages.Add($"Thư mục logs chưa tồn tại ({logsDir}).");
                    result.FinishedAtUtc = DateTime.UtcNow;
                    return result;
                }

                var vnTimeZone = VietnamTimeHelper.VietnamTimeZone;
                var todayVn = VietnamTimeHelper.Today;
                int retentionDays = Math.Max(1, _options.LocalRetentionDays);
                var cutoffDate = todayVn.AddDays(-(retentionDays - 1));
                bool isScheduledTrigger = string.Equals(normalizedTrigger, "SCHEDULED", StringComparison.OrdinalIgnoreCase);
                int maxRetriesPerFile = Math.Max(1, _options.MaxRetriesPerFile);
                int retryDelaySeconds = Math.Max(0, _options.RetryDelaySeconds);
                int bufferSize = Math.Clamp(_options.UploadBufferSizeBytes, 4096, 1024 * 1024);

                var tmpLogsDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "PrivateUploads", "tmp_logs"));
                Directory.CreateDirectory(tmpLogsDir);

                var logFiles = new DirectoryInfo(logsDir)
                    .GetFiles("*.log", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var fileInfo in logFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    if (IsProtectedActiveLogFile(fileInfo, todayVn, cutoffDate, vnTimeZone))
                    {
                        result.SkippedActiveCount++;
                        continue;
                    }

                    result.ScannedFiles++;

                    string gzOriginalName = $"{fileInfo.Name}.gz";
                    var existingRecords = await _context.CloudMediaFiles
                        .Where(f => f.Category == CloudMediaCategory.SystemLog && f.OriginalFileName == gzOriginalName)
                        .OrderByDescending(f => f.CreatedAt)
                        .ToListAsync(ct);

                    var verifiedExisting = existingRecords.FirstOrDefault(f =>
                        string.Equals(f.StorageProvider, CloudStorageProvider.GoogleDrive, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(f.SyncStatus, CloudSyncStatus.Success, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(f.ExternalFileId));

                    if (verifiedExisting != null)
                    {
                        if (_options.DeleteLocalAfterVerifiedUpload
                            && !IsProtectedActiveLogFile(fileInfo, todayVn, cutoffDate, vnTimeZone))
                        {
                            try
                            {
                                File.Delete(fileInfo.FullName);
                                result.DeletedLocalCount++;
                                result.Messages.Add($"Bỏ qua upload {fileInfo.Name} (đã xác thực trên Google Drive CloudMediaId={verifiedExisting.Id}) và đã xóa file cục bộ còn sót.");
                            }
                            catch (Exception deleteEx)
                            {
                                _logger.LogWarning(
                                    deleteEx,
                                    "Tệp {FileName} đã lưu xác thực trên Drive (CloudMediaId={CloudMediaId}) nhưng không thể xóa file cục bộ còn sót.",
                                    fileInfo.Name,
                                    verifiedExisting.Id);
                                result.Messages.Add($"Bỏ qua {fileInfo.Name}: đã lưu trên Drive (CloudMediaId={verifiedExisting.Id}) nhưng không xóa được file cục bộ: {deleteEx.Message}");
                            }
                        }
                        else
                        {
                            result.Messages.Add($"Bỏ qua {fileInfo.Name}: đã lưu xác thực trên Google Drive (CloudMediaId={verifiedExisting.Id}).");
                        }
                        continue;
                    }

                    if (fileInfo.Length > MaxLogFileBytes)
                    {
                        var sizeMb = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 1);
                        _logger.LogWarning(
                            "Bỏ qua nén/upload tệp log {FileName} vì dung lượng ({SizeMb} MB) vượt ngưỡng an toàn {MaxMb} MB.",
                            fileInfo.Name,
                            sizeMb,
                            MaxLogFileBytes / (1024 * 1024));
                        result.FailedCount++;
                        result.Messages.Add($"Bỏ qua {fileInfo.Name}: dung lượng {sizeMb} MB vượt giới hạn {MaxLogFileBytes / (1024 * 1024)} MB.");
                        continue;
                    }

                    var latestPrior = existingRecords.FirstOrDefault();
                    int priorRetryCount = latestPrior?.RetryCount ?? 0;

                    if (isScheduledTrigger
                        && latestPrior != null
                        && string.Equals(latestPrior.SyncStatus, CloudSyncStatus.Failed, StringComparison.OrdinalIgnoreCase)
                        && priorRetryCount >= maxRetriesPerFile)
                    {
                        result.FailedCount++;
                        result.Messages.Add($"Bỏ qua {fileInfo.Name}: đã đạt ngưỡng {maxRetriesPerFile} lần thử ({latestPrior.LastError}).");
                        continue;
                    }

                    if (latestPrior != null
                        && string.Equals(latestPrior.SyncStatus, CloudSyncStatus.Retry, StringComparison.OrdinalIgnoreCase)
                        && retryDelaySeconds > 0)
                    {
                        var lastAttemptUtc = DateTime.SpecifyKind(latestPrior.UpdatedAt ?? latestPrior.CreatedAt, DateTimeKind.Utc);
                        var nextAllowedRetryUtc = lastAttemptUtc.AddSeconds(retryDelaySeconds);
                        var remainingDelay = nextAllowedRetryUtc - DateTime.UtcNow;

                        if (remainingDelay > TimeSpan.Zero)
                        {
                            if (isScheduledTrigger && remainingDelay > MaxInlineRetryWait)
                            {
                                result.Messages.Add(
                                    $"Hoãn thử lại {fileInfo.Name} đến {nextAllowedRetryUtc:HH:mm:ss} UTC (RetryDelaySeconds={retryDelaySeconds}s).");
                                continue;
                            }

                            await Task.Delay(remainingDelay, ct);
                        }
                    }

                    string yearMonth = ResolveLogYearMonth(fileInfo, vnTimeZone);
                    bool hasExplicitLogsFolder = !string.IsNullOrWhiteSpace(_options.LogsFolderId);
                    var folderSegments = hasExplicitLogsFolder
                        ? new[] { yearMonth }
                        : new[] { "System_Logs", yearMonth };
                    var explicitParentFolderId = hasExplicitLogsFolder
                        ? _options.LogsFolderId.Trim()
                        : null;

                    var tmpGzPath = Path.Combine(
                        tmpLogsDir,
                        $"{Path.GetFileNameWithoutExtension(fileInfo.Name)}_{Guid.NewGuid():N}.log.gz");

                    try
                    {
                        await CompressLogFileAsync(fileInfo.FullName, tmpGzPath, bufferSize, ct);

                        CloudMediaFile media;
                        await using (var gzReadStream = new FileStream(
                            tmpGzPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            bufferSize: bufferSize,
                            options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            media = await _storageService.UploadAsync(new CloudUploadRequest
                            {
                                Content = gzReadStream,
                                OriginalFileName = gzOriginalName,
                                ContentType = "application/gzip",
                                Category = CloudMediaCategory.SystemLog,
                                EntityName = "SystemLog",
                                EntityId = null,
                                FolderSegments = folderSegments,
                                ExplicitParentFolderId = explicitParentFolderId,
                                UploadedBy = normalizedTrigger,
                                UploadedByUserId = null,
                                AllowLocalFallback = false
                            }, ct);
                        }

                        // Soft-delete any superseded failed/retry records for the same log file so only 1 active audit row remains
                        var superseded = existingRecords.Where(r => r.Id != media.Id).ToList();
                        if (superseded.Count > 0)
                        {
                            _context.CloudMediaFiles.RemoveRange(superseded);
                        }

                        bool verifiedDriveSuccess =
                            string.Equals(media.StorageProvider, CloudStorageProvider.GoogleDrive, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(media.SyncStatus, CloudSyncStatus.Success, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(media.ExternalFileId);

                        if (verifiedDriveSuccess)
                        {
                            if (priorRetryCount > 0)
                            {
                                media.RetryCount = priorRetryCount;
                                media.UpdatedAt = DateTime.UtcNow;
                            }

                            if (superseded.Count > 0 || priorRetryCount > 0)
                            {
                                await _context.SaveChangesAsync(ct);
                            }

                            result.UploadedCount++;
                            result.Messages.Add($"Upload thành công {gzOriginalName} -> Drive FileId={media.ExternalFileId}.");

                            if (_options.DeleteLocalAfterVerifiedUpload
                                && !IsProtectedActiveLogFile(fileInfo, todayVn, cutoffDate, vnTimeZone))
                            {
                                try
                                {
                                    File.Delete(fileInfo.FullName);
                                    result.DeletedLocalCount++;
                                }
                                catch (Exception deleteEx)
                                {
                                    _logger.LogWarning(
                                        deleteEx,
                                        "Đã upload thành công {FileName} lên Drive nhưng không thể xóa file log gốc cục bộ.",
                                        fileInfo.Name);
                                    result.Messages.Add($"Đã upload {fileInfo.Name} nhưng không xóa được file gốc: {deleteEx.Message}");
                                }
                            }
                        }
                        else
                        {
                            int nextAttemptCount = priorRetryCount + 1;
                            media.RetryCount = nextAttemptCount;
                            media.SyncStatus = nextAttemptCount < maxRetriesPerFile
                                ? CloudSyncStatus.Retry
                                : CloudSyncStatus.Failed;
                            media.UpdatedAt = DateTime.UtcNow;

                            await _context.SaveChangesAsync(ct);

                            result.FailedCount++;
                            result.Messages.Add(
                                $"Upload thất bại {gzOriginalName} (Status={media.SyncStatus}, Retry={nextAttemptCount}/{maxRetriesPerFile}): {media.LastError}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi ngoại lệ khi nén/upload file log {FileName}.", fileInfo.Name);
                        result.FailedCount++;
                        result.Messages.Add($"Lỗi xử lý {fileInfo.Name}: {ex.Message}");
                    }
                    finally
                    {
                        SafeDeleteFile(tmpGzPath);
                    }
                }

                if (result.ScannedFiles == 0)
                {
                    result.Messages.Add($"Không có tệp log cũ cần upload (bỏ qua {result.SkippedActiveCount} tệp đang hoạt động/trong thời gian giữ lại).");
                }
            }
            finally
            {
                result.FinishedAtUtc = DateTime.UtcNow;
                RunLock.Release();
            }

            return result;
        }

        public async Task<List<CloudMediaSummaryDto>> GetRecentLogUploadsAsync(int take = 30, CancellationToken ct = default)
        {
            int limit = Math.Clamp(take, 1, 200);

            return await _context.CloudMediaFiles
                .AsNoTracking()
                .Where(f => f.Category == CloudMediaCategory.SystemLog)
                .OrderByDescending(f => f.CreatedAt)
                .Take(limit)
                .Select(f => new CloudMediaSummaryDto
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
                })
                .ToListAsync(ct);
        }

        private static async Task CompressLogFileAsync(
            string sourceLogPath,
            string destinationGzPath,
            int bufferSize,
            CancellationToken ct)
        {
            await using var sourceStream = new FileStream(
                sourceLogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: bufferSize,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var targetStream = new FileStream(
                destinationGzPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: bufferSize,
                options: FileOptions.Asynchronous);

            await using var gzipStream = new GZipStream(targetStream, CompressionLevel.Optimal, leaveOpen: false);

            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while (true)
                {
                    int bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, bufferSize), ct);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    await gzipStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                }

                await gzipStream.FlushAsync(ct);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static bool IsProtectedActiveLogFile(
            FileInfo fileInfo,
            DateTime todayVn,
            DateTime cutoffDate,
            TimeZoneInfo vnTimeZone)
        {
            string todayStamp = todayVn.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (fileInfo.Name.Contains(todayStamp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (TryExtractDateFromFileName(fileInfo.Name, out var nameDate))
            {
                if (nameDate.Date >= todayVn || nameDate.Date >= cutoffDate)
                {
                    return true;
                }
            }

            var lastWriteUtc = DateTime.SpecifyKind(fileInfo.LastWriteTimeUtc, DateTimeKind.Utc);
            var lastWriteVnDate = TimeZoneInfo.ConvertTimeFromUtc(lastWriteUtc, vnTimeZone).Date;
            return lastWriteVnDate >= todayVn || lastWriteVnDate >= cutoffDate;
        }

        private static string ResolveLogYearMonth(FileInfo fileInfo, TimeZoneInfo vnTimeZone)
        {
            if (TryExtractDateFromFileName(fileInfo.Name, out var nameDate))
            {
                return nameDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            }

            var lastWriteUtc = DateTime.SpecifyKind(fileInfo.LastWriteTimeUtc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(lastWriteUtc, vnTimeZone).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        private static bool TryExtractDateFromFileName(string fileName, out DateTime parsedDate)
        {
            parsedDate = default;
            var matches = LogDateRegex.Matches(fileName);
            foreach (Match match in matches)
            {
                if (!match.Success)
                {
                    continue;
                }

                if (int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int year)
                    && int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int month)
                    && int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int day)
                    && year is >= 2000 and <= 2100
                    && month is >= 1 and <= 12
                    && day >= 1
                    && day <= DateTime.DaysInMonth(year, month))
                {
                    parsedDate = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
                    return true;
                }
            }

            return false;
        }

        private string ResolveConfiguredPath(string? configuredPath, string defaultRelative)
        {
            var raw = string.IsNullOrWhiteSpace(configuredPath) ? defaultRelative : configuredPath.Trim();
            return Path.IsPathRooted(raw)
                ? Path.GetFullPath(raw)
                : Path.GetFullPath(Path.Combine(_env.ContentRootPath, raw));
        }

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
                // Bỏ qua lỗi dọn tệp nén tạm
            }
        }
    }
}
