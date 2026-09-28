using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Model.Entities
{
    public class Attendance
    {
        public long Id { get; set; }

        public long EnrollmentId { get; set; }
        public Enrollment Enrollment { get; set; } = null!;

        public DateTime AttendanceDate { get; set; } 

        [Required, MaxLength(20)]
        public string Status { get; set; } = "PRESENT"; // PRESENT, ABSENT_PERMITTED, ABSENT_UNPERMITTED

        public bool MassAttended { get; set; } = true; // Dự Lễ Chúa Nhật
        public bool WedAttended { get; set; } = true; // Dự Lễ Thứ 5
        public bool ClassAttended { get; set; } = true; // Đi học

        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
