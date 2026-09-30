using ManagerStudentCaltholic.Services;
using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    // DTO nhận payload Ajax từ giao diện gửi lên
    public class AttendanceBatchSubmitDto
    {
        [Required(ErrorMessage = "Thiếu thông tin lớp học")]
        public int ClassId { get; set; }

        [Required(ErrorMessage = "Thiếu ngày điểm danh")]
        public DateTime AttendanceDate { get; set; }

        public List<AttendanceItemSubmitDto> Items { get; set; } = new();
    }

    public class AttendanceItemSubmitDto
    {
        public long EnrollmentId { get; set; }
        public bool AttendedMass { get; set; }
        public string MassStatus { get; set; } = AttendanceStatus.AbsentUnpermitted;

        public bool ClassAttended { get; set; }
        public string ClassStatus { get; set; } = AttendanceStatus.AbsentUnpermitted;

        public string? Note { get; set; }
    }

    // ViewModel phục vụ hiển thị màn hình Sổ Điểm Danh Lớp
    public class ClassAttendanceSheetViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public DateTime AttendanceDate { get; set; }
        public DayOfWeek DayOfWeek => AttendanceDate.DayOfWeek;

        public List<StudentAttendanceRowItem> Students { get; set; } = new();
    }

    public class StudentAttendanceRowItem
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";

        // Trạng thái đi Lễ
        public bool AttendedMass { get; set; }
        public string MassStatus { get; set; } = AttendanceStatus.AbsentUnpermitted;
        public TimeSpan? MassCheckInTime { get; set; }

        // Trạng thái đi Học
        public bool ClassAttended { get; set; }
        public string ClassStatus { get; set; } = AttendanceStatus.AbsentUnpermitted;
        public TimeSpan? ClassCheckInTime { get; set; }

        public bool IsMakeUp { get; set; }
        public string? Note { get; set; }
    }
}
