using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class ExecutiveDashboardViewModel
    {
        // Bộ lọc hiện hành
        public int SelectedYearId { get; set; }
        public string SelectedYearName { get; set; } = string.Empty;
        public string SelectedGrade { get; set; } = "ALL";
        public int? SelectedMonth { get; set; }
        public bool IsGradeLocked { get; set; }

        public List<AcademicYear> AcademicYears { get; set; } = new();

        // 1. Thẻ chỉ số KPI Tổng quan Toàn Xứ / Khối
        public int TotalActiveStudents { get; set; }
        public int TotalEnrolledStudents { get; set; }
        public int TotalClasses { get; set; }
        public int TotalActiveTeachers { get; set; }

        public double OverallSunMassRate { get; set; }
        public double OverallThuMassRate { get; set; }
        public double OverallClassAttendanceRate { get; set; }
        public double CombinedParishAttendanceRate { get; set; }

        // Xu hướng tăng/giảm so với tháng trước (%)
        public double MonthlyTrendDelta { get; set; }
        public string CurrentMonthLabel { get; set; } = string.Empty;
        public string PreviousMonthLabel { get; set; } = string.Empty;

        // Thống kê vận hành chờ xử lý
        public int PendingRegistrationsCount { get; set; }
        public int PendingAbsenceRequestsCount { get; set; }
        public int PendingRoomIncidentsCount { get; set; }
        public int SacramentEligibleCount { get; set; }

        // 2. Dữ liệu Biểu đồ Cột theo 4 Khối (Khai Tâm, Rước Lễ, Thêm Sức, Bao Đồng)
        public List<GradeAttendanceBreakdownDto> GradeBreakdowns { get; set; } = new();

        // 3. Dữ liệu Biểu đồ Đường Xu hướng theo Tháng trong Niên khóa
        public List<MonthlyAttendanceTrendPointDto> MonthlyTrends { get; set; } = new();

        // 4. Cơ cấu trạng thái chuyên cần (Dùng cho Doughnut Chart)
        public int TotalLinedUpCount { get; set; }
        public int TotalPresentOnTimeCount { get; set; }
        public int TotalLateCount { get; set; }
        public int TotalPermittedAbsentCount { get; set; }
        public int TotalUnpermittedAbsentCount { get; set; }

        // 5. Dữ liệu Bản đồ nhiệt (Heatmap) Vắng Lễ & Vắng Học theo từng ngày T5 / CN
        public List<AttendanceHeatmapCellDto> HeatmapCells { get; set; } = new();

        // 6. Bảng Xếp hạng Lớp & Danh sách Thiếu nhi cần cảnh báo sớm
        public List<ClassRankingSummaryDto> TopPerformingClasses { get; set; } = new();
        public List<AtRiskStudentAlertDto> HighAbsenceStudents { get; set; } = new();
    }

    public class GradeAttendanceBreakdownDto
    {
        public string GradeLevel { get; set; } = string.Empty;
        public int ClassCount { get; set; }
        public int StudentCount { get; set; }
        public double SundayMassRate { get; set; }
        public double ThursdayMassRate { get; set; }
        public double ClassAttendanceRate { get; set; }
        public double CombinedRate { get; set; }
        public int AbsentUnpermittedTotal { get; set; }
    }

    public class MonthlyAttendanceTrendPointDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string Label => $"Thg {Month:D2}/{Year}";
        public int TotalSessions { get; set; }
        public double SundayMassRate { get; set; }
        public double ThursdayMassRate { get; set; }
        public double ClassAttendanceRate { get; set; }
        public int TotalAbsencesCount { get; set; }
    }

    public class AttendanceHeatmapCellDto
    {
        public DateTime Date { get; set; }
        public string FormattedDate => Date.ToString("dd/MM/yyyy");
        public string ShortDate => Date.ToString("dd/MM");
        public string MonthGroup => $"Tháng {Date:MM/yyyy}";
        public DayOfWeek DayOfWeek { get; set; }
        public bool IsSunday => DayOfWeek == DayOfWeek.Sunday;
        public string SessionLabel => IsSunday ? "Chúa Nhật (Lễ & Học)" : "Thứ Năm (Thánh Lễ)";

        public int TotalExpectedStudents { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int PermittedAbsentCount { get; set; }
        public int UnpermittedAbsentCount { get; set; }
        public int TotalAbsentCount => PermittedAbsentCount + UnpermittedAbsentCount;

        public double AbsenceRatePercent { get; set; }
        public double AttendanceRatePercent { get; set; }

        /// <summary>
        /// Cấp độ nhiệt từ 0 đến 4:
        /// 0: Chưa có dữ liệu | 1: Tốt (Vắng < 5%) | 2: Khá (Vắng 5-12%) | 3: Cảnh báo (Vắng 12-22%) | 4: Cao điểm (Vắng > 22%)
        /// </summary>
        public int HeatLevel { get; set; }
    }

    public class ClassRankingSummaryDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public double MassRate { get; set; }
        public double ClassRate { get; set; }
        public double CombinedRate { get; set; }
    }

    public class AtRiskStudentAlertDto
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string? ParentPhone { get; set; }
        public int UnpermittedAbsentCount { get; set; }
        public int TotalAbsentCount { get; set; }
        public double CombinedAttendanceRate { get; set; }
        public DateTime? LastAttendedDate { get; set; }
    }
}