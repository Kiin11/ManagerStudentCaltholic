using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class AbsenceRequest
    {
        public long Id { get; set; }

        public long EnrollmentId { get; set; }
        public Enrollment Enrollment { get; set; } = null!;

        [Required]
        public DateTime AbsenceDate { get; set; }

        // Loại vắng: "MASS" (Thánh lễ), "CLASS" (Giờ học), "ALL" (Cả hai)
        [Required, MaxLength(20)]
        public string SessionType { get; set; } = "ALL";

        [Required, MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ParentPhone { get; set; }

        // Trạng thái: "PENDING", "APPROVED", "REJECTED"
        [Required, MaxLength(30)]
        public string Status { get; set; } = "PENDING";

        [MaxLength(255)]
        public string? ReviewNote { get; set; }

        [MaxLength(100)]
        public string? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}