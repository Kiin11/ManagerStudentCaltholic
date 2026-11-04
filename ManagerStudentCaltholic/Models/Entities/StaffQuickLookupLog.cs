using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("StaffQuickLookupLogs")]
    public class StaffQuickLookupLog
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [Required]
        public long StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student? Student { get; set; }

        [MaxLength(150)]
        public string SearchTerm { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string SelectedOption { get; set; } = "OVERVIEW";

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        public DateTime LookedUpAt { get; set; } = DateTime.UtcNow;
    }
}
