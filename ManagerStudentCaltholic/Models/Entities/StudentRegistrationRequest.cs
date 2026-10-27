using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("student_registration_requests")]
    public class StudentRegistrationRequest
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required, MaxLength(50)]
        [Column("christian_name")]
        public string ChristianName { get; set; } = string.Empty; // Tên thánh

        [Required, MaxLength(50)]
        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;     // Họ và tên đệm

        [Required, MaxLength(20)]
        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;      // Tên

        [Required, MaxLength(10)]
        [Column("gender")]
        public string Gender { get; set; } = "Nam";               // Nam / Nữ

        [Required]
        [Column("date_of_birth")]
        public DateTime DateOfBirth { get; set; }

        [DataType(DataType.Date)]
        [Column("baptism_date")]
        public DateTime? BaptismDate { get; set; }

        [Required, MaxLength(20)]
        [Column("parent_phone")]
        public string ParentPhone { get; set; } = string.Empty;   // SĐT Phụ huynh liên hệ

        [MaxLength(255)]
        [Column("baptism_certificate_path")]
        public string? BaptismCertificatePath { get; set; }       // Đường dẫn file ảnh giấy Rửa tội đã upload

        [Required, MaxLength(30)]
        [Column("status")]
        public string Status { get; set; } = "PENDING";           // PENDING (Chờ duyệt), APPROVED (Đã duyệt), REJECTED (Từ chối)

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        [Column("reviewed_by")]
        public string? ReviewedBy { get; set; }

        [Column("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }
    }
}