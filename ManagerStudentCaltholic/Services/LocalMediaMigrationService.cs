using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class LocalMediaMigrationService : ILocalMediaMigrationService
    {
        private static readonly SemaphoreSlim MigrationLock = new(1, 1);

        private readonly ParishDbContext _context;
        private readonly IGoogleDriveStorageService _storageService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LocalMediaMigrationService> _logger;

        public LocalMediaMigrationService(
            ParishDbContext context,
            IGoogleDriveStorageService storageService,
            IWebHostEnvironment env,
            ILogger<LocalMediaMigrationService> logger)
        {
            _context = context;
            _storageService = storageService;
            _env = env;
            _logger = logger;
        }

        public async Task<LocalMigrationReportDto> RunMigrationAsync(
            bool dryRun = true,
            bool deleteLocalAfterVerified = false,
            string? operatorName = null,
            CancellationToken ct = default)
        {
            var report = new LocalMigrationReportDto
            {
                DryRun = dryRun,
                DeleteLocalAfterVerified = deleteLocalAfterVerified
            };

            if (!await MigrationLock.WaitAsync(0, ct))
            {
                report.Details.Add("Đang có một tiến trình di chuyển dữ liệu lên Google Drive khác đang thực thi.");
                return report;
            }

            var effectiveOperator = string.IsNullOrWhiteSpace(operatorName)
                ? "MIGRATION"
                : Truncate(operatorName.Trim(), 100)!;

            try
            {
                // STEP A: Retry all pending LOCAL_FALLBACK rows
                await ExecuteStepAAsync(dryRun, report, ct);

                // STEP B: Migrate ClassLessonDocuments where CloudFileId == null
                await ExecuteStepBAsync(dryRun, deleteLocalAfterVerified, effectiveOperator, report, ct);

                // STEP C: Migrate ClassCurriculumVersions where CloudFileId == null
                await ExecuteStepCAsync(dryRun, deleteLocalAfterVerified, effectiveOperator, report, ct);

                // STEP D: Scan/Migrate orphan files in wwwroot/uploads/lesson_plans
                await ExecuteStepDAsync(dryRun, deleteLocalAfterVerified, effectiveOperator, report, ct);

                if (report.Details.Count == 0)
                {
                    report.Details.Add("Không có tệp cục bộ hoặc bản ghi fallback nào cần di chuyển.");
                }
            }
            finally
            {
                MigrationLock.Release();
            }

            return report;
        }

        private async Task ExecuteStepAAsync(
            bool dryRun,
            LocalMigrationReportDto report,
            CancellationToken ct)
        {
            var pendingFallbacks = await _context.CloudMediaFiles
                .AsNoTracking()
                .Where(f => f.StorageProvider == CloudStorageProvider.LocalFallback
                    && (f.SyncStatus == CloudSyncStatus.Retry || f.SyncStatus == CloudSyncStatus.PendingSync))
                .OrderBy(f => f.CreatedAt)
                .Select(f => new { f.Id, f.Category, f.OriginalFileName, f.FileSize })
                .ToListAsync(ct);

            report.ScannedFiles += pendingFallbacks.Count;

            if (pendingFallbacks.Count == 0)
            {
                report.Details.Add("[Step A] Không có tệp LOCAL_FALLBACK đang chờ đồng bộ.");
                return;
            }

            if (dryRun)
            {
                report.Details.Add($"[Step A - DryRun] Phát hiện {pendingFallbacks.Count} tệp LOCAL_FALLBACK đang chờ đồng bộ lại lên Drive.");
                foreach (var item in pendingFallbacks.Take(25))
                {
                    report.Details.Add($"  • Fallback #{item.Id} [{item.Category}] {item.OriginalFileName} ({item.FileSize:N0} bytes)");
                }
                if (pendingFallbacks.Count > 25)
                {
                    report.Details.Add($"  • ... và {pendingFallbacks.Count - 25} tệp khác.");
                }
                return;
            }

            var pendingIds = pendingFallbacks.Select(f => f.Id).ToList();
            int syncedCount = await _storageService.RetryAllPendingFallbackFilesAsync(ct);
            report.FallbackRetriedCount = syncedCount;

            int failedFallbacks = Math.Max(0, pendingFallbacks.Count - syncedCount);
            report.FailedCount += failedFallbacks;

            if (syncedCount > 0)
            {
                long freedFallbackBytes = await _context.CloudMediaFiles
                    .AsNoTracking()
                    .Where(f => pendingIds.Contains(f.Id)
                        && f.StorageProvider == CloudStorageProvider.GoogleDrive
                        && f.SyncStatus == CloudSyncStatus.Success)
                    .SumAsync(f => (long?)f.FileSize ?? 0L, ct);
                report.FreedBytes += freedFallbackBytes;
            }

            report.Details.Add($"[Step A] Đã đồng bộ lại {syncedCount}/{pendingFallbacks.Count} tệp LOCAL_FALLBACK lên Google Drive.");
        }

        private async Task ExecuteStepBAsync(
            bool dryRun,
            bool deleteLocalAfterVerified,
            string operatorName,
            LocalMigrationReportDto report,
            CancellationToken ct)
        {
            var privateLessonPlanRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "PrivateUploads", "lesson_plans"));
            var legacyLessonPlanRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "lesson_plans"));

            var pendingDocs = await _context.ClassLessonDocuments
                .Include(d => d.AcademicYear)
                .Where(d => d.CloudFileId == null)
                .OrderBy(d => d.Id)
                .ToListAsync(ct);

            foreach (var doc in pendingDocs)
            {
                ct.ThrowIfCancellationRequested();

                string? localPath = null;
                if (TryResolveContainedPath(privateLessonPlanRoot, doc.StoredFileName, out var privatePath)
                    && File.Exists(privatePath))
                {
                    localPath = privatePath;
                }
                else if (TryResolveContainedPath(legacyLessonPlanRoot, doc.StoredFileName, out var legacyPath)
                    && File.Exists(legacyPath))
                {
                    localPath = legacyPath;
                }

                if (localPath == null)
                {
                    report.SkippedCount++;
                    report.Details.Add($"[Step B] Bỏ qua ClassLessonDocument#{doc.Id} ({doc.FileName}): không tìm thấy tệp cục bộ '{doc.StoredFileName}'.");
                    continue;
                }

                var fileInfo = new FileInfo(localPath);
                report.ScannedFiles++;

                string yearName = string.IsNullOrWhiteSpace(doc.AcademicYear?.Name)
                    ? "UnknownYear"
                    : doc.AcademicYear.Name.Trim();

                if (dryRun)
                {
                    report.Details.Add($"[Step B - DryRun] Sẽ di chuyển ClassLessonDocument#{doc.Id} '{doc.FileName}' ({fileInfo.Length:N0} bytes) -> [{yearName}/LessonPlans].");
                    continue;
                }

                try
                {
                    var originalName = string.IsNullOrWhiteSpace(doc.FileName)
                        ? Path.GetFileName(localPath)
                        : doc.FileName;
                    var contentType = string.IsNullOrWhiteSpace(doc.ContentType)
                        ? ResolveContentType(originalName)
                        : doc.ContentType;

                    CloudMediaFile media;
                    await using (var fileStream = new FileStream(
                        localPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 65536,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        media = await _storageService.UploadAsync(new CloudUploadRequest
                        {
                            Content = fileStream,
                            OriginalFileName = originalName,
                            ContentType = contentType,
                            Category = CloudMediaCategory.LessonPlan,
                            EntityName = nameof(ClassLessonDocument),
                            EntityId = doc.Id,
                            FolderSegments = new[] { yearName, "LessonPlans" },
                            UploadedBy = string.IsNullOrWhiteSpace(doc.UploadedBy) ? operatorName : doc.UploadedBy,
                            AllowLocalFallback = false
                        }, ct);
                    }

                    await CleanupSupersededFailedMediaAsync(
                        CloudMediaCategory.LessonPlan,
                        nameof(ClassLessonDocument),
                        doc.Id,
                        media.Id,
                        ct);

                    bool verified = IsVerifiedDriveUpload(media);
                    if (verified)
                    {
                        doc.CloudFileId = media.Id;
                        if (string.IsNullOrWhiteSpace(doc.FilePath)
                            || doc.FilePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                        {
                            doc.FilePath = $"/LessonPlans/DownloadDocument/{doc.Id}";
                        }

                        await _context.SaveChangesAsync(ct);
                        report.MigratedCount++;

                        string deleteNote = string.Empty;
                        if (deleteLocalAfterVerified)
                        {
                            if (TryDeleteLocalFile(localPath, out var deleteErr))
                            {
                                report.FreedBytes += fileInfo.Length;
                                deleteNote = " (đã xóa file gốc)";
                            }
                            else
                            {
                                deleteNote = $" (không xóa được file gốc: {deleteErr})";
                            }
                        }

                        report.Details.Add($"[Step B] Đã di chuyển ClassLessonDocument#{doc.Id} '{doc.FileName}' -> CloudMedia#{media.Id}{deleteNote}.");
                    }
                    else
                    {
                        await _context.SaveChangesAsync(ct);
                        report.FailedCount++;
                        report.Details.Add($"[Step B] Thất bại ClassLessonDocument#{doc.Id} '{doc.FileName}': {media.LastError}");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi di chuyển ClassLessonDocument#{DocId} lên Google Drive.", doc.Id);
                    report.FailedCount++;
                    report.Details.Add($"[Step B] Lỗi ngoại lệ ClassLessonDocument#{doc.Id}: {ex.Message}");
                }
            }
        }

        private async Task ExecuteStepCAsync(
            bool dryRun,
            bool deleteLocalAfterVerified,
            string operatorName,
            LocalMigrationReportDto report,
            CancellationToken ct)
        {
            var privateCurriculumRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "PrivateUploads", "curriculums"));

            var pendingVersions = await _context.ClassCurriculumVersions
                .Include(v => v.CurriculumDocument)
                    .ThenInclude(d => d.AcademicYear)
                .Where(v => v.CloudFileId == null)
                .OrderBy(v => v.Id)
                .ToListAsync(ct);

            foreach (var ver in pendingVersions)
            {
                ct.ThrowIfCancellationRequested();

                if (!TryResolveContainedPath(privateCurriculumRoot, ver.StoredFileName, out var localPath)
                    || !File.Exists(localPath))
                {
                    report.SkippedCount++;
                    report.Details.Add($"[Step C] Bỏ qua ClassCurriculumVersion#{ver.Id} ({ver.FileName}): không tìm thấy tệp cục bộ '{ver.StoredFileName}'.");
                    continue;
                }

                var fileInfo = new FileInfo(localPath);
                report.ScannedFiles++;

                string yearName = string.IsNullOrWhiteSpace(ver.CurriculumDocument?.AcademicYear?.Name)
                    ? "UnknownYear"
                    : ver.CurriculumDocument.AcademicYear.Name.Trim();

                if (dryRun)
                {
                    report.Details.Add($"[Step C - DryRun] Sẽ di chuyển ClassCurriculumVersion#{ver.Id} '{ver.FileName}' ({fileInfo.Length:N0} bytes) -> [{yearName}/LessonPlans].");
                    continue;
                }

                try
                {
                    var originalName = string.IsNullOrWhiteSpace(ver.FileName)
                        ? Path.GetFileName(localPath)
                        : ver.FileName;
                    var contentType = string.IsNullOrWhiteSpace(ver.ContentType)
                        ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                        : ver.ContentType;

                    CloudMediaFile media;
                    await using (var fileStream = new FileStream(
                        localPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 65536,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        media = await _storageService.UploadAsync(new CloudUploadRequest
                        {
                            Content = fileStream,
                            OriginalFileName = originalName,
                            ContentType = contentType,
                            Category = CloudMediaCategory.LessonPlan,
                            EntityName = nameof(ClassCurriculumVersion),
                            EntityId = ver.Id,
                            FolderSegments = new[] { yearName, "LessonPlans" },
                            UploadedBy = string.IsNullOrWhiteSpace(ver.CreatedBy) ? operatorName : ver.CreatedBy,
                            UploadedByUserId = ver.CreatedByUserId,
                            AllowLocalFallback = false
                        }, ct);
                    }

                    await CleanupSupersededFailedMediaAsync(
                        CloudMediaCategory.LessonPlan,
                        nameof(ClassCurriculumVersion),
                        ver.Id,
                        media.Id,
                        ct);

                    bool verified = IsVerifiedDriveUpload(media);
                    if (verified)
                    {
                        ver.CloudFileId = media.Id;

                        if (ver.CurriculumDocument != null
                            && (ver.VersionNumber == ver.CurriculumDocument.VersionNumber
                                || string.Equals(ver.VersionTag, ver.CurriculumDocument.CurrentVersion, StringComparison.OrdinalIgnoreCase)))
                        {
                            ver.CurriculumDocument.CloudFileId = media.Id;
                        }

                        await _context.SaveChangesAsync(ct);
                        report.MigratedCount++;

                        string deleteNote = string.Empty;
                        if (deleteLocalAfterVerified)
                        {
                            if (TryDeleteLocalFile(localPath, out var deleteErr))
                            {
                                report.FreedBytes += fileInfo.Length;
                                deleteNote = " (đã xóa file gốc)";
                            }
                            else
                            {
                                deleteNote = $" (không xóa được file gốc: {deleteErr})";
                            }
                        }

                        report.Details.Add($"[Step C] Đã di chuyển ClassCurriculumVersion#{ver.Id} '{ver.FileName}' -> CloudMedia#{media.Id}{deleteNote}.");
                    }
                    else
                    {
                        await _context.SaveChangesAsync(ct);
                        report.FailedCount++;
                        report.Details.Add($"[Step C] Thất bại ClassCurriculumVersion#{ver.Id} '{ver.FileName}': {media.LastError}");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi di chuyển ClassCurriculumVersion#{VerId} lên Google Drive.", ver.Id);
                    report.FailedCount++;
                    report.Details.Add($"[Step C] Lỗi ngoại lệ ClassCurriculumVersion#{ver.Id}: {ex.Message}");
                }
            }
        }

        private async Task ExecuteStepDAsync(
            bool dryRun,
            bool deleteLocalAfterVerified,
            string operatorName,
            LocalMigrationReportDto report,
            CancellationToken ct)
        {
            var legacyOrphanDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "lesson_plans"));
            if (!Directory.Exists(legacyOrphanDir))
            {
                return;
            }

            var referencedFileNames = (await _context.ClassLessonDocuments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(d => d.StoredFileName)
                .ToListAsync(ct))
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

            var alreadyMigratedOrphanNames = (await _context.CloudMediaFiles
                .AsNoTracking()
                .Where(f => f.Category == CloudMediaCategory.LessonPlan
                    && f.EntityName == "LegacyOrphan"
                    && f.StorageProvider == CloudStorageProvider.GoogleDrive
                    && f.SyncStatus == CloudSyncStatus.Success)
                .Select(f => f.OriginalFileName)
                .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var diskFiles = Directory.EnumerateFiles(legacyOrphanDir, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var filePath in diskFiles)
            {
                ct.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(filePath);
                if (string.IsNullOrWhiteSpace(fileName) || referencedFileNames.Contains(fileName))
                {
                    continue;
                }

                if (alreadyMigratedOrphanNames.Contains(fileName))
                {
                    report.SkippedCount++;
                    report.Details.Add($"[Step D] Bỏ qua tệp mồ côi '{fileName}': đã được đồng bộ lên Legacy_Orphans trước đó.");
                    continue;
                }

                var fileInfo = new FileInfo(filePath);
                report.ScannedFiles++;

                if (dryRun)
                {
                    report.Details.Add($"[Step D - DryRun] Phát hiện tệp mồ côi '{fileName}' ({fileInfo.Length:N0} bytes) trong wwwroot/uploads/lesson_plans -> [Legacy_Orphans/lesson_plans].");
                    continue;
                }

                try
                {
                    CloudMediaFile media;
                    await using (var fileStream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 65536,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        media = await _storageService.UploadAsync(new CloudUploadRequest
                        {
                            Content = fileStream,
                            OriginalFileName = fileName,
                            ContentType = ResolveContentType(fileName),
                            Category = CloudMediaCategory.LessonPlan,
                            EntityName = "LegacyOrphan",
                            EntityId = null,
                            FolderSegments = new[] { "Legacy_Orphans", "lesson_plans" },
                            UploadedBy = operatorName,
                            AllowLocalFallback = false
                        }, ct);
                    }

                    var priorOrphanFailures = await _context.CloudMediaFiles
                        .Where(f => f.Category == CloudMediaCategory.LessonPlan
                            && f.EntityName == "LegacyOrphan"
                            && f.OriginalFileName == fileName
                            && f.Id != media.Id
                            && f.SyncStatus != CloudSyncStatus.Success)
                        .ToListAsync(ct);

                    if (priorOrphanFailures.Count > 0)
                    {
                        _context.CloudMediaFiles.RemoveRange(priorOrphanFailures);
                        await _context.SaveChangesAsync(ct);
                    }

                    bool verified = IsVerifiedDriveUpload(media);
                    if (verified)
                    {
                        report.MigratedCount++;
                        string deleteNote = string.Empty;
                        if (deleteLocalAfterVerified)
                        {
                            if (TryDeleteLocalFile(filePath, out var deleteErr))
                            {
                                report.FreedBytes += fileInfo.Length;
                                deleteNote = " (đã xóa file gốc)";
                            }
                            else
                            {
                                deleteNote = $" (không xóa được file gốc: {deleteErr})";
                            }
                        }

                        report.Details.Add($"[Step D] Đã di chuyển tệp mồ côi '{fileName}' -> CloudMedia#{media.Id} [Legacy_Orphans/lesson_plans]{deleteNote}.");
                    }
                    else
                    {
                        report.FailedCount++;
                        report.Details.Add($"[Step D] Thất bại tệp mồ côi '{fileName}': {media.LastError}");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi di chuyển tệp mồ côi {FileName} lên Google Drive.", fileName);
                    report.FailedCount++;
                    report.Details.Add($"[Step D] Lỗi ngoại lệ '{fileName}': {ex.Message}");
                }
            }
        }

        private async Task CleanupSupersededFailedMediaAsync(
            string category,
            string entityName,
            long entityId,
            long keepMediaId,
            CancellationToken ct)
        {
            var priorFailed = await _context.CloudMediaFiles
                .Where(f => f.Category == category
                    && f.EntityName == entityName
                    && f.EntityId == entityId
                    && f.Id != keepMediaId
                    && f.SyncStatus != CloudSyncStatus.Success)
                .ToListAsync(ct);

            if (priorFailed.Count > 0)
            {
                _context.CloudMediaFiles.RemoveRange(priorFailed);
            }
        }

        private static bool IsVerifiedDriveUpload(CloudMediaFile media) =>
            string.Equals(media.StorageProvider, CloudStorageProvider.GoogleDrive, StringComparison.OrdinalIgnoreCase)
            && string.Equals(media.SyncStatus, CloudSyncStatus.Success, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(media.ExternalFileId);

        private static bool TryResolveContainedPath(string rootDirectory, string? storedFileName, out string fullPath)
        {
            fullPath = string.Empty;
            var safeName = Path.GetFileName((storedFileName ?? string.Empty).Trim());
            if (string.IsNullOrWhiteSpace(safeName))
            {
                return false;
            }

            var rootFull = Path.GetFullPath(rootDirectory);
            var normalizedRoot = rootFull.EndsWith(Path.DirectorySeparatorChar)
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;

            var candidate = Path.GetFullPath(Path.Combine(rootFull, safeName));
            if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }

        private static bool TryDeleteLocalFile(string path, out string? errorMessage)
        {
            errorMessage = null;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static string ResolveContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".csv" => "text/csv",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (value == null)
            {
                return null;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
