using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
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
    }

    public class AssignedClassItemDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
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

    // DTO Đổi mật khẩu
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu không trùng khớp")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
