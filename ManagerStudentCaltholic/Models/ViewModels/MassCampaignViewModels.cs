using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class MassCampaignDashboardViewModel
    {
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public int SelectedClassId { get; set; }
        public string SelectedClassName { get; set; } = string.Empty;
        public string SelectedGradeLevel { get; set; } = string.Empty;
        public bool IsEligibleSacramentClass { get; set; }
        public string SacramentClassBadge { get; set; } = string.Empty;
        public bool CanManageCampaign { get; set; }

        public List<EligibleCampaignClassItemDto> AvailableClasses { get; set; } = new();
        public List<CampaignSummaryItemDto> ClassCampaigns { get; set; } = new();

        public CampaignDetailMatrixDto? ActiveCampaignMatrix { get; set; }
    }

    public class EligibleCampaignClassItemDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string SacramentLabel { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public bool HasActiveCampaign { get; set; }
    }

    public class CampaignSummaryItemDto
    {
        public long CampaignId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TargetDaysCount { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class CampaignDetailMatrixDto
    {
        public long CampaignId { get; set; }
        public int ClassRoomId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ApplicableDaysCsv { get; set; } = "0,1,2,3,4,5,6";
        public int TargetDaysCount { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public string CreatedBy { get; set; } = string.Empty;

        public List<CampaignDateColumnDto> DateColumns { get; set; } = new();
        public List<StudentCampaignRowDto> StudentRows { get; set; } = new();

        // KPI Summary
        public int TotalApplicableDays => DateColumns.Count;
        public int ElapsedApplicableDays => DateColumns.Count(d => d.Date.Date <= DateTime.UtcNow.AddHours(7).Date);
        public int TotalStudents => StudentRows.Count;
        public int TargetMetStudentsCount => StudentRows.Count(s => s.IsTargetMet);
        public double AverageCompletionRate => StudentRows.Any()
            ? Math.Round(StudentRows.Average(s => s.CompletionPercentage), 1)
            : 0.0;
    }

    public class CampaignDateColumnDto
    {
        public DateTime Date { get; set; }
        public string DayNumberText => Date.ToString("dd/MM");
        public string DayOfWeekShort { get; set; } = string.Empty;
        public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
        public bool IsToday { get; set; }
        public bool IsFuture { get; set; }
        public int PresentCount { get; set; }
    }

    public class StudentCampaignRowDto
    {
        public long StudentId { get; set; }
        public long EnrollmentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public int PresentDays { get; set; }
        public int LateDays { get; set; }
        public int ExcusedDays { get; set; }
        public int AbsentDays { get; set; }

        /// <summary>
        /// Tổng số ngày được tính đi lễ (PRESENT = 1.0, LATE = 0.8 hoặc 1 buổi có mặt tùy quy chế, ở đây tính số buổi thực đi = Present + Late)
        /// </summary>
        public int TotalAttendedDays => PresentDays + LateDays;

        public double EffectiveScoreDays => Math.Round(PresentDays + (LateDays * 0.8), 1);

        public double CompletionPercentage { get; set; }
        public int CurrentStreak { get; set; }
        public int MaxStreak { get; set; }
        public bool IsTargetMet { get; set; }

        /// <summary>
        /// Key: yyyy-MM-dd
        /// </summary>
        public Dictionary<string, CampaignCellStatusDto> DailyCells { get; set; } = new();
    }

    public class CampaignCellStatusDto
    {
        public long? RecordId { get; set; }
        public string Status { get; set; } = "NONE"; // PRESENT, LATE, EXCUSED, ABSENT, NONE
        public string CheckInMethod { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedAt { get; set; }
    }

    public class CreateMassCampaignRequestDto
    {
        [Required]
        public int ClassRoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên chiến dịch đi lễ.")]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public List<int> ApplicableDays { get; set; } = new() { 0, 1, 2, 3, 4, 5, 6 };

        [Range(1, 60, ErrorMessage = "Chỉ tiêu số ngày cần đạt phải từ 1 đến 60 ngày.")]
        public int TargetDaysCount { get; set; } = 25;
    }

    public class UpdateMassCampaignConfigDto
    {
        [Required]
        public long CampaignId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Range(1, 60)]
        public int TargetDaysCount { get; set; } = 25;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "ACTIVE";
    }

    public class UpdateCampaignCellRequestDto
    {
        [Required]
        public long CampaignId { get; set; }

        [Required]
        public long EnrollmentId { get; set; }

        [Required]
        public string DateIso { get; set; } = string.Empty; // yyyy-MM-dd

        /// <summary>
        /// PRESENT | LATE | EXCUSED | ABSENT | CLEAR
        /// </summary>
        [Required]
        public string Status { get; set; } = "PRESENT";

        [MaxLength(300)]
        public string? Note { get; set; }
    }

    public class BulkMarkCampaignDayRequestDto
    {
        [Required]
        public long CampaignId { get; set; }

        [Required]
        public string DateIso { get; set; } = string.Empty; // yyyy-MM-dd

        [Required]
        public string Status { get; set; } = "PRESENT";

        public bool OverwriteExisting { get; set; } = false;
    }

    public class CampaignQrScanRequestDto
    {
        [Required]
        public long CampaignId { get; set; }

        [Required]
        public string StudentCode { get; set; } = string.Empty;

        public string? DateIso { get; set; }
    }
}