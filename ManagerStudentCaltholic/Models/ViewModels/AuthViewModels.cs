using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public static class PasswordPolicy
    {
        public const string ComplexityPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$";
        public const string ComplexityErrorMessage = "Mật khẩu phải từ 8 ký tự trở lên, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt.";
    }

    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = true;
        public string? ReturnUrl { get; set; }
    }

    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public UserInfoDto? User { get; set; }
    }

    public class UserInfoDto
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class CreateUserRequestDto
    {
        private string? _christianName;
        private string? _email;
        private string? _phoneNumber;
        private string? _managedGradeLevel;
        private string? _oldStudentCode;

        [Required(ErrorMessage = "Tên đăng nhập không được rỗng")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username từ 3 đến 50 ký tự")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên Thánh không được để trống")]
        [StringLength(50, ErrorMessage = "Tên Thánh không quá 50 ký tự")]
        public string? ChristianName
        {
            get => _christianName;
            set => _christianName = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [Required(ErrorMessage = "Họ và tên đệm không được trống")]
        [StringLength(100, ErrorMessage = "Họ và tên đệm không quá 100 ký tự")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên không được trống")]
        [StringLength(50, ErrorMessage = "Tên không quá 50 ký tự")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100, ErrorMessage = "Email không quá 100 ký tự")]
        public string? Email
        {
            get => _email;
            set => _email = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [StringLength(15, ErrorMessage = "Số điện thoại không quá 15 ký tự")]
        public string? PhoneNumber
        {
            get => _phoneNumber;
            set => _phoneNumber = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        public string Role { get; set; } = "Teacher";

        [StringLength(50, ErrorMessage = "Khối phụ trách không quá 50 ký tự")]
        public string? ManagedGradeLevel
        {
            get => _managedGradeLevel;
            set => _managedGradeLevel = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [StringLength(50, ErrorMessage = "Mã học sinh cũ không quá 50 ký tự")]
        public string? OldStudentCode
        {
            get => _oldStudentCode;
            set => _oldStudentCode = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public class ResetPasswordRequestDto
    {
        [Required]
        public long UserId { get; set; }

        [Required(ErrorMessage = "Mật khẩu mới không được rỗng")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        public string NewPassword { get; set; } = string.Empty;
    }
}