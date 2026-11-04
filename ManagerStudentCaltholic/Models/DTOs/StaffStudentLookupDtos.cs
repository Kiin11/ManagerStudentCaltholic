using System;
using System.Collections.Generic;

namespace ManagerStudentCaltholic.Models.DTOs
{
    public class StaffStudentQuickSearchResultDto
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }
        public string? CurrentClassName { get; set; }
        public string? CurrentAcademicYearName { get; set; }
        public bool IsHistoricalMatch { get; set; }
        public string? MatchedHistoricalCode { get; set; }
    }

    public class StaffStudent360DetailDto
    {
        public StudentBasicInfoDto BasicInfo { get; set; } = new();
        public AttendanceOverviewDto Attendance { get; set; } = new();
        public AcademicHistoryDto Academic { get; set; } = new();
        public SacramentHistoryDto Sacrament { get; set; } = new();
        public ParentContactDto ParentContact { get; set; } = new();
        public string ActiveOption { get; set; } = "OVERVIEW";
    }

    public class StudentBasicInfoDto
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{ChristianName} {LastName} {FirstName}".Trim();
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }
        public int Age => Math.Max(0, DateTime.UtcNow.Year - DateOfBirth.Year);
        public string? ParentPhone { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CurrentClassName { get; set; }
        public string? CurrentGradeLevel { get; set; }
        public string? CurrentRoomName { get; set; }
        public string? CurrentShift { get; set; }
        public string? CurrentAcademicYearName { get; set; }
        public long? CurrentEnrollmentId { get; set; }
    }

    public class AttendanceOverviewDto
    {
        public RecentAttendanceItemDto? LatestSunday { get; set; }
        public RecentAttendanceItemDto? LatestThursday { get; set; }
        public RecentAttendanceItemDto? LatestClassSession { get; set; }
        public MassCampaignSummaryDto? ActiveCampaign { get; set; }
        public int TotalMassAttendedCurrentYear { get; set; }
        public int TotalMassMissedCurrentYear { get; set; }
        public double MassAttendanceRatePercent { get; set; }
        public int TotalClassAttendedCurrentYear { get; set; }
        public int TotalClassMissedCurrentYear { get; set; }
        public double ClassAttendanceRatePercent { get; set; }
    }

    public class RecentAttendanceItemDto
    {
        public DateTime AttendanceDate { get; set; }
        public bool IsAttended { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string StatusBadgeColor { get; set; } = "secondary";
        public TimeSpan? CheckInTime { get; set; }
        public string? CheckInTimeDisplay => CheckInTime.HasValue ? CheckInTime.Value.ToString(@"hh\:mm") : null;
        public string SessionType { get; set; } = string.Empty; // "MASS_SUNDAY", "MASS_THURSDAY", "CLASS_CATECHISM"
    }

    public class MassCampaignSummaryDto
    {
        public long CampaignId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TargetDays { get; set; } = 25;
        public int AttendedDays { get; set; }
        public int CurrentStreak { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public double CompletionRate => TargetDays > 0 ? Math.Min(100, Math.Round((double)AttendedDays / TargetDays * 100, 1)) : 0;
        public bool IsTargetReached => AttendedDays >= TargetDays;
        public List<string> RecentPresentDatesIso { get; set; } = new();
    }

    public class AcademicHistoryDto
    {
        public decimal? CurrentYearAverage { get; set; }
        public string? CurrentClassification { get; set; }
        public List<CurrentGradeRecordDto> CurrentGrades { get; set; } = new();
        public List<PastYearHistoryDto> PastTwoYears { get; set; } = new();
    }

    public class CurrentGradeRecordDto
    {
        public string ColumnName { get; set; } = string.Empty;
        public int WeightFactor { get; set; } = 1;
        public decimal? Score { get; set; }
        public int Semester { get; set; } = 1;
        public string? Note { get; set; }
    }

    public class PastYearHistoryDto
    {
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public decimal? YearAverageScore { get; set; }
        public double AttendanceRatePercent { get; set; }
        public string RolloverOutcome { get; set; } = "PROMOTE";
        public string RolloverOutcomeDisplay { get; set; } = "Lên lớp";
        public bool IsPassed { get; set; } = true;
        public string Classification { get; set; } = "Đạt";
    }

    public class SacramentHistoryDto
    {
        public SacramentMilestoneItemDto Baptism { get; set; } = new() { MilestoneName = "Rửa Tội", SacramentType = 1 };
        public SacramentMilestoneItemDto FirstCommunion { get; set; } = new() { MilestoneName = "Rước Lễ Lần Đầu", SacramentType = 2 };
        public SacramentMilestoneItemDto Confirmation { get; set; } = new() { MilestoneName = "Thêm Sức", SacramentType = 3 };
        public SacramentMilestoneItemDto SolemnProfession { get; set; } = new() { MilestoneName = "Bao Đồng", SacramentType = 4 };
        public int CompletedCount => (Baptism.IsReceived ? 1 : 0) + (FirstCommunion.IsReceived ? 1 : 0) + (Confirmation.IsReceived ? 1 : 0) + (SolemnProfession.IsReceived ? 1 : 0);
    }

    public class SacramentMilestoneItemDto
    {
        public string MilestoneName { get; set; } = string.Empty;
        public int SacramentType { get; set; }
        public bool IsReceived { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string? ParishName { get; set; }
        public string? PriestName { get; set; }
        public string? GodparentName { get; set; }
        public string? RegistryBookNumber { get; set; }
        public string? Notes { get; set; }
    }

    public class ParentContactDto
    {
        public string? ParentPhone { get; set; }
        public string? FormattedPhone { get; set; }
        public string? Address { get; set; }
        public string? ZaloLink => !string.IsNullOrWhiteSpace(ParentPhone) ? $"https://zalo.me/{ParentPhone.Trim().Replace(" ", "").Replace("+84", "0")}" : null;
        public string? QuickSmsLink => !string.IsNullOrWhiteSpace(ParentPhone) ? $"sms:{ParentPhone.Trim()}" : null;
        public string? QuickCallLink => !string.IsNullOrWhiteSpace(ParentPhone) ? $"tel:{ParentPhone.Trim()}" : null;
        public List<ParentMessageQuickTemplateDto> Templates { get; set; } = new();
    }

    public class ParentMessageQuickTemplateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = "ATTENDANCE"; // ATTENDANCE | REMINDER | SACRAMENT | ENCOURAGE
    }

    public class RecentLookupItemDto
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string? ClassName { get; set; }
        public DateTime LookedUpAt { get; set; }
        public string TimeAgoDisplay { get; set; } = string.Empty;
    }
}
