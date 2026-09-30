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

    // DTO nhận dữ liệu mã QR từ máy quét / Camera gửi lên
    public class QrScanRequestDto
    {
        [Required(ErrorMessage = "Thiếu mã định danh học sinh")]
        public string StudentCode { get; set; } = string.Empty;

        // Cho phép người dùng ghi đè ngày/giờ nếu cần kiểm thử hoặc quét bù
        public DateTime? ScanTimestamp { get; set; }
    }

    // Kết quả trả về cho giao diện sau khi quét
    public class QrScanResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty; // "THÁNH LỄ" hoặc "GIỜ HỌC GIÁO LÝ"
        public string Status { get; set; } = string.Empty;       // "PRESENT" hoặc "LATE"
        public string CheckInTimeStr { get; set; } = string.Empty;
        public bool IsDuplicate { get; set; }                    // Cảnh báo nếu đã quét trước đó
    }

    public class PrintStudentBadgesViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string AcademicYearName { get; set; } = string.Empty;
        public List<StudentBadgeCardItem> Badges { get; set; } = new();
    }

    public class StudentBadgeCardItem
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string QrBase64 { get; set; } = string.Empty;
    }
}
