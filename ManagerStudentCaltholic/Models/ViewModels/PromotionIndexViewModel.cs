using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class PromotionIndexViewModel
    {
        public int SourceYearId { get; set; }
        public string SourceYearName { get; set; } = string.Empty;

        public int TargetYearId { get; set; }
        public string TargetYearName { get; set; } = string.Empty;

        public int SelectedClassId { get; set; }
        public string SelectedClassName { get; set; } = string.Empty;
        public string CurrentGradeLevel { get; set; } = string.Empty;
        public string SuggestedNextGradeLevel { get; set; } = string.Empty;

        public PromotionRuleConfig AppliedRule { get; set; } = new();

        public List<AcademicYear> AcademicYears { get; set; } = new();
        public List<ClassRoom> SourceClasses { get; set; } = new();
        public List<ClassRoom> TargetPromoteClasses { get; set; } = new();
        public List<ClassRoom> TargetRetainClasses { get; set; } = new();

        public List<StudentPromotionEvalItem> Students { get; set; } = new();

        // Thống kê nhanh
        public int TotalStudents => Students.Count;
        public int TotalPromoted => Students.Count(s => s.IsPromoted && !s.IsAlreadyRolledOver);
        public int TotalRetained => Students.Count(s => !s.IsPromoted && !s.IsAlreadyRolledOver);
        public int TotalAlreadyProcessed => Students.Count(s => s.IsAlreadyRolledOver);
    }

    public class StudentPromotionEvalItem
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }

        public decimal? Semester1Average { get; set; }
        public decimal? Semester2Average { get; set; }
        public decimal? YearAverage { get; set; }
        public double AttendanceRate { get; set; }
        public decimal? SacramentExamScore { get; set; }
        public bool HasSacramentExamColumn { get; set; }

        public string AcademicRank { get; set; } = "Chưa xếp loại";
        public bool IsSacramentEligible { get; set; }

        public bool IsPromoted { get; set; }
        public bool IsRetained => !IsPromoted;
        public string SuggestedAction { get; set; } = "PROMOTE"; // PROMOTE | RETAIN | GRADUATE
        public int? SuggestedTargetClassId { get; set; }
        public string EvaluationReason { get; set; } = string.Empty;

        // Trạng thái nếu học sinh đã được kết chuyển sang niên khóa mới trước đó
        public bool IsAlreadyRolledOver { get; set; }
        public string? ExistingTargetClassName { get; set; }
    }
}