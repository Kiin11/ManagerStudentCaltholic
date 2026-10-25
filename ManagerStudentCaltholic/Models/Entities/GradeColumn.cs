using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("grade_columns")]
    public class GradeColumn
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
        public int? ClassId { get; set; } // Nullable: Áp dụng chung hoặc riêng theo lớp

        [Required, MaxLength(50)]
        [Column("column_code")]
        public string ColumnCode { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        [Column("column_name")]
        public string ColumnName { get; set; } = string.Empty;

        [Column("coefficient")]
        public decimal Coefficient { get; set; } = 1.0m;

        [Column("is_attendance_based")]
        public bool IsAttendanceBased { get; set; } = false;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}
