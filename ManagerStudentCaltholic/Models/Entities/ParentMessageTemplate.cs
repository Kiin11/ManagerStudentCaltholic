using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("parent_message_templates")]
    public class ParentMessageTemplate
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required, MaxLength(150)]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [Column("category")]
        public string Category { get; set; } = "CHUYEN_CAN"; // Nhóm dịp: CHUYEN_CAN, THI_CU, LE_PHUNG_VU, HOP_PHU_HUYNH, DONG_QUY

        [Required, MaxLength(50)]
        [Column("scope")]
        public string Scope { get; set; } = "ALL"; // Khối áp dụng: ALL, Khai Tâm, Rước Lễ, Thêm Sức, Bao Đồng

        [Required]
        [Column("content_template")]
        public string ContentTemplate { get; set; } = string.Empty;
        // Ví dụ: "Trân trọng kính báo Phụ huynh em {TenThieuNhi} lớp {TenLop} đã vắng mặt..."

        [MaxLength(100)]
        [Column("created_by")]
        public string? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}