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

    #region Diem danh bù và thống kê chuyên cần
    public class MakeUpAttendanceRequestDto
    {
        [Required]
        public long EnrollmentId { get; set; }

        [Required]
        public DateTime OriginalMissedDate { get; set; } // Ngày thiếu nhi đã vắng trước đó

        [Required]
        public DateTime MakeUpDate { get; set; } // Ngày thực hiện trả bài/đi lễ bù

        public bool MakeUpMass { get; set; }     // Bù Thánh Lễ
        public bool MakeUpClass { get; set; }    // Bù Giờ Học Giáo Lý

        [Required(ErrorMessage = "Vui lòng nhập lý do/hình thức bù")]
        [StringLength(255)]
        public string Reason { get; set; } = string.Empty; // VD: "Đi lễ bù sáng thứ 7", "Đã trả bài kinh cho GLV"
    }

    // DTO xem lịch sử các ngày vắng chưa bù của học sinh
    public class MissedDateItemDto
    {
        public long AttendanceId { get; set; }
        public DateTime MissedDate { get; set; }
        public string DayOfWeekName { get; set; } = string.Empty;
        public bool MissedMass { get; set; }
        public bool MissedClass { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // ViewModel cho màn hình Thống kê chuyên cần (TASK-411)
    public class AttendanceStatisticsViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string AcademicYearName { get; set; } = string.Empty;
        public int TotalMassSessions { get; set; }  // Tổng số buổi lễ đã diễn ra cn
        public int TotalThuMassSessions { get; set; }  // Tổng số buổi lễ đã diễn ra Thứ 5
        public int TotalClassSessions { get; set; } // Tổng số buổi học giáo lý đã diễn ra
        public List<StudentAttendanceStatRow> StudentStats { get; set; } = new();
    }

    public class StudentAttendanceStatRow
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        // Thống kê Lễ CN
        public int MassPresentCount { get; set; }
        public int MassLateCount { get; set; }
        public int MassAbsentPermitted { get; set; }
        public int MassAbsentUnpermitted { get; set; }
        public double MassAttendanceRate { get; set; } // Tỷ lệ %

        // Thống kê Lễ
        public int MassThuPresentCount { get; set; }
        public int MassThuLateCount { get; set; }
        public int MassThuAbsentPermitted { get; set; }
        public int MassThuAbsentUnpermitted { get; set; }
        public double MassThuAttendanceRate { get; set; } // Tỷ lệ %

        // Thống kê Học Giáo Lý
        public int ClassPresentCount { get; set; }
        public int ClassLateCount { get; set; }
        public int ClassAbsentPermitted { get; set; }
        public int ClassAbsentUnpermitted { get; set; }
        public double ClassAttendanceRate { get; set; } // Tỷ lệ %

        public int TotalMakeUpCount { get; set; }
        public bool IsEligibleForSacrament { get; set; } // Đủ điều kiện lãnh Bí tích (>= 80% cả Lễ và Học)
    }
    #endregion

    #region Thống kê điểm danh theo tháng (TASK-412)
    public class MonthlyAttendanceSessionHeader
    {
        public DateTime Date { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public string FormattedDate => Date.ToString("dd/MM");
        public bool IsSunday => DayOfWeek == DayOfWeek.Sunday;
        public bool IsThursday => DayOfWeek == DayOfWeek.Thursday;
    }

    public class StudentDailyStatusDto
    {
        public DateTime Date { get; set; }
        public DayOfWeek DayOfWeek { get; set; }

        // Trạng thái Lễ (T5 hoặc CN)
        public bool HasMassRecord { get; set; }
        public string MassStatus { get; set; } = "ABSENT_UNPERMITTED"; // PRESENT, LATE, ABSENT_PERMITTED, ABSENT_UNPERMITTED
        public TimeSpan? MassCheckInTime { get; set; }
        public bool IsMassMakeUp { get; set; }

        // Trạng thái Học (Chỉ CN)
        public bool HasClassRecord { get; set; }
        public string ClassStatus { get; set; } = "ABSENT_UNPERMITTED";
        public TimeSpan? ClassCheckInTime { get; set; }
        public bool IsClassMakeUp { get; set; }
    }

    public class StudentMonthlyAttendanceRow
    {
        public long StudentId { get; set; }
        public long EnrollmentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ChristianName { get; set; }

        // Key: Date.Date (yyyy-MM-dd)
        public Dictionary<DateTime, StudentDailyStatusDto> DailyStatuses { get; set; } = new();

        // Tổng kết tháng
        public int TotalMassAttended { get; set; }
        public int TotalMassLate { get; set; }
        public int TotalMassAbsent { get; set; }

        public int TotalClassAttended { get; set; }
        public int TotalClassLate { get; set; }
        public int TotalClassAbsent { get; set; }
    }

    public class MonthlyAttendanceReportViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }

        // Danh sách các ngày sinh hoạt (Thứ 5 & Chúa Nhật) trong tháng
        public List<MonthlyAttendanceSessionHeader> SessionDates { get; set; } = new();

        public List<StudentMonthlyAttendanceRow> Rows { get; set; } = new();
    }

    #endregion
}
