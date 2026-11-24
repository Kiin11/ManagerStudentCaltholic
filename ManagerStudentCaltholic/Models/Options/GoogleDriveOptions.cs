namespace ManagerStudentCaltholic.Models.Options
{
    public class GoogleDriveOptions
    {
        public const string SectionName = "GoogleDrive";

        public bool Enabled { get; set; } = false;
        public string ServiceAccountKeyPath { get; set; } = string.Empty;
        public string OAuthClientId { get; set; } = string.Empty;
        public string OAuthClientSecret { get; set; } = string.Empty;
        public string OAuthRefreshToken { get; set; } = string.Empty;
        public string RootFolderId { get; set; } = string.Empty;
        public string RootFolderName { get; set; } = "QLGL_Storage_Root";
        public string SharedDriveId { get; set; } = string.Empty;
        public string LocalFallbackRoot { get; set; } = "PrivateUploads/cloud_fallback";
        public long MaxIncidentImageBytes { get; set; } = 15728640;
        public long MaxLessonPlanBytes { get; set; } = 26214400;
        public long MaxDocumentBytes { get; set; } = 10485760;
        public long MaxAvatarBytes { get; set; } = 5242880;
        public int UploadChunkSizeBytes { get; set; } = 2097152;
        public int MaxRetryAttempts { get; set; } = 3;
    }
}
