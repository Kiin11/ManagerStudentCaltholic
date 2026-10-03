using System.ComponentModel.DataAnnotations;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    // DTO nhận dữ liệu lưu điểm lẻ hoặc hàng loạt qua Ajax
    public class SaveGradeItemDto
    {
        public long EnrollmentId { get; set; }
        public int GradeConfigurationId { get; set; }

        [Range(0.00, 10.00, ErrorMessage = "Điểm phải từ 0.0 đến 10.0")]
        public decimal? Score { get; set; }
        public string? Note { get; set; }
    }

    public class SaveBatchGradeRequestDto
    {
        public int ClassId { get; set; }
        public int Semester { get; set; }
        public List<SaveGradeItemDto> Grades { get; set; } = new();
    }

    // ViewModel hiển thị bảng ma trận nhập điểm (Grid View)
    public class ClassGradeMatrixViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string AcademicYearName { get; set; } = string.Empty;
        public int CurrentSemester { get; set; } = 1;
        public bool IsLocked { get; set; }

        // Danh sách cột điểm được cấu hình cho khối/lớp này
        public List<GradeConfiguration> Columns { get; set; } = new();

        // Danh sách hàng dữ liệu học sinh kèm điểm
        public List<StudentGradeRowItem> Rows { get; set; } = new();
    }

    public class StudentGradeRowItem
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        // Điểm theo từng cấu hình: Key = GradeConfigurationId, Value = Score
        public Dictionary<int, decimal?> Scores { get; set; } = new();

        // Điểm trung bình và kết quả xếp loại
        public decimal? SemesterAverage { get; set; }
        public decimal? YearAverage { get; set; }
        public double AttendanceRate { get; set; } // Tỷ lệ chuyên cần (%)
        public string AcademicRank { get; set; } = "Chưa xếp loại";
        public bool IsEligibleForSacrament { get; set; }
    }
}