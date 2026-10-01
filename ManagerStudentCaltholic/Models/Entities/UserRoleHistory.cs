using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class UserRoleHistory
    {
        public long Id { get; set; }

        public long UserId { get; set; }
        public User User { get; set; } = null!;

        [Required, MaxLength(30)]
        public string OldRole { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string NewRole { get; set; } = string.Empty;

        public int? AcademicYearId { get; set; }
        public AcademicYear? AcademicYear { get; set; }

        public long? ChangedByUserId { get; set; }
        public User? ChangedByUser { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? Reason { get; set; }
    }
}
