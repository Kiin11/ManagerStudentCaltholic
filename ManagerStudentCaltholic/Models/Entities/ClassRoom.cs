using System.ComponentModel.DataAnnotations;
using ManagerStudentCaltholic.Interface.Models;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class ClassRoom : ISoftDelete
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string GradeLevel { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? RoomName { get; set; }

        [MaxLength(20)]
        public string? Shift { get; set; }

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        // Chỉ giữ duy nhất mối quan hệ tới 1 phòng học cố định
        public int? ClassRoomLocationId { get; set; }
        public ClassRoomLocation? ClassRoomLocation { get; set; }

        public bool IsGradeLocked { get; set; } = false;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<ClassTeacher> ClassTeachers { get; set; } = new List<ClassTeacher>();
    }
}