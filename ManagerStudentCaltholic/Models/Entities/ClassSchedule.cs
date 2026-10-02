using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Lịch giảng dạy chi tiết theo phòng và thời gian cho lớp học (TASK-904)
    /// </summary>
    public class ClassSchedule
    {
        public long Id { get; set; }

        public int ClassRoomId { get; set; }
        public ClassRoom ClassRoom { get; set; } = null!;

        public int ClassRoomLocationId { get; set; }
        public ClassRoomLocation ClassRoomLocation { get; set; } = null!;

        // Thứ trong tuần (0: Chúa Nhật, 4: Thứ Năm...)
        public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Sunday;

        // Giờ bắt đầu và kết thúc tiết học (TimeSpan ánh xạ sang kiểu TIME trong Postgres)
        public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);

        public TimeSpan EndTime { get; set; } = new TimeSpan(10, 30, 0);

        [MaxLength(255)]
        public string? Notes { get; set; }

        [MaxLength(20)]
        public string Shift { get; set; } = string.Empty; // Ca học: Sáng, Chiều, Tối
    }
}
