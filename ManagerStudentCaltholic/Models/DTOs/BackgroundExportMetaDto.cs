namespace ManagerStudentCaltholic.Models.DTOs
{
    public class BackgroundExportMetaDto
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = "PROCESSING"; // PROCESSING | COMPLETED | FAILED
        public string Title { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? RequestedBy { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
    }

}
