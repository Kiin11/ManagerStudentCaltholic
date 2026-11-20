using System.ComponentModel.DataAnnotations;
using ManagerStudentCaltholic.Interface.Models;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class ClassRoom : ISoftDelete
    {
        private string _gradeLevel = string.Empty;

        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string GradeLevel
        {
            get => _gradeLevel;
            set
            {
                _gradeLevel = value ?? string.Empty;
                Session = Models.Entities.GradeLevel.ResolveSession(_gradeLevel);
            }
        }

        [MaxLength(50)]
        public string? RoomName { get; set; }

        [MaxLength(20)]
        public string? Shift { get; set; }

        /// <summary>
        /// TASK-01.2 (Phase 6): Ca sinh hoạt của lớp (Morning / Afternoon).
        /// Tự động ánh xạ: Khai Tâm, Rước Lễ, Thêm Sức = Morning; Bao Đồng, Dự Bị = Afternoon.
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Session { get; set; } = Models.Entities.GradeLevel.SessionMorning;

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