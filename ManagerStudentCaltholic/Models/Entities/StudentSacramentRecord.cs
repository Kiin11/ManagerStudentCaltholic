using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ManagerStudentCaltholic.Interface.Models;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("StudentSacramentRecords")]
    public class StudentSacramentRecord : ISoftDelete
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student Student { get; set; } = null!;

        /// <summary>
        /// Loại Bí tích: 1 = Rửa Tội, 2 = Rước Lễ Lần Đầu, 3 = Thêm Sức, 4 = Bao Đồng
        /// </summary>
        [Required]
        public int SacramentType { get; set; }

        /// <summary>
        /// Ngày lãnh nhận Bí tích (UTC)
        /// </summary>
        [Required]
        [DataType(DataType.Date)]
        public DateTime ReceivedDate { get; set; }

        /// <summary>
        /// Tên Giáo xứ ban Bí tích (Ví dụ: Giáo xứ Phú Trung)
        /// </summary>
        [Required]
        [MaxLength(150)]
        public string ParishName { get; set; } = string.Empty;

        /// <summary>
        /// Tên Linh mục / Giám mục ban Bí tích
        /// </summary>
        [MaxLength(120)]
        public string? PriestName { get; set; }

        /// <summary>
        /// Tên Thánh & Họ tên Người đỡ đầu (Cha/Mẹ thiêng liêng)
        /// </summary>
        [MaxLength(120)]
        public string? GodparentName { get; set; }

        /// <summary>
        /// Số vào Sổ bộ Giáo xứ (Ví dụ: Quyển 03, Trang 45, Số 128/RT)
        /// </summary>
        [MaxLength(100)]
        public string? RegistryBookNumber { get; set; }

        /// <summary>
        /// Ghi chú bổ sung (Chứng nhận chuyển xứ, chuẩn chước...)
        /// </summary>
        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Soft Delete (ISoftDelete)
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}