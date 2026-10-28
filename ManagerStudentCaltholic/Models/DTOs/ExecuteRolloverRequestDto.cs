using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.DTOs
{
    public class ExecuteRolloverRequestDto
    {
        [Required]
        public int SourceAcademicYearId { get; set; }

        [Required]
        public int SourceClassId { get; set; }

        [Required]
        public int TargetAcademicYearId { get; set; }

        /// <summary>
        /// Nếu bật true, các học sinh không chỉ định lớp đích cụ thể sẽ được chia đều tự động (Smart Balancing)
        /// </summary>
        public bool UseSmartBalancing { get; set; } = true;

        [Required]
        public List<StudentRolloverDecisionDto> Decisions { get; set; } = new();
    }

    public class StudentRolloverDecisionDto
    {
        [Required]
        public long EnrollmentId { get; set; }

        [Required]
        public long StudentId { get; set; }

        /// <summary>
        /// "PROMOTE" (Lên lớp), "RETAIN" (Ở lại lớp), "SKIP" (Nghỉ học/Chuyển xứ), "GRADUATE" (Hoàn thành chương trình)
        /// </summary>
        [Required, MaxLength(20)]
        public string Action { get; set; } = "PROMOTE";

        /// <summary>
        /// Lớp đích tại niên khóa mới (Null nếu để thuật toán SmartClassBalancing tự phân bổ hoặc chọn SKIP/GRADUATE)
        /// </summary>
        public int? TargetClassId { get; set; }

        [MaxLength(255)]
        public string? Note { get; set; }
    }

    public class SavePromotionRuleDto
    {
        [Required]
        public int AcademicYearId { get; set; }

        [Required, MaxLength(50)]
        public string GradeLevel { get; set; } = "ALL";

        [Range(0.0, 10.0)]
        public decimal MinYearAverage { get; set; } = 5.0m;

        [Range(0.0, 100.0)]
        public double MinAttendanceRate { get; set; } = 80.0;

        public bool RequireSacramentPassed { get; set; } = true;

        [Range(0.0, 10.0)]
        public decimal MinSacramentScore { get; set; } = 5.0m;

        [MaxLength(50)]
        public string? NextGradeLevel { get; set; }
    }
}