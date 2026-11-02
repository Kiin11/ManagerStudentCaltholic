using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("StudentCodeHistories")]
    public class StudentCodeHistory
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student? Student { get; set; }

        [Required]
        public int SourceAcademicYearId { get; set; }

        [ForeignKey(nameof(SourceAcademicYearId))]
        public AcademicYear? SourceAcademicYear { get; set; }

        public int? TargetAcademicYearId { get; set; }

        /// <summary>
        /// Mã gốc của học sinh trong năm học cũ trước khi kết chuyển (VD: TS2A001)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string OriginalCode { get; set; } = string.Empty;

        /// <summary>
        /// Mã đã gắn đuôi năm học cũ (VD: TS2A001_2526)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ArchivedYearCode { get; set; } = string.Empty;

        /// <summary>
        /// Mã chính thức sau khi kết chuyển sang năm học mới (VD: TS2A001_2526 hoặc TS3A001)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string NewCode { get; set; } = string.Empty;

        /// <summary>
        /// PROMOTE: Lên lớp | RETAIN: Ở lại lớp | GRADUATE: Tốt nghiệp | SKIP: Nghỉ học/Chuyển xứ
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string RolloverOutcome { get; set; } = "PROMOTE";

        [MaxLength(100)]
        public string? SourceClassName { get; set; }

        [MaxLength(100)]
        public string? TargetClassName { get; set; }

        [Required]
        [MaxLength(100)]
        public string ChangedBy { get; set; } = string.Empty;

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}