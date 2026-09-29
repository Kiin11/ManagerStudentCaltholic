using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class AttendanceAuditLog
    {
        public long Id { get; set; }

        public long AttendanceId { get; set; }
        public Attendance Attendance { get; set; } = null!;

        [Required, MaxLength(50)]
        public string ActionType { get; set; } = string.Empty; // UPDATE_MASS, UPDATE_CLASS, MAKE_UP, EXCUSE_LEAVE

        // Trạng thái / Dữ liệu trước khi sửa
        public string? OldValues { get; set; }

        // Trạng thái / Dữ liệu sau khi sửa
        public string? NewValues { get; set; }

        // Mã hoặc Tên người thao tác (GLV, Phân đoàn trưởng, Ban Quản trị)
        [MaxLength(100)]
        public string ModifiedBy { get; set; } = "SYSTEM";

        // Lý do chỉnh sửa
        [MaxLength(255)]
        public string? Reason { get; set; }

        // Địa chỉ IP của máy thao tác
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
