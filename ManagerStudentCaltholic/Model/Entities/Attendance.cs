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

        public bool MassAttended { get; set; } = false; // Dự Lễ 
        public bool ClassAttended { get; set; } = false; // Đi học

        // Hỗ trợ điểm danh bù (được thiết lập ở Task-408)
        public bool IsMakeUp { get; set; } = false;       // Đánh dấu bản ghi là điểm danh bù
        public DateTime? OriginalMissedDate { get; set; } // Ngày vắng ban đầu cần bù

        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
