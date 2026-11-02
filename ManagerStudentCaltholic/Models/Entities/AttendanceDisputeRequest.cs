using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("AttendanceDisputeRequests")]
    public class AttendanceDisputeRequest
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long EnrollmentId { get; set; }

        [ForeignKey(nameof(EnrollmentId))]
        public Enrollment? Enrollment { get; set; }

        [Required]
        public long StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student? Student { get; set; }

        [Required]
        public int ClassRoomId { get; set; }

        [ForeignKey(nameof(ClassRoomId))]
        public ClassRoom? ClassRoom { get; set; }

        /// <summary>
        /// Nếu báo sai sót cho ngày trong Chiến dịch đi lễ 1 tháng thì lưu CampaignId
        /// </summary>
        public long? CampaignId { get; set; }

        [ForeignKey(nameof(CampaignId))]
        public MassCampaign? Campaign { get; set; }

        /// <summary>
        /// Ngày điểm danh cần đính chính (UTC Date)
        /// </summary>
        [Required]
        public DateTime AttendanceDate { get; set; }

        /// <summary>
        /// REGULAR_MASS_SUN: Lễ Chúa Nhật | REGULAR_MASS_THU: Lễ Thứ Năm | REGULAR_CLASS: Học Giáo Lý | CAMPAIGN_MASS: Lễ Chiến dịch 1 tháng
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string SessionScope { get; set; } = "REGULAR_MASS_SUN";

        /// <summary>
        /// Trạng thái đang ghi nhận trên hệ thống lúc phụ huynh báo (VD: ABSENT_UNPERMITTED, LATE, NONE)
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string CurrentRecordedStatus { get; set; } = "ABSENT_UNPERMITTED";

        /// <summary>
        /// Trạng thái phụ huynh đề nghị cập nhật lại: PRESENT | LINED_UP | LATE | ABSENT_PERMITTED
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string RequestedStatus { get; set; } = "PRESENT";

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ParentPhoneLast4 { get; set; }

        /// <summary>
        /// PENDING: Đang chờ GLV duyệt | APPROVED: Đã chấp nhận & sửa điểm danh | REJECTED: Đã từ chối
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "PENDING";

        [MaxLength(500)]
        public string? TeacherResponseNote { get; set; }

        [MaxLength(100)]
        public string? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}