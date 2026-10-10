using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("teacher_classes")]
    public class TeacherClass
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("class_id")]
        public int ClassId { get; set; }

        [MaxLength(50)]
        [Column("role_in_class")]
        public string RoleInClass { get; set; } = "GLV";

        [Required, MaxLength(20)]
        [Column("academic_year")]
        public string AcademicYear { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
