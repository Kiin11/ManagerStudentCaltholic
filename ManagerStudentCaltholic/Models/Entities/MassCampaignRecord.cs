using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("MassCampaignRecords")]
    public class MassCampaignRecord
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long CampaignId { get; set; }

        [ForeignKey(nameof(CampaignId))]
        public MassCampaign? Campaign { get; set; }

        [Required]
        public long StudentId { get; set; }

        [ForeignKey(nameof(StudentId))]
        public Student? Student { get; set; }

        [Required]
        public long EnrollmentId { get; set; }

        [ForeignKey(nameof(EnrollmentId))]
        public Enrollment? Enrollment { get; set; }

        /// <summary>
        /// Ngày đi lễ trong chiến dịch (Date-only UTC/Local chuẩn hóa)
        /// </summary>
        [Required]
        public DateTime AttendanceDate { get; set; }

        /// <summary>
        /// PRESENT: Có mặt đúng giờ | LATE: Đi trễ | EXCUSED: Vắng có phép | ABSENT: Vắng không phép
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "PRESENT";

        /// <summary>
        /// QR_SCAN: Quét mã QR | MANUAL_GRID: GLV tích trên lưới | BULK_DAY: Điểm danh cả lớp | DISPUTE_FIX: Sửa theo báo sai sót
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string CheckInMethod { get; set; } = "MANUAL_GRID";

        public DateTime? CheckInTime { get; set; }

        [MaxLength(300)]
        public string? Note { get; set; }

        [Required]
        [MaxLength(100)]
        public string LastModifiedBy { get; set; } = string.Empty;

        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
    }
}