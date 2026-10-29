using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ManagerStudentCaltholic.Interface.Models;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("student_registration_requests")]
    public class StudentRegistrationRequest : ISoftDelete
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required, MaxLength(50)]
        [Column("christian_name")]
        public string ChristianName { get; set; } = string.Empty; // Tên Thánh

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
        [DataType(DataType.Date)]
        [Column("date_of_birth")]
        public DateTime DateOfBirth { get; set; }

        [DataType(DataType.Date)]
        [Column("baptism_date")]
        public DateTime? BaptismDate { get; set; }

        // --- CÁC TRƯỜNG MỞ RỘNG TRONG FEAT-P3-02 (TASK-P3-02.1) ---

        [MaxLength(100)]
        [Column("baptism_parish")]
        public string? BaptismParish { get; set; }                // Giáo xứ Rửa tội

        [Required, MaxLength(100)]
        [Column("parent_full_name")]
        public string ParentFullName { get; set; } = string.Empty; // Họ tên Cha/Mẹ hoặc Người giám hộ

        [Required, MaxLength(20)]
        [Column("parent_phone")]
        public string ParentPhone { get; set; } = string.Empty;   // SĐT Phụ huynh liên hệ

        [MaxLength(100)]
        [Column("parent_email")]
        public string? ParentEmail { get; set; }                  // Email phụ huynh (tùy chọn)

        [MaxLength(255)]
        [Column("address")]
        public string? Address { get; set; }                      // Địa chỉ cư ngụ

        [MaxLength(100)]
        [Column("parish_sub_division")]
        public string? ParishSubDivision { get; set; }            // Giáo họ / Khu xóm

        /// <summary>
        /// Phân loại hồ sơ: "NEW" (Học mới hoàn toàn) hoặc "TRANSFER" (Chuyển xứ đến)
        /// </summary>
        [Required, MaxLength(30)]
        [Column("registration_type")]
        public string RegistrationType { get; set; } = "NEW";

        [Column("academic_year_id")]
        public int? AcademicYearId { get; set; }
        public AcademicYear? AcademicYear { get; set; }

        /// <summary>
        /// Khối đăng ký dự kiến: "Khai Tâm", "Rước Lễ", "Thêm Sức", "Bao Đồng"
        /// </summary>
        [Required, MaxLength(50)]
        [Column("target_grade_level")]
        public string TargetGradeLevel { get; set; } = "Khai Tâm";

        [MaxLength(255)]
        [Column("baptism_certificate_path")]
        public string? BaptismCertificatePath { get; set; }       // Tên file lưu trong PrivateUploads/baptism_certs

        /// <summary>
        /// Trạng thái đơn: "PENDING" (Chờ duyệt), "APPROVED" (Đã duyệt), "REJECTED" (Từ chối)
        /// </summary>
        [Required, MaxLength(30)]
        [Column("status")]
        public string Status { get; set; } = "PENDING";

        [MaxLength(500)]
        [Column("rejection_reason")]
        public string? RejectionReason { get; set; }              // Lý do từ chối hoặc yêu cầu bổ sung hồ sơ

        [Column("created_student_id")]
        public long? CreatedStudentId { get; set; }               // Liên kết tới Students.Id sau khi duyệt
        public Student? CreatedStudent { get; set; }

        [Column("assigned_class_id")]
        public int? AssignedClassId { get; set; }                 // Lớp đã xếp sau khi duyệt
        public ClassRoom? AssignedClass { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        [Column("reviewed_by")]
        public string? ReviewedBy { get; set; }

        [Column("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }

        // Soft Delete (ISoftDelete)
        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }
    }
}