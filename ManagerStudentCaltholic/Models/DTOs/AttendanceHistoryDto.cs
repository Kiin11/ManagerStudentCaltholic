namespace ManagerStudentCaltholic.Models.DTOs
{
    public class AttendanceHistoryDto
    {
        public long StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // QUICK_UPDATE_*, BATCH_SAVE, QR_SCAN, QR_UNDO, MAKE_UP, DISPUTE_APPROVED, EXCUSE_APPROVED, OFFLINE_*_SYNC, AUTO_ABSENT
        public string ActionLabel { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime Timestamp { get; set; }
        public string TimeDisplay
        {
            get
            {
                var utc = Timestamp.Kind == DateTimeKind.Local ? Timestamp.ToUniversalTime() : Timestamp;
                return utc.AddHours(7).ToString("HH:mm:ss - dd/MM/yyyy");
            }
        }
    }
}
