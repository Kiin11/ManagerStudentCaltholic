using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Kế hoạch bài giảng chi tiết từng tuần của lớp học trong niên khóa (TASK-904)
    /// </summary>
    public class ClassLessonPlan
    {
        public long Id { get; set; }

        public int ClassRoomId { get; set; }
        public ClassRoom ClassRoom { get; set; } = null!;

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        public DateTime LessonDate { get; set; } // Ngày dạy (Chúa Nhật)

        [MaxLength(100)]
        public string? LiturgicalDay { get; set; } // Ví dụ: "CN 24 TN - Khai giảng"

        [MaxLength(50)]
        public string? LessonCode { get; set; } // "BÀI 1", "BÀI 2", "KT 1T", "ÔN TẬP"

        [Required, MaxLength(255)]
        public string TopicTitle { get; set; } = string.Empty; // Tiêu đề bài học kèm trích dẫn Kinh Thánh

        public string? KeyPoints { get; set; } // Ý chính bài học

        public long? AssignedTeacherUserId { get; set; } // GLV phụ trách chính buổi này (Users.Id)
        public User? AssignedTeacherUser { get; set; }

        [MaxLength(100)]
        public string? AssignedTeacherName { get; set; } // Text hiển thị nhanh: "Anh Toàn", "Anh Hiệp"...

        [MaxLength(500)]
        public string? EventNotes { get; set; } // Ghi chú sự kiện: Trung Thu, Giáng Sinh, Họp phụ huynh...

        public bool IsExamDay { get; set; } = false; // Đánh dấu ngày kiểm tra / thi
        public bool IsDayOff { get; set; } = false; // Nghỉ lễ / Không học
    }
}
