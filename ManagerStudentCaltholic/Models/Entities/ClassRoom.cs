using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class ClassRoom
    {
        public int Id { get; set; }

        public int AcademicYearId { get; set; }
        [ValidateNever]
        public AcademicYear? AcademicYear { get; set; }

        public string RoomName { get;set; } = string.Empty; // Ví dụ: "1A", "2B", "3C"

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty; // Ví dụ: "Xưng Tội 1A"

        [MaxLength(20)]
        public string GradeLevel { get; set; } = string.Empty; // Khai tâm, Rước lễ, Thêm sức, Bao đồng
        [ValidateNever]
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        // Thêm danh sách ClassTeachers vào ClassRoom
        [ValidateNever]
        public ICollection<ClassTeacher> ClassTeachers { get; set; } = new List<ClassTeacher>();
    }
}
