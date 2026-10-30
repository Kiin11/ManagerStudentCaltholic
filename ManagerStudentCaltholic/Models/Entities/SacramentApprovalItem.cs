using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("SacramentApprovalItems")]
    public class SacramentApprovalItem
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long BatchId { get; set; }

        [ForeignKey(nameof(BatchId))]
        public SacramentApprovalBatch? Batch { get; set; }

        [Required]
        public long StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student? Student { get; set; }

        [Required]
        public long EnrollmentId { get; set; }

        [ForeignKey(nameof(EnrollmentId))]
        public Enrollment? Enrollment { get; set; }

        [MaxLength(50)]
        public string ClassName { get; set; } = string.Empty;

        [Column(TypeName = "numeric(4,2)")]
        public decimal? Semester1Average { get; set; }

        [Column(TypeName = "numeric(4,2)")]
        public decimal? Semester2Average { get; set; }

        [Column(TypeName = "numeric(4,2)")]
        public decimal? YearAverage { get; set; }

        [Column(TypeName = "numeric(4,2)")]
        public decimal? SacramentExamScore { get; set; }

        public double MassAttendanceRate { get; set; }
        public double ClassAttendanceRate { get; set; }
        public double CombinedAttendanceRate { get; set; }

        /// <summary>
        /// Đạt tự động theo công thức chuẩn
        /// </summary>
        public bool IsSystemEligible { get; set; }

        /// <summary>
        /// Được xét đặc cách / chuẩn chước bởi Cha Xứ hoặc BĐH
        /// </summary>
        public bool IsSpecialExemption { get; set; }

        /// <summary>
        /// Quyết định cuối cùng có được nhận Bí tích trong đợt này hay không
        /// </summary>
        public bool IsApprovedForSacrament { get; set; }

        [MaxLength(120)]
        public string? GodparentName { get; set; }

        [MaxLength(100)]
        public string? AssignedRegistryNumber { get; set; }

        [MaxLength(300)]
        public string? EvaluationNote { get; set; }
    }
}