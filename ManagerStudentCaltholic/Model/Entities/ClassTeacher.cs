using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Model.Entities
{
    public class ClassTeacher
    {
        public long Id { get; set; }

        public int ClassRoomId { get; set; }
        [ValidateNever]
        public ClassRoom ClassRoom { get; set; } = null!;

        [Required(ErrorMessage = "Tên Giáo lý viên không được để trống")]
        [MaxLength(100)]
        public string TeacherName { get; set; } = string.Empty;

        [MaxLength(15)]
        public string? PhoneNumber { get; set; }

        // HEAD: GLV Chủ nhiệm, MEMBER: GLV Đồng hành, DEPUTY: GLV Dự bị / Trợ tá
        [Required]
        [MaxLength(20)]
        public string RoleInClass { get; set; } = "HEAD";

        public int AcademicYearId { get; set; }
        [ValidateNever]
        public AcademicYear AcademicYear { get; set; } = null!;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
