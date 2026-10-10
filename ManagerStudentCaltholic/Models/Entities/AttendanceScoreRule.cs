using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("attendance_score_rules")]
    public class AttendanceScoreRule
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required, MaxLength(20)]
        [Column("academic_year")]
        public string AcademicYear { get; set; } = string.Empty;

        [Column("semester")]
        public int Semester { get; set; }

        [Column("base_score")]
        public decimal BaseScore { get; set; } = 10.0m;

        [Column("penalty_mass_unexcused")]
        public decimal PenaltyMassUnexcused { get; set; } = 1.0m;

        [Column("penalty_mass_excused")]
        public decimal PenaltyMassExcused { get; set; } = 0.5m;

        [Column("penalty_mass_late")]
        public decimal PenaltyMassLate { get; set; } = 0.25m;

        [Column("penalty_class_unexcused")]
        public decimal PenaltyClassUnexcused { get; set; } = 1.0m;

        [Column("penalty_class_excused")]
        public decimal PenaltyClassExcused { get; set; } = 0.5m;

        [Column("penalty_class_late")]
        public decimal PenaltyClassLate { get; set; } = 0.25m;

        [Column("min_score")]
        public decimal MinScore { get; set; } = 0.0m;
    }
}
