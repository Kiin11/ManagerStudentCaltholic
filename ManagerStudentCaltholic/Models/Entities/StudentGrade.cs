using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("student_grades")]
    public class StudentGrade
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("student_id")]
        public int StudentId { get; set; }

        [Column("grade_column_id")]
        public int GradeColumnId { get; set; }
        public virtual GradeColumn GradeColumn { get; set; } = null!;

        [Column("semester")]
        public int Semester { get; set; }

        [Column("score")]
        public decimal? Score { get; set; }

        [Column("note")]
        public string? Note { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
