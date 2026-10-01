using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Khu vực / Dãy nhà (Ví dụ: Khu A - Nhà Xứ, Khu B - Nhà Mục Vụ, Khu C - Dãy Hoa Viên)
    /// </summary>
    public class BuildingZone
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string ZoneName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ZoneCode { get; set; } // A, B, C...

        [MaxLength(255)]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; } = 1;

        public ICollection<ClassRoomLocation> Rooms { get; set; } = new List<ClassRoomLocation>();
    }
}
