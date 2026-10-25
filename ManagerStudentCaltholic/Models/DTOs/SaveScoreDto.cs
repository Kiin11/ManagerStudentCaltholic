namespace ManagerStudentCaltholic.Models.DTOs
{
    public class SaveScoreDto
    {
        public int ClassId { get; set; } // Đã bổ sung để khớp với Controller & UI
        public string AcademicYear { get; set; } = string.Empty; // Đã bổ sung
        public int StudentId { get; set; }
        public int GradeColumnId { get; set; }
        public int Semester { get; set; }
        public decimal? Score { get; set; }
    }
}
