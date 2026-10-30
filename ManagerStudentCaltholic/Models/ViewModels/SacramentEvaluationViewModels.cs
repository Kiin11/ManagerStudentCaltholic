using System.ComponentModel.DataAnnotations;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class SacramentEvaluationFilterViewModel
    {
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public string SelectedGrade { get; set; } = "Rước Lễ";
        public int? SelectedClassId { get; set; }
        public int SacramentType { get; set; } = 2;
        public string SacramentName { get; set; } = "Xưng Tội & Rước Lễ Lần Đầu";

        // Ngưỡng xét duyệt linh hoạt
        public decimal MinYearAverage { get; set; } = 5.0m;
        public double MinAttendanceRate { get; set; } = 80.0;
        public bool RequireSacramentExam { get; set; } = true;
        public decimal MinSacramentExamScore { get; set; } = 5.0m;

        // Thông tin buổi Lễ ban Bí tích dự kiến để lập tờ trình
        public long? ExistingBatchId { get; set; }
        public string BatchStatus { get; set; } = "NEW";
        public string BatchTitle { get; set; } = string.Empty;
        public DateTime PlannedCeremonyDate { get; set; } = DateTime.Today;
        public string CeremonyParish { get; set; } = "Giáo xứ Phú Trung";
        public string CelebrantPriest { get; set; } = string.Empty;
        public string RegistryBookPrefix { get; set; } = string.Empty;
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public bool CanApprove { get; set; }

        public List<AcademicYear> AcademicYears { get; set; } = new();
        public List<ClassRoom> AvailableClasses { get; set; } = new();
        public List<SacramentApprovalBatch> SavedBatches { get; set; } = new();
        public List<StudentSacramentEvalRowDto> Candidates { get; set; } = new();

        // Chỉ số KPI
        public int TotalCandidates => Candidates.Count;
        public int TotalSystemEligible => Candidates.Count(c => c.IsSystemEligible && !c.AlreadyReceivedSacrament);
        public int TotalSpecialExemption => Candidates.Count(c => !c.IsSystemEligible && c.IsSpecialExemption && !c.AlreadyReceivedSacrament);
        public int TotalFinalApproved => Candidates.Count(c => c.IsApprovedForSacrament && !c.AlreadyReceivedSacrament);
        public int TotalIneligible => Candidates.Count(c => !c.IsApprovedForSacrament && !c.AlreadyReceivedSacrament);
        public int TotalAlreadyReceived => Candidates.Count(c => c.AlreadyReceivedSacrament);
    }

    public class StudentSacramentEvalRowDto
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;

        public decimal? Semester1Average { get; set; }
        public decimal? Semester2Average { get; set; }
        public decimal? YearAverage { get; set; }
        public bool HasSacramentExamColumn { get; set; }
        public decimal? SacramentExamScore { get; set; }

        public double MassAttendanceRate { get; set; }
        public double ClassAttendanceRate { get; set; }
        public double CombinedAttendanceRate { get; set; }
        public int TotalMakeUpCount { get; set; }

        public string AcademicRank { get; set; } = "Chưa xếp loại";
        public bool AlreadyReceivedSacrament { get; set; }
        public string? ExistingReceivedDateStr { get; set; }
        public string? ExistingBookNumber { get; set; }

        public bool IsSystemEligible { get; set; }
        public bool IsSpecialExemption { get; set; }
        public bool IsApprovedForSacrament { get; set; }

        public string? GodparentName { get; set; }
        public string? AssignedRegistryNumber { get; set; }
        public string EvaluationReason { get; set; } = string.Empty;
    }

    public class SaveSacramentApprovalBatchRequestDto
    {
        public long? BatchId { get; set; }

        [Required]
        public int AcademicYearId { get; set; }

        [Required]
        public string GradeLevel { get; set; } = "Rước Lễ";

        public int? ClassRoomId { get; set; }

        [Range(1, 4)]
        public int SacramentType { get; set; } = 2;

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề tờ trình xét duyệt.")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Range(0, 10)]
        public decimal MinYearAverage { get; set; } = 5.0m;

        [Range(0, 100)]
        public double MinAttendanceRate { get; set; } = 80.0;

        public bool RequireSacramentExam { get; set; } = true;

        [Range(0, 10)]
        public decimal MinSacramentExamScore { get; set; } = 5.0m;

        [Required]
        public DateTime PlannedCeremonyDate { get; set; } = DateTime.Today;

        [Required]
        [MaxLength(150)]
        public string CeremonyParish { get; set; } = "Giáo xứ Phú Trung";

        [MaxLength(120)]
        public string? CelebrantPriest { get; set; }

        [MaxLength(50)]
        public string? RegistryBookPrefix { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public List<SacramentApprovalCandidateInputDto> Items { get; set; } = new();
    }
}