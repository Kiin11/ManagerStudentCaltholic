using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class ClassLessonDocument
    {
        public long Id { get; set; }

        public int ClassRoomId { get; set; }
        public ClassRoom ClassRoom { get; set; } = null!;

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty; // Tên gốc: KHNH_Bao_Dong_2.xlsx

        [Required, MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty; // Tên mã hóa: 20261002_guid.xlsx

        [Required, MaxLength(500)]
        public string FilePath { get; set; } = string.Empty; // Đường dẫn web: /uploads/lesson_plans/...

        [Required, MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string UploadedBy { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}