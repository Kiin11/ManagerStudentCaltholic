using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class ParentMessageCenterViewModel
    {
        public int SelectedClassId { get; set; }
        public string SelectedClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string SelectedCategory { get; set; } = "ALL";
        public bool FilterAttentionOnly { get; set; } = false;
        public bool CanManageTemplates { get; set; }

        public List<ClassRoom> AuthorizedClasses { get; set; } = new();
        public List<ParentMessageTemplate> Templates { get; set; } = new();
        public List<StudentInteractionRowDto> Students { get; set; } = new();

        public int TotalStudentsCount => Students.Count;
        public int AttentionStudentsCount => Students.Count(s => s.NeedsAttention);
    }

    public class StudentInteractionRowDto
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ParentPhone { get; set; }
        public string ClassName { get; set; } = string.Empty;

        public decimal? Semester1Average { get; set; }
        public decimal? Semester2Average { get; set; }
        public decimal? YearAverage { get; set; }
        public string AcademicRank { get; set; } = "Chưa xếp loại";

        public int TotalAbsentDays { get; set; }
        public int MassAbsentCount { get; set; }
        public int ClassAbsentCount { get; set; }
        public double CombinedAttendanceRate { get; set; }
        public bool IsEligibleForSacrament { get; set; }

        public bool NeedsAttention { get; set; }
        public List<string> AttentionBadges { get; set; } = new();
    }
}