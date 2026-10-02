using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class BranchHeadAssignment
    {
        public long Id { get; set; }

        public long UserId { get; set; }
        public User User { get; set; } = null!;

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        [Required, MaxLength(50)]
        public string ManagedGradeLevel { get; set; } = string.Empty; // Khai Tâm, Rước Lễ, Thêm Sức, Bao Đồng

        public long? AssignedByUserId { get; set; }
        public User? AssignedByUser { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? Notes { get; set; }
    }
}
