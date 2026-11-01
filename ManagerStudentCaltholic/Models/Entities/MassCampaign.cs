using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ManagerStudentCaltholic.Interface.Models;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("MassCampaigns")]
    public class MassCampaign : ISoftDelete
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int AcademicYearId { get; set; }

        [ForeignKey(nameof(AcademicYearId))]
        public AcademicYear? AcademicYear { get; set; }

        [Required]
        public int ClassRoomId { get; set; }

        [ForeignKey(nameof(ClassRoomId))]
        public ClassRoom? ClassRoom { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Ngày bắt đầu chiến dịch (chỉ lấy phần Date)
        /// </summary>
        [Required]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Ngày kết thúc chiến dịch (kéo dài 28 - 31 ngày liên tục)
        /// </summary>
        [Required]
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Danh sách các thứ áp dụng trong tuần (0=CN, 1=T2, ..., 6=T7). Mặc định: "0,1,2,3,4,5,6" (tất cả các ngày)
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string ApplicableDaysCsv { get; set; } = "0,1,2,3,4,5,6";

        /// <summary>
        /// Số buổi đi lễ tối thiểu cần đạt trong chiến dịch (VD: 25 buổi)
        /// </summary>
        public int TargetDaysCount { get; set; } = 25;

        /// <summary>
        /// ACTIVE: Đang diễn ra | COMPLETED: Đã hoàn tất | CANCELLED: Đã hủy
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "ACTIVE";

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public ICollection<MassCampaignRecord> Records { get; set; } = new List<MassCampaignRecord>();
    }
}