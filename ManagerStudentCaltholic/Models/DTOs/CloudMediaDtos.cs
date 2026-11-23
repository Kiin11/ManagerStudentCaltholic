namespace ManagerStudentCaltholic.Models.DTOs
{
    public class CloudUploadRequest
    {
        public Stream Content { get; set; } = Stream.Null;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public string Category { get; set; } = string.Empty;
        public string? EntityName { get; set; }
        public long? EntityId { get; set; }
        public IReadOnlyList<string> FolderSegments { get; set; } = Array.Empty<string>();
        public string? ExplicitParentFolderId { get; set; }
        public string? UploadedBy { get; set; }
        public long? UploadedByUserId { get; set; }
        public bool AllowLocalFallback { get; set; } = true;
    }

    public class CloudDownloadResult
    {
        public Stream ContentStream { get; set; } = Stream.Null;
        public string ContentType { get; set; } = "application/octet-stream";
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string StorageProvider { get; set; } = string.Empty;
    }

    public class CloudMediaSummaryDto
    {
        public long Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? EntityName { get; set; }
        public long? EntityId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string StorageProvider { get; set; } = string.Empty;
        public string? ExternalFileId { get; set; }
        public string? DriveFolderPath { get; set; }
        public string SyncStatus { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SyncedAt { get; set; }
        public string ProxyUrl { get; set; } = string.Empty;
    }

    public class CloudStorageHealthDto
    {
        public bool IsDriveEnabled { get; set; }
        public bool IsServiceAccountKeyFound { get; set; }
        public bool IsRootFolderConfigured { get; set; }
        public string? RootFolderName { get; set; }
        public int TotalFilesCount { get; set; }
        public int DriveSyncedCount { get; set; }
        public int LocalFallbackPendingCount { get; set; }
        public int FailedCount { get; set; }
        public Dictionary<string, int> CountByCategory { get; set; } = new();
    }

    public class DriveLogUploadRunResultDto
    {
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
        public string TriggerSource { get; set; } = string.Empty;
        public int ScannedFiles { get; set; }
        public int UploadedCount { get; set; }
        public int DeletedLocalCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedActiveCount { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    public class LocalMigrationReportDto
    {
        public bool DryRun { get; set; }
        public bool DeleteLocalAfterVerified { get; set; }
        public int ScannedFiles { get; set; }
        public int MigratedCount { get; set; }
        public int FallbackRetriedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public long FreedBytes { get; set; }
        public List<string> Details { get; set; } = new();
    }
}
