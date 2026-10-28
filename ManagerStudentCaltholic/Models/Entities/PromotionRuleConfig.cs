using ManagerStudentCaltholic.Interface.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class PromotionRuleConfig : ISoftDelete
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        /// <summary>
        /// Khối áp dụng: "Khai Tâm", "Rước Lễ", "Thêm Sức", "Bao Đồng" hoặc "ALL"
        /// </summary>
        [Required, MaxLength(50)]
        public string GradeLevel { get; set; } = "ALL";

        /// <summary>
        /// Điểm trung bình cả năm tối thiểu (Mặc định: 5.00)
        /// </summary>
        [Column(TypeName = "decimal(4,2)")]
        [Range(0.00, 10.00)]
        public decimal MinYearAverage { get; set; } = 5.00m;

        /// <summary>
        /// Tỷ lệ chuyên cần tối thiểu % (Mặc định: 80.0%)
        /// </summary>
        [Range(0.0, 100.0)]
        public double MinAttendanceRate { get; set; } = 80.0;

        /// <summary>
        /// Bắt buộc đậu cột điểm khảo hạch Bí tích (IsSacramentExam = true) nếu có
        /// </summary>
        public bool RequireSacramentPassed { get; set; } = true;

        /// <summary>
        /// Điểm tối thiểu của bài khảo hạch Bí tích (Mặc định: 5.00)
        /// </summary>
        [Column(TypeName = "decimal(4,2)")]
        [Range(0.00, 10.00)]
        public decimal MinSacramentScore { get; set; } = 5.00m;

        /// <summary>
        /// Khối kế tiếp khi được lên lớp (VD: "Khai Tâm" -> "Rước Lễ", hoặc giữ nguyên khối nếu lên lớp trong cùng khối)
        /// </summary>
        [MaxLength(50)]
        public string? NextGradeLevel { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Soft Delete (ISoftDelete)
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
