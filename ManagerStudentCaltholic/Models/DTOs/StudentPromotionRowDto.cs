namespace ManagerStudentCaltholic.Models.DTOs
{
    public class StudentPromotionRowDto
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }

        public decimal? HK1Average { get; set; }
        public decimal? HK2Average { get; set; }
        public decimal? YearAverage { get; set; }
        public double AttendanceRate { get; set; }
        public string AcademicRank { get; set; } = "Chưa xếp loại";

        // Tạm để false cho tới khi có quy tắc Bí tích thật sự
        public bool IsEligibleForSacrament { get; set; } = false;

        // Kết quả xét lên lớp (True = Lên lớp, False = Ở lại lớp)
        public bool IsPromoted { get; set; }
        public string PromotionNote { get; set; } = string.Empty;

        // Đánh dấu если học sinh này đã được kết chuyển sang niên khóa đích rồi
        public bool AlreadyEnrolledInTargetYear { get; set; }
        public string? ExistingTargetClassName { get; set; }
    }
}
