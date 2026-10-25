using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("grading_formulas")]
    public class GradingFormula
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required, MaxLength(20)]
        [Column("academic_year")]
        public string AcademicYear { get; set; } = string.Empty;

        [Column("semester")]
        public int Semester { get; set; }

        [Column("class_id")]
        public int? ClassId { get; set; }

        [Required]
        [Column("formula_expression")]
        public string FormulaExpression { get; set; } = string.Empty;

        [Column("evaluation_rules", TypeName = "jsonb")]
        public string EvaluationRules { get; set; } = "{}";
    }

    [Table("student_grade_summaries")]
    public class StudentGradeSummary
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("student_id")]
        public int StudentId { get; set; }

        [Required, MaxLength(20)]
        [Column("academic_year")]
        public string AcademicYear { get; set; } = string.Empty;

        [Column("semester")]
        public int Semester { get; set; }

        [Column("average_score")]
        public decimal? AverageScore { get; set; }

        [MaxLength(50)]
        [Column("classification")]
        public string? Classification { get; set; }

        [Column("is_passed")]
        public bool IsPassed { get; set; } = true;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
