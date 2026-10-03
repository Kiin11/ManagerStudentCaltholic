using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Lưu trữ điểm số chi tiết từng cột của từng học sinh (TASK-1001)
    /// </summary>
    public class GradeRecord
    {
        public long Id { get; set; }

        public long EnrollmentId { get; set; }
        public Enrollment Enrollment { get; set; } = null!;

        public int GradeConfigurationId { get; set; }
        public GradeConfiguration GradeConfiguration { get; set; } = null!;

        // Điểm số: Thang điểm 10 (từ 0.00 đến 10.00), null nếu chưa nhập điểm
        [Column(TypeName = "decimal(4,2)")]
        [Range(0.00, 10.00, ErrorMessage = "Điểm phải nằm trong thang từ 0.0 đến 10.0")]
        public decimal? Score { get; set; }

        // Ghi chú của GLV khi chấm bài
        [MaxLength(255)]
        public string? Note { get; set; }

        // Tài khoản GLV nhập điểm
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}