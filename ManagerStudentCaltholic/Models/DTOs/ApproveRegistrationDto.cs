using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.DTOs
{
    public class ApproveRegistrationDto
    {
        [Required]
        public long RequestId { get; set; }

        /// <summary>
        /// Nếu để null hoặc 0, hệ thống tự động gọi StudentPlacementService để xếp lớp cân bằng sĩ số
        /// </summary>
        public int? TargetClassId { get; set; }
    }
}
