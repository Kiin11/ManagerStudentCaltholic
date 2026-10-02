using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Báo cáo sự cố / Hư hỏng cơ sở vật chất phòng học (TASK-905)
    /// </summary>
    public class RoomIncidentReport
    {
        public long Id { get; set; }

        public int ClassRoomLocationId { get; set; }
        public ClassRoomLocation ClassRoomLocation { get; set; } = null!;

        public long ReporterUserId { get; set; }
        public User ReporterUser { get; set; } = null!;

        // Loại thiết bị: "Đèn", "Quạt trần", "Máy chiếu", "Micro / Loa", "Bàn ghế", "Khác"
        [Required, MaxLength(50)]
        public string DeviceType { get; set; } = string.Empty;

        // Mức độ nghiêm trọng: "LOW", "NORMAL", "HIGH", "CRITICAL"
        [Required, MaxLength(20)]
        public string Severity { get; set; } = "NORMAL";

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        // Trạng thái: "PENDING" (Chờ xử lý), "IN_PROGRESS" (Đang sửa), "RESOLVED" (Đã khắc phục), "CANCELLED"
        [Required, MaxLength(30)]
        public string Status { get; set; } = "PENDING";

        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        [MaxLength(255)]
        public string? ResolutionNotes { get; set; }
    }
}
