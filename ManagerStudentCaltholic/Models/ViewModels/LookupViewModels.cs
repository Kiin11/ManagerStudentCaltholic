using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class StudentLookupRequestDto
    {
        [Required(ErrorMessage = "Vui lòng nhập Mã Thiếu Nhi (hoặc mã in trên thẻ QR)")]
        [StringLength(50, ErrorMessage = "Mã thiếu nhi không vượt quá 50 ký tự")]
        [Display(Name = "Mã Thiếu Nhi")]
        public string StudentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Ngày sinh của em")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày Sinh")]
        public DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-10);

        [Required(ErrorMessage = "Vui lòng nhập 4 số cuối SĐT Phụ huynh đã đăng ký")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "Vui lòng nhập đúng 4 chữ số cuối của SĐT Phụ huynh")]
        [Display(Name = "4 Số Cuối SĐT Phụ Huynh")]
        public string PhoneLast4Digits { get; set; } = string.Empty;

        public bool RememberOnDevice { get; set; } = true;
    }

    public class StudentLookupResultViewModel
    {
        public long StudentId { get; set; }
        public long EnrollmentId { get; set; }
        public int ClassRoomId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }

        // Chỉ lưu SĐT đã che (VD: 091****678) để không bị lộ PII trên HTML
        public string MaskedParentPhone { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string AcademicYearName { get; set; } = string.Empty;
        public string? RoomNumber { get; set; }
        public List<string> Teachers { get; set; } = new();

        // 1. Chuyên cần Thánh Lễ Chúa Nhật
        public int TotalSunMassSessions { get; set; }
        public int AttendedSunMassCount { get; set; }
        public int SunMassLinedUpCount { get; set; }
        public int SunMassPresentCount { get; set; }
        public int SunMassLateCount { get; set; }
        public int SunMassAbsentCount { get; set; }
        public double SunMassAttendanceRate { get; set; }

        // 2. Chuyên cần Thánh Lễ Thứ Năm
        public int TotalThuMassSessions { get; set; }
        public int AttendedThuMassCount { get; set; }
        public int ThuMassAbsentCount { get; set; }
        public double ThuMassAttendanceRate { get; set; }

        // 3. Chuyên cần Giờ Học Giáo Lý
        public int TotalClassSessions { get; set; }
        public int AttendedClassCount { get; set; }
        public int ClassPresentCount { get; set; }
        public int ClassLateCount { get; set; }
        public int ClassAbsentCount { get; set; }
        public double ClassAttendanceRate { get; set; }

        // Thuộc tính tổng hợp
        public int TotalMassSessions { get; set; }
        public int AttendedMassCount { get; set; }
        public double MassAttendanceRate { get; set; }

        // Bảng điểm
        public SemesterGradeDetail HK1 { get; set; } = new();
        public SemesterGradeDetail HK2 { get; set; } = new();
        public decimal? YearAverage { get; set; }
        public string OverallRank { get; set; } = "Chưa hoàn tất";
        public bool IsEligibleForSacrament { get; set; } = false;
        public string? QrCodeBase64 { get; set; }

        // FEAT-P4-05: Lịch Chiến dịch đi lễ 1 tháng (dành cho lớp chuẩn bị Bí tích)
        public MassCampaignLookupSummary? ActiveCampaign { get; set; }

        // FEAT-P4-05: Nhật ký chi tiết từng ngày đi học / đi lễ
        public List<AttendanceDayLogItem> AttendanceHistoryLogs { get; set; } = new();

        // FEAT-P4-05: Lịch sử các yêu cầu báo sai sót điểm danh đã gửi
        public List<DisputeHistoryItemDto> SubmittedDisputes { get; set; } = new();
    }

    public class SemesterGradeDetail
    {
        public int Semester { get; set; }
        public List<GradeItemView> Items { get; set; } = new();
        public decimal? SemesterAverage { get; set; }
        public string Rank { get; set; } = "Đang cập nhật";
    }

    public class GradeItemView
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ScoreType { get; set; } = string.Empty;
        public int WeightFactor { get; set; } = 1;
        public decimal? Score { get; set; }
        public bool IsSacramentExam { get; set; }
    }

    public class AttendanceDayLogItem
    {
        public long? AttendanceId { get; set; }
        public DateTime Date { get; set; }
        public string FormattedDate => Date.ToString("dd/MM/yyyy");
        public string DayLabel { get; set; } = string.Empty; // "Chúa Nhật", "Thứ Năm", "Chiến dịch (T2)"...
        public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
        public bool IsThursday => Date.DayOfWeek == DayOfWeek.Thursday;
        public bool IsCampaignDay { get; set; }
        public long? CampaignId { get; set; }

        // Trạng thái Thánh Lễ
        public bool AttendedMass { get; set; }
        public string MassStatus { get; set; } = "ABSENT_UNPERMITTED";
        public string? MassCheckInTime { get; set; }

        // Trạng thái Học Giáo Lý (Chúa Nhật)
        public bool HasClassSession { get; set; }
        public bool AttendedClass { get; set; }
        public string ClassStatus { get; set; } = "ABSENT_UNPERMITTED";
        public string? ClassCheckInTime { get; set; }

        public bool IsMakeUp { get; set; }
        public string? Note { get; set; }

        // Trạng thái báo sai sót (nếu có): PENDING, APPROVED, REJECTED
        public string? MassCorrectionStatus { get; set; }
        public string? ClassCorrectionStatus { get; set; }
        public string? TeacherNote { get; set; }

        public bool HasAnyAbsenceOrLate =>
            !AttendedMass || MassStatus == "LATE" || (HasClassSession && (!AttendedClass || ClassStatus == "LATE"));
    }

    public class MassCampaignLookupSummary
    {
        public long CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TargetDaysCount { get; set; }
        public int TotalDays { get; set; }
        public int AttendedDays { get; set; }
        public int LateDays { get; set; }
        public int ExcusedDays { get; set; }
        public int AbsentDays { get; set; }
        public int CurrentStreak { get; set; }
        public int MaxStreak { get; set; }
        public double CompletionPercentage { get; set; }
        public bool IsTargetMet => AttendedDays >= TargetDaysCount;
        public List<AttendanceDayLogItem> CampaignDays { get; set; } = new();
    }

    public class DisputeHistoryItemDto
    {
        public long Id { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string SessionScope { get; set; } = string.Empty;
        public string SessionScopeLabel { get; set; } = string.Empty;
        public string CurrentRecordedStatus { get; set; } = string.Empty;
        public string RequestedStatus { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "PENDING";
        public string? TeacherResponseNote { get; set; }
        public string? ReviewedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public class SubmitAttendanceDisputeRequestDto
    {
        [Required(ErrorMessage = "Thiếu mã thiếu nhi")]
        [MaxLength(50)]
        public string StudentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Thiếu ngày sinh")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập 4 số cuối SĐT Phụ huynh để xác nhận")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "Vui lòng nhập đúng 4 chữ số cuối SĐT Phụ huynh")]
        public string PhoneLast4Digits { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày cần báo sai sót")]
        public string AttendanceDateIso { get; set; } = string.Empty; // yyyy-MM-dd

        /// <summary>
        /// REGULAR_MASS_SUN | REGULAR_MASS_THU | REGULAR_CLASS | CAMPAIGN_MASS
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string SessionScope { get; set; } = "REGULAR_MASS_SUN";

        public long? CampaignId { get; set; }

        /// <summary>
        /// PRESENT | LINED_UP | LATE | ABSENT_PERMITTED
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string RequestedStatus { get; set; } = "PRESENT";

        [Required(ErrorMessage = "Vui lòng mô tả chi tiết lý do báo sai sót")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class ReviewAttendanceDisputeRequestDto
    {
        [Required]
        public long DisputeId { get; set; }

        /// <summary>
        /// APPROVED | REJECTED
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Decision { get; set; } = "APPROVED";

        [MaxLength(500)]
        public string? TeacherResponseNote { get; set; }
    }
}