using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ManagerStudentCaltholic.Interface.Models;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("SacramentApprovalBatches")]
    public class SacramentApprovalBatch : ISoftDelete
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int AcademicYearId { get; set; }

        [ForeignKey(nameof(AcademicYearId))]
        public AcademicYear? AcademicYear { get; set; }

        [Required]
        [MaxLength(50)]
        public string GradeLevel { get; set; } = string.Empty;

        public int? ClassRoomId { get; set; }

        [ForeignKey(nameof(ClassRoomId))]
        public ClassRoom? ClassRoom { get; set; }

        /// <summary>
        /// 1: Rửa Tội, 2: Rước Lễ Lần Đầu, 3: Thêm Sức, 4: Bao Đồng
        /// </summary>
        [Required]
        public int SacramentType { get; set; } = 2;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Column(TypeName = "numeric(4,2)")]
        public decimal MinYearAverage { get; set; } = 5.00m;

        public double MinAttendanceRate { get; set; } = 80.0;

        public bool RequireSacramentExam { get; set; } = true;

        [Column(TypeName = "numeric(4,2)")]
        public decimal MinSacramentExamScore { get; set; } = 5.00m;

        public DateTime PlannedCeremonyDate { get; set; } = DateTime.UtcNow.Date;

        [MaxLength(150)]
        public string CeremonyParish { get; set; } = "Giáo xứ Phú Trung";

        [MaxLength(120)]
        public string? CelebrantPriest { get; set; }

        [MaxLength(50)]
        public string? RegistryBookPrefix { get; set; }

        /// <summary>
        /// DRAFT: Bản nháp chờ trình ký | APPROVED: Đã được Cha Xứ duyệt & đẩy vào Sổ bộ
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "DRAFT";

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string CreatedBy { get; set; } = "Ban Điều Hành";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public ICollection<SacramentApprovalItem> Items { get; set; } = new List<SacramentApprovalItem>();
    }
}