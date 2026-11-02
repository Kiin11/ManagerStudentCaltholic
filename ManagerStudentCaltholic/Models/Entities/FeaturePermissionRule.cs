using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    [Table("FeaturePermissionRules")]
    public class FeaturePermissionRule
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Mã định danh duy nhất của nút chức năng (VD: ATT_QR_SCAN, ATT_MASS_CAMPAIGN, ACD_GRADEBOOK...)
        /// </summary>
        [Required]
        [MaxLength(60)]
        public string FeatureCode { get; set; } = string.Empty;

        /// <summary>
        /// Mã phân khu trên Master Taskbar: COMMON | ATTENDANCE | ACADEMIC | SACRAMENT | GOVERNANCE
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string MasterCode { get; set; } = "COMMON";

        [Required]
        [MaxLength(120)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(60)]
        public string IconClass { get; set; } = "bi-grid-fill";

        [Required]
        [MaxLength(200)]
        public string TargetUrl { get; set; } = "/";

        [MaxLength(40)]
        public string BadgeColor { get; set; } = "primary";

        /// <summary>
        /// Danh sách Role được phép thấy nút này (phân cách bằng dấu phẩy, VD: "ALL" hoặc "Admin,ExecutiveBoard,BranchHead,Teacher")
        /// </summary>
        [Required]
        [MaxLength(250)]
        public string AllowedRolesCsv { get; set; } = "ALL";

        /// <summary>
        /// Nếu bật = true, chỉ hiển thị cho người dùng đang phụ trách lớp chuẩn bị Bí tích (RL2, TS3, BĐ3, DB4) hoặc Ban Điều Hành
        /// </summary>
        public bool SacramentClassesOnly { get; set; } = false;

        /// <summary>
        /// Danh sách ID lớp cụ thể được cấp quyền riêng (tùy chọn, phân cách bởi dấu phẩy)
        /// </summary>
        [MaxLength(250)]
        public string? SpecificClassIdsCsv { get; set; }

        public int SortOrder { get; set; } = 1;

        public bool IsEnabled { get; set; } = true;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}