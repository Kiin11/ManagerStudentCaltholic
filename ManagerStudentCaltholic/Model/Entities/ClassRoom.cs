using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Model.Entities
{
    public class ClassRoom
    {
        public int Id { get; set; }

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty; // Ví dụ: "Xưng Tội 1A"

        [MaxLength(20)]
        public string GradeLevel { get; set; } = string.Empty; // Khai tâm, Rước lễ, Thêm sức, Bao đồng

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
