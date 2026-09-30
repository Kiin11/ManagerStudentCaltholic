namespace ManagerStudentCaltholic.Models.DTOs
{
    public class AttendanceHistoryDto
    {
        public long StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // CheckInQR, ManualUpdate, MakeupApproval
        public string Description { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string TimeDisplay => Timestamp.ToString("HH:mm - dd/MM/yyyy");
    }
}
