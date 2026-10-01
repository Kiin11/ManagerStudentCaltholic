using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class Announcement
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "Tiêu đề thông báo không được để trống")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nội dung thông báo không được để trống")]
        public string Content { get; set; } = string.Empty;

        // Phạm vi thông báo: ALL (Toàn xứ), GRADE (Theo khối), CLASS (Theo lớp)
        [Required]
        [MaxLength(20)]
        public string Scope { get; set; } = "ALL";

        // Áp dụng nếu Scope = GRADE (Khai Tâm, Rước Lễ, Thêm Sức, Bao Đồng)
        [MaxLength(50)]
        public string? TargetGradeLevel { get; set; }

        // Áp dụng nếu Scope = CLASS
        public int? TargetClassRoomId { get; set; }
        public ClassRoom? TargetClassRoom { get; set; }

        // Mức độ ưu tiên / khẩn cấp: NORMAL, IMPORTANT, URGENT
        [Required]
        [MaxLength(20)]
        public string Priority { get; set; } = "NORMAL";

        public bool IsPinned { get; set; } = false; // Ghim lên đầu bảng tin
        public bool IsActive { get; set; } = true;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty; // Username hoặc họ tên người đăng

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpireAt { get; set; } // Ngày hết hạn thông báo (tùy chọn)
    }
}
