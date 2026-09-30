using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class RefreshToken
    {
        public long Id { get; set; }

        public long UserId { get; set; }
        public User User { get; set; } = null!;

        [Required, MaxLength(255)]
        public string Token { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(45)]
        public string? CreatedByIp { get; set; }

        public DateTime? RevokedAt { get; set; }

        [MaxLength(45)]
        public string? RevokedByIp { get; set; }

        [MaxLength(255)]
        public string? ReplacedByToken { get; set; }

        // Token hợp lệ khi chưa hết hạn và chưa bị thu hồi
        public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    }
}
