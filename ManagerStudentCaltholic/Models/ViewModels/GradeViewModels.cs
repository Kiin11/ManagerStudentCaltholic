using System.ComponentModel.DataAnnotations;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class SaveGradeItemDto
    {
        public long EnrollmentId { get; set; }
        public int GradeConfigurationId { get; set; }

        [Range(0.00, 10.00, ErrorMessage = "Điểm phải từ 0.0 đến 10.0")]
        public decimal? Score { get; set; }
        public string? Note { get; set; }
    }

    public class CreateGradeColumnDto
    {
        [Required]
        public int ClassId { get; set; }

        [Range(1, 2)]
        public int Semester { get; set; } = 1;

        [Required(ErrorMessage = "Vui lòng nhập tên cột điểm")]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string ScoreType { get; set; } = "CUSTOM";

        [Range(1, 5)]
        public int WeightFactor { get; set; } = 1;
    }

    public class SaveBatchGradeRequestDto
    {
        public int ClassId { get; set; }
        public int Semester { get; set; }
        public List<SaveGradeItemDto> Grades { get; set; } = new();
    }

    public class ClassGradeMatrixViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public int CurrentSemester { get; set; } = 1;
        public bool IsLocked { get; set; }

        // FEAT-P4-02: Thông tin Nút Xét Duyệt Thông Minh theo loại lớp
        public ClassEvaluationActionDescriptor EvaluationAction { get; set; } = new();

        public List<GradeConfiguration> Columns { get; set; } = new();
        public List<StudentGradeRowItem> Rows { get; set; } = new();
    }

    public class StudentGradeRowItem
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public Dictionary<int, decimal?> Scores { get; set; } = new();

        public decimal? Semester1Average { get; set; }
        public decimal? Semester2Average { get; set; }
        public decimal? SemesterAverage { get; set; }
        public decimal? YearAverage { get; set; }
        public double AttendanceRate { get; set; }
        public string AcademicRank { get; set; } = "Chưa xếp loại";
        public bool IsEligibleForSacrament { get; set; }

        // FEAT-P4-02: Trạng thái đề xuất của GLV ngay trên dòng Sổ điểm
        public string TeacherProposalStatus { get; set; } = "PENDING";
        public string? TeacherProposalNote { get; set; }
    }

    #region FEAT-P4-02: Class Evaluation DTOs & ViewModels
    public class ClassEvaluationActionDescriptor
    {
        /// <summary>
        /// PROMOTION_NORMAL | SACRAMENT_RL2 | SACRAMENT_TS3 | PROMISE_BD3_DB4
        /// </summary>
        public string ActionType { get; set; } = "PROMOTION_NORMAL";

        public string ButtonLabel { get; set; } = "Xét Lên Lớp";
        public string ButtonCssClass { get; set; } = "btn-primary";
        public string IconClass { get; set; } = "bi-mortarboard-fill";
        public string ModalTitle { get; set; } = "Xét Duyệt Lên Lớp Cuối Năm";
        public string TargetDescription { get; set; } = "Đánh giá điều kiện hoàn thành chương trình giáo lý để lên lớp ở niên khóa kế tiếp.";
        public int? SacramentType { get; set; }
        public bool RequireCampaignCheck { get; set; }
        public int SubmittedCount { get; set; }
        public int TotalStudents { get; set; }
        public long? LinkedApprovalBatchId { get; set; }
        public string? LinkedBatchStatus { get; set; }
    }

    public class ClassEvaluationSheetViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;

        public ClassEvaluationActionDescriptor Descriptor { get; set; } = new();

        public decimal MinYearAverage { get; set; } = 5.0m;
        public double MinAttendanceRate { get; set; } = 80.0;
        public decimal MinSacramentExamScore { get; set; } = 5.0m;

        public bool HasActiveMassCampaign { get; set; }
        public string? MassCampaignTitle { get; set; }
        public int MassCampaignTargetDays { get; set; }

        public List<ClassEvaluationStudentItemDto> Students { get; set; } = new();

        // KPI
        public int TotalCount => Students.Count;
        public int AutoEligibleCount => Students.Count(s => s.IsAutoEligible);
        public int SpecialProposedCount => Students.Count(s => s.ProposalStatus == "PROPOSED_SPECIAL");
        public int RetainedCount => Students.Count(s => s.ProposalStatus == "PROPOSED_RETAIN");
    }

    public class ClassEvaluationStudentItemDto
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public decimal? Semester1Avg { get; set; }
        public decimal? Semester2Avg { get; set; }
        public decimal? YearAvg { get; set; }
        public decimal? SacramentExamScore { get; set; }

        public double MassAttendanceRate { get; set; }
        public double ClassAttendanceRate { get; set; }
        public double CombinedAttendanceRate { get; set; }

        // Dữ liệu Chiến dịch đi lễ 1 tháng (FEAT-P4-01)
        public int CampaignAttendedDays { get; set; }
        public int CampaignTargetDays { get; set; }
        public bool IsCampaignTargetMet { get; set; }

        public string AcademicRank { get; set; } = "Chưa xếp loại";
        public bool IsAutoEligible { get; set; }
        public string AutoIneligibleReason { get; set; } = string.Empty;

        /// <summary>
        /// PROPOSED_PASS: Đạt chuẩn | PROPOSED_SPECIAL: Đề xuất đặc cách | PROPOSED_RETAIN: Chưa đạt / Ở lại lớp
        /// </summary>
        public string ProposalStatus { get; set; } = "PROPOSED_PASS";

        public string? ProposalNote { get; set; }
        public string? GodparentName { get; set; }
    }

    public class SubmitClassEvaluationRequestDto
    {
        [Required]
        public int ClassId { get; set; }

        public List<SubmitStudentEvaluationItemDto> Items { get; set; } = new();
    }

    public class SubmitStudentEvaluationItemDto
    {
        [Required]
        public long EnrollmentId { get; set; }

        /// <summary>
        /// PROPOSED_PASS | PROPOSED_SPECIAL | PROPOSED_RETAIN
        /// </summary>
        [Required]
        public string ProposalStatus { get; set; } = "PROPOSED_PASS";

        [MaxLength(500)]
        public string? ProposalNote { get; set; }

        [MaxLength(150)]
        public string? GodparentName { get; set; }
    }
    #endregion
}