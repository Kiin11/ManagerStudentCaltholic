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

    public class CreateGradeColumnDto
    {
        [Required]
        public int ClassId { get; set; }

        [Range(1, 2)]
        public int Semester { get; set; } = 1;

        [Required(ErrorMessage = "Vui lòng nhập tên cột điểm")]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string ScoreType { get; set; } = "CUSTOM";

        [Range(1, 5)]
        public int WeightFactor { get; set; } = 1;
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

        public Dictionary<int, decimal?> Scores { get; set; } = new();

        public decimal? Semester1Average { get; set; }
        public decimal? Semester2Average { get; set; }
        public decimal? SemesterAverage { get; set; }
        public decimal? YearAverage { get; set; }
        public double AttendanceRate { get; set; }
        public string AcademicRank { get; set; } = "Chưa xếp loại";
        public bool IsEligibleForSacrament { get; set; }
    }
}
