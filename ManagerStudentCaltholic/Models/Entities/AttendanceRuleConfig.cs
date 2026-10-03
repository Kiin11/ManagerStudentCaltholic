using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class AttendanceRuleConfig
    {
        public int Id { get; set; }

        public int ClassRoomId { get; set; }
        public ClassRoom ClassRoom { get; set; } = null!;

        // 1. Tỷ trọng chuyên cần phân tách (Tổng 3 cột = 100%)
        [Range(0, 100)]
        public int SundayMassWeightPercent { get; set; } = 40;   // Lễ Chúa Nhật (Mặc định 40%)

        [Range(0, 100)]
        public int ThursdayMassWeightPercent { get; set; } = 20; // Lễ Thứ Năm (Mặc định 20%)

        [Range(0, 100)]
        public int ClassWeightPercent { get; set; } = 40;        // Giờ Học Giáo Lý (Mặc định 40%)

        // 2. Hệ số tính khi đi trễ và vắng có phép
        [Column(TypeName = "decimal(3,2)")]
        [Range(0.00, 1.00)]
        public decimal LateMultiplier { get; set; } = 0.80m;

        [Column(TypeName = "decimal(3,2)")]
        [Range(0.00, 1.00)]
        public decimal PermittedAbsentMultiplier { get; set; } = 0.50m;

        [Column(TypeName = "decimal(3,2)")]
        [Range(0.00, 1.00)]
        public decimal MakeUpBonusRate { get; set; } = 1.00m;

        // 3. Tiêu chuẩn xét Bí tích
        [Range(0.0, 100.0)]
        public double MinAttendanceRateForSacrament { get; set; } = 80.0;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}