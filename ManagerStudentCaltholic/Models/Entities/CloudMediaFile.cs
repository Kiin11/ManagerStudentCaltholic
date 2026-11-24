using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ManagerStudentCaltholic.Interface.Models;

namespace ManagerStudentCaltholic.Models.Entities
{
    public static class CloudMediaCategory
    {
        public const string LessonPlan = "LESSON_PLAN";
        public const string IncidentImage = "INCIDENT_IMAGE";
        public const string Avatar = "AVATAR";
        public const string SacramentDoc = "SACRAMENT_DOC";
        public const string AnnouncementDoc = "ANNOUNCEMENT_DOC";
        public const string AbsenceDoc = "ABSENCE_DOC";
        public const string SystemLog = "SYSTEM_LOG";
    }

    public static class CloudStorageProvider
    {
        public const string GoogleDrive = "GOOGLE_DRIVE";
        public const string LocalFallback = "LOCAL_FALLBACK";
    }

    public static class CloudSyncStatus
    {
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string Retry = "RETRY";
        public const string PendingSync = "PENDING_SYNC";
    }

    [Table("CloudMediaFiles")]
    public class CloudMediaFile : ISoftDelete
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(50)]
        public string Category { get; set; } = CloudMediaCategory.LessonPlan;

        [MaxLength(80)]
        public string? EntityName { get; set; }

        public long? EntityId { get; set; }

        [Required, MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string ContentType { get; set; } = "application/octet-stream";

        public long FileSize { get; set; }

        [MaxLength(64)]
        public string? Sha256Hash { get; set; }

        [Required, MaxLength(30)]
        public string StorageProvider { get; set; } = CloudStorageProvider.GoogleDrive;

        [MaxLength(150)]
        public string? ExternalFileId { get; set; }

        [MaxLength(150)]
        public string? ExternalFolderId { get; set; }

        [MaxLength(300)]
        public string? DriveFolderPath { get; set; }

        [MaxLength(500)]
        public string? LocalFallbackPath { get; set; }

        [Required, MaxLength(20)]
        public string SyncStatus { get; set; } = CloudSyncStatus.Success;

        public int RetryCount { get; set; } = 0;

        [MaxLength(1000)]
        public string? LastError { get; set; }

        [MaxLength(100)]
        public string? UploadedBy { get; set; }

        public long? UploadedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? SyncedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }
    }
}
