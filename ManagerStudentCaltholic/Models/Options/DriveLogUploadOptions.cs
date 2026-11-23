namespace ManagerStudentCaltholic.Models.Options
{
    public class DriveLogUploadOptions
    {
        public const string SectionName = "DriveLogUpload";

        public bool Enabled { get; set; } = false;
        public string CronExpression { get; set; } = "0 2 * * *";
        public int IntervalMinutes { get; set; } = 0;
        public string LocalLogsPath { get; set; } = "logs";
        public int LocalRetentionDays { get; set; } = 1;
        public string LogsFolderId { get; set; } = string.Empty;
        public bool DeleteLocalAfterVerifiedUpload { get; set; } = true;
        public int MaxRetriesPerFile { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 10;
        public int UploadBufferSizeBytes { get; set; } = 65536;
    }
}
