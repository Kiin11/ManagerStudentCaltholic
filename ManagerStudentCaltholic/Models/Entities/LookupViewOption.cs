using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("LookupViewOptions")]
    public class LookupViewOption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string OptionCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string IconClass { get; set; } = "bi-card-list";

        [Required]
        [MaxLength(30)]
        public string BadgeColor { get; set; } = "primary";

        public int SortOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;
    }
}
