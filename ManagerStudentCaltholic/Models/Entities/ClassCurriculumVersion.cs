using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Lịch sử từng phiên bản ngày sửa của Kế hoạch lớp (FEAT-P5-07 / TASK-P5-07.1)
    /// </summary>
    public class ClassCurriculumVersion
    {
        public long Id { get; set; }

        public long ClassCurriculumDocumentId { get; set; }
        public ClassCurriculumDocument CurriculumDocument { get; set; } = null!;

        public int VersionNumber { get; set; } = 1;

        [Required, MaxLength(50)]
        public string VersionTag { get; set; } = "v1";

        [MaxLength(500)]
        public string? ChangeSummary { get; set; }

        /// <summary>
        /// Bản chụp toàn bộ danh sách bài học tuần tại thời điểm lưu phiên bản này (JSON)
        /// </summary>
        [Column(TypeName = "text")]
        public string? ContentJson { get; set; }

        /// <summary>
        /// Tên file đính kèm tự động sinh theo cú pháp: KeHoach_{Class}_{Version}_{YYYYMMDD}.xlsx
        /// </summary>
        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [MaxLength(100)]
        public string? ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        [Required, MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public long? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
