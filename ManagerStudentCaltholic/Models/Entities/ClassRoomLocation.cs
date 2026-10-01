using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Phòng học cụ thể kèm tầng, sức chứa và liên kết với lớp học
    /// </summary>
    public class ClassRoomLocation
    {
        public int Id { get; set; }

        public int BuildingZoneId { get; set; }
        public BuildingZone BuildingZone { get; set; } = null!;

        [Required, MaxLength(50)]
        public string RoomName { get; set; } = string.Empty; // Ví dụ: "Phòng 101", "Hội trường C"

        public int FloorNumber { get; set; } = 1; // Tầng trệt: 0 hoặc 1, Lầu 1: 2...

        public int Capacity { get; set; } = 40; // Sức chứa tiêu chuẩn

        [MaxLength(100)]
        public string? EquipmentNotes { get; set; } // Ví dụ: "Máy chiếu, 15 bàn đôi, 2 quạt"

        public bool IsAvailable { get; set; } = true; // Trạng thái sẵn sàng sử dụng
    }
}
