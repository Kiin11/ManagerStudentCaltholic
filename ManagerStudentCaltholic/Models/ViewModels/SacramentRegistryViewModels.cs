using System.ComponentModel.DataAnnotations;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class SacramentRegistryIndexViewModel
    {
        public string? SearchKeyword { get; set; }
        public string? SelectedGrade { get; set; }
        public int? SelectedClassId { get; set; }
        public int? SacramentFilter { get; set; } // null = Tất cả, 1 = Đã RT, 2 = Đã RL, 3 = Đã TS, 4 = Đã BĐ, -1 = Thiếu Bí tích theo tuổi

        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalStudents { get; set; }

        public int TotalBaptized { get; set; }
        public int TotalFirstCommunion { get; set; }
        public int TotalConfirmed { get; set; }
        public int TotalProfessionOfFaith { get; set; }

        public List<ClassRoom> AvailableClasses { get; set; } = new();
        public List<StudentSacramentSummaryRow> Rows { get; set; } = new();
    }

    public class StudentSacramentSummaryRow
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public int Age { get; set; }
        public string ClassName { get; set; } = "Chưa xếp lớp";
        public string GradeLevel { get; set; } = string.Empty;

        public bool HasBaptism { get; set; }
        public string? BaptismDateStr { get; set; }
        public string? BaptismBookNo { get; set; }

        public bool HasFirstCommunion { get; set; }
        public string? FirstCommunionDateStr { get; set; }
        public string? FirstCommunionBookNo { get; set; }

        public bool HasConfirmation { get; set; }
        public string? ConfirmationDateStr { get; set; }
        public string? ConfirmationBookNo { get; set; }

        public bool HasProfessionOfFaith { get; set; }
        public string? ProfessionDateStr { get; set; }
        public string? ProfessionBookNo { get; set; }

        public bool HasAgeWarning { get; set; }
    }

    public class SacramentRecordItemDto
    {
        public long Id { get; set; }
        public long StudentId { get; set; }
        public int SacramentType { get; set; }
        public string SacramentName { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string? ReceivedDate { get; set; }
        public string? ReceivedDateDisplay { get; set; }
        public string? ParishName { get; set; }
        public string? PriestName { get; set; }
        public string? GodparentName { get; set; }
        public string? RegistryBookNumber { get; set; }
        public string? Notes { get; set; }
        public string? UpdatedBy { get; set; }
        public string? UpdatedAtDisplay { get; set; }
    }

    public class MissingSacramentAlertDto
    {
        public int SacramentType { get; set; }
        public string SacramentName { get; set; } = string.Empty;
        public int StandardAge { get; set; }
        public int CurrentAge { get; set; }
        public string WarningMessage { get; set; } = string.Empty;
    }

    public class StudentSacramentTimelineResponseDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string DateOfBirthDisplay { get; set; } = string.Empty;
        public int CurrentAge { get; set; }
        public bool CanEdit { get; set; }
        public List<SacramentRecordItemDto> Timeline { get; set; } = new();
        public List<MissingSacramentAlertDto> MissingAlerts { get; set; } = new();
    }

    public class SaveSacramentRecordRequestDto
    {
        public long? Id { get; set; }

        [Required]
        public long StudentId { get; set; }

        [Required]
        [Range(1, 4, ErrorMessage = "Loại Bí tích không hợp lệ (1: Rửa Tội, 2: Rước Lễ, 3: Thêm Sức, 4: Bao Đồng).")]
        public int SacramentType { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày lãnh nhận Bí tích.")]
        [DataType(DataType.Date)]
        public DateTime ReceivedDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên Giáo xứ ban Bí tích.")]
        [MaxLength(150, ErrorMessage = "Tên Giáo xứ không vượt quá 150 ký tự.")]
        public string ParishName { get; set; } = string.Empty;

        [MaxLength(120, ErrorMessage = "Tên Linh mục không vượt quá 120 ký tự.")]
        public string? PriestName { get; set; }

        [MaxLength(120, ErrorMessage = "Tên Người đỡ đầu không vượt quá 120 ký tự.")]
        public string? GodparentName { get; set; }

        [MaxLength(100, ErrorMessage = "Số sổ bộ không vượt quá 100 ký tự.")]
        public string? RegistryBookNumber { get; set; }

        [MaxLength(500, ErrorMessage = "Ghi chú không vượt quá 500 ký tự.")]
        public string? Notes { get; set; }
    }

    public class DeleteSacramentRecordRequestDto
    {
        [Required]
        public long RecordId { get; set; }

        [Required]
        public long StudentId { get; set; }
    }
}