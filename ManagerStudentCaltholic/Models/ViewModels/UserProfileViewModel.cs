using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    /// <summary>
    /// ViewModel Profile tổng hợp (hỗ trợ cả Lịch sử Kép Học & Dạy và 5 Cột mốc Đức tin - TASK-04.1)
    /// </summary>
    public class UserProfileViewModel
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Tên Thánh")]
        [StringLength(50)]
        public string? ChristianName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ và Tên")]
        [StringLength(100)]
        [Display(Name = "Họ")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Tên")]
        [StringLength(10)]
        [Display(Name = "Tên")]
        public string LastName { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Ngày Sinh")]
        public DateTime? DateOfBirth { get; set; }

        [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ")]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không đúng định dạng")]
        [Display(Name = "Số Điện Thoại")]
        public string? PhoneNumber { get; set; }

        [StringLength(200)]
        [Display(Name = "Giáo Khu / Địa Chỉ")]
        public string? Address { get; set; }

        public string? AvatarUrl { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // TASK-813: Các lớp đang dạy trong niên khóa hiện tại
        public List<AssignedClassItemDto> CurrentClasses { get; set; } = new();

        // TASK-813: Lịch sử giảng dạy gom theo các niên khóa trước
        public List<TeachingHistoryByYearDto> TeachingHistory { get; set; } = new();

        public string? ManagedGradeLevel { get; set; }

        // TASK-04.1 / TASK-04.2 / TASK-04.3 (Phase 6): Hồ sơ Thiếu nhi liên kết, Timeline kép & 5 Cột mốc Đức tin
        public long? LinkedStudentId { get; set; }
        public string? LinkedStudentCode { get; set; }
        public DateTime? StudentDateOfBirth { get; set; }
        public string? StudentGender { get; set; }
        public string? ParentPhone { get; set; }
        public bool IsStudentActive { get; set; } = true;
        public List<ProfileDualYearTimelineDto> DualTimeline { get; set; } = new();
        public List<FaithMilestoneViewModel> FaithMilestones { get; set; } = new();
        public bool IsUnder18CatechistWarning { get; set; }
        public string? CatechistWarningMessage { get; set; }
    }

    /// <summary>
    /// Alias tương thích với tên gọi ProfileHistoryViewModel trong đặc tả TASK-04.1
    /// </summary>
    public class ProfileHistoryViewModel : UserProfileViewModel
    {
    }

    public class AssignedClassItemDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Session { get; set; } = "Morning";
        public string SessionDisplay { get; set; } = "Ca Sáng";
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public bool IsCurrentYear { get; set; }
        public string RoleInClass { get; set; } = "HEAD";
        public string RoleName { get; set; } = "Chủ nhiệm";
        public string? RoomNumber { get; set; }
        public int TotalStudents { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    public class TeachingHistoryByYearDto
    {
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public List<AssignedClassItemDto> Classes { get; set; } = new();
    }

    /// <summary>
    /// Vế 1 trong Timeline kép: Lớp đang học qua từng niên khóa (Khai Tâm -> Bao Đồng -> Dự Bị)
    /// </summary>
    public class StudentLearningHistoryItemDto
    {
        public long EnrollmentId { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Session { get; set; } = "Morning";
        public string SessionDisplay { get; set; } = "Ca Sáng";
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public bool IsCurrentYear { get; set; }
        public decimal? Semester1Average { get; set; }
        public decimal? Semester2Average { get; set; }
        public decimal? YearAverage { get; set; }
        public string AcademicRank { get; set; } = "Chưa xếp loại";
        public bool? IsPassed { get; set; }
    }

    /// <summary>
    /// Thẻ niên khóa chứa song song 2 vế: Lớp đang học (Vế 1) & Lớp đang dạy (Vế 2)
    /// </summary>
    public class ProfileDualYearTimelineDto
    {
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public bool IsCurrentYear { get; set; }
        public List<StudentLearningHistoryItemDto> LearningClasses { get; set; } = new();
        public List<AssignedClassItemDto> TeachingClasses { get; set; } = new();
    }

    /// <summary>
    /// Mô hình hiển thị 1 trong 5 Cột mốc Đức tin & Tuyên hứa
    /// </summary>
    public class FaithMilestoneViewModel
    {
        public int StepNumber { get; set; }
        public string MilestoneName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public DateTime? EventDate { get; set; }
        public int? AgeAtMilestone { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsWarning { get; set; }
        public string? WarningMessage { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    // DTO Đổi mật khẩu
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        // SEC-09 FIX: Yêu cầu tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [MinLength(8, ErrorMessage = "Mật khẩu mới phải có tối thiểu 8 ký tự")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
            ErrorMessage = "Mật khẩu phải từ 8 ký tự trở lên, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu không trùng khớp")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
