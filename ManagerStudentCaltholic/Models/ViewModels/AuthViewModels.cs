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

        public bool RememberMe { get; set; } = false;
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
        [Required(ErrorMessage = "Tên đăng nhập không được rỗng")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username từ 3 đến 50 ký tự")]
        public string Username { get; set; } = string.Empty;

        // SEC-NEW-10 FIX: Áp dụng chuẩn độ phức tạp mật khẩu đồng nhất
        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")]
        [RegularExpression(PasswordPolicy.ComplexityPattern, ErrorMessage = PasswordPolicy.ComplexityErrorMessage)]
        public string Password { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ChristianName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ và tên đệm không được trống")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên không được trống")]
        [StringLength(10)]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        public string Role { get; set; } = "Teacher";

        public string? ManagedGradeLevel { get; set; }
    }

    public class ResetPasswordRequestDto
    {
        [Required]
        public long UserId { get; set; }

        // SEC-NEW-10 FIX: Nâng từ 6 ký tự lên tối thiểu 8 ký tự kèm kiểm tra độ phức tạp
        [Required(ErrorMessage = "Mật khẩu mới không được rỗng")]
        [MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")]
        [RegularExpression(PasswordPolicy.ComplexityPattern, ErrorMessage = PasswordPolicy.ComplexityErrorMessage)]
        public string NewPassword { get; set; } = string.Empty;
    }
}