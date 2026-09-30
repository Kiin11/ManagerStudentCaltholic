using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class User
    {
        public long Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(15)]
        public string? PhoneNumber { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        // Vai trò: Admin, BranchHead, Teacher, Parent
        [Required, MaxLength(30)]
        public string Role { get; set; } = UserRole.Teacher;

        public bool IsActive { get; set; } = true;

        // Phục vụ cơ chế bảo vệ đăng nhập và khóa tài khoản (Epic 7 tương thích sẵn)
        public int AccessFailedCount { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Quan hệ với các RefreshToken
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}
