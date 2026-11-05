using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Kế hoạch phân phối chương trình và bài học hiện hành của lớp học trong niên khóa (FEAT-P5-07)
    /// </summary>
    public class ClassCurriculumDocument
    {
        public long Id { get; set; }

        public int ClassRoomId { get; set; }
        public ClassRoom ClassRoom { get; set; } = null!;

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        [Required, MaxLength(255)]
        public string Title { get; set; } = "Kế hoạch năm học & Phân phối chương trình";

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required, MaxLength(50)]
        public string CurrentVersion { get; set; } = "v1.0";

        public int VersionNumber { get; set; } = 1;

        /// <summary>
        /// Chuỗi JSON lưu toàn bộ danh sách các tuần bài học hiện hành để nạp nhanh lên web
        /// </summary>
        [Column(TypeName = "text")]
        public string? ContentJson { get; set; }

        /// <summary>
        /// Tên file đính kèm tự động sinh tương ứng phiên bản hiện hành (VD: KeHoach_TS3A_v2_20261105.xlsx)
        /// </summary>
        [MaxLength(255)]
        public string? LastAttachmentFileName { get; set; }

        [MaxLength(500)]
        public string? LastAttachmentFilePath { get; set; }

        public long LastAttachmentFileSize { get; set; }

        [MaxLength(100)]
        public string? LastAttachmentContentType { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public long? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        public long? UpdatedByUserId { get; set; }
        public User? UpdatedByUser { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public ICollection<ClassCurriculumVersion> Versions { get; set; } = new List<ClassCurriculumVersion>();
        public ICollection<ClassLessonPlan> LessonPlans { get; set; } = new List<ClassLessonPlan>();
    }
}
