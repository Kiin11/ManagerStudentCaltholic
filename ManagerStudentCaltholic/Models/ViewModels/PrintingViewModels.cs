namespace ManagerStudentCaltholic.Models.ViewModels
{
    // DTO tạo đơn xin phép vắng
    public class SubmitAbsenceRequestDto
    {
        public string StudentCode { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public DateTime AbsenceDate { get; set; }
        public string SessionType { get; set; } = "ALL";
        public string Reason { get; set; } = string.Empty;
        public string? ParentPhone { get; set; }
    }

    // DTO duyệt đơn
    public class ReviewAbsenceRequestDto
    {
        public long RequestId { get; set; }
        public bool IsApproved { get; set; }
        public string? Note { get; set; }
    }

    // ViewModel In Phiếu Liên Lạc / Bằng Khen / Chứng Chỉ
    public class BatchPrintViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string AcademicYearName { get; set; } = string.Empty;
        public string SpiritualDirectorName { get; set; } = "Cha Tuyên Úy";
        public List<PrintStudentItemViewModel> Students { get; set; } = new();
    }

    public class PrintStudentItemViewModel
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = "Nam";

        // Điểm & Xếp loại
        public decimal? Semester1Average { get; set; }
        public decimal? Semester2Average { get; set; }
        public decimal? YearAverage { get; set; }
        public string AcademicRank { get; set; } = "Chưa xếp loại";
        public double AttendanceRate { get; set; }
        public bool IsEligibleForSacrament { get; set; }
        public string TeacherComment { get; set; } = "Chăm ngoan, tích cực tham dự Thánh Lễ và học hỏi Lời Chúa.";
    }
}