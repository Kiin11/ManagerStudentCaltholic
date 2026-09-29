using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Model.Entities
{
    public class Attendance
    {
        public long Id { get; set; }

        public long EnrollmentId { get; set; }
        public Enrollment Enrollment { get; set; } = null!;

        public DateTime AttendanceDate { get; set; }

        // Bổ sung: Thứ trong tuần (0: Sunday, 4: Thursday...) để query thống kê nhanh
        public DayOfWeek DayOfWeek { get; set; }

        // 1. ĐI LỄ
        public bool AttendedMass { get; set; } = false;
        [Required, MaxLength(20)]
        public string MassStatus { get; set; } = "ABSENT_UNPERMITTED"; // PRESENT, LATE, ABSENT_...
        public TimeSpan? MassCheckInTime { get; set; }

        // 2. ĐI HỌC GIÁO LÝ
        public bool ClassAttended { get; set; } = false;
        [Required, MaxLength(20)]
        public string ClassStatus { get; set; } = "ABSENT_UNPERMITTED"; // PRESENT, LATE, ABSENT_...
        public TimeSpan? ClassCheckInTime { get; set; }

        // Điểm danh bù (TASK-408)
        public bool IsMakeUp { get; set; } = false;       // Là buổi điểm danh bù
        public DateTime? OriginalMissedDate { get; set; } // Bù cho ngày vắng nào

        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
