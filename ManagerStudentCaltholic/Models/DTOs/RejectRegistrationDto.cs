using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.DTOs
{
    public class RejectRegistrationDto
    {
        [Required]
        public long RequestId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do từ chối hoặc yêu cầu bổ sung hồ sơ")]
        [MaxLength(500)]
        public string RejectionReason { get; set; } = string.Empty;
    }
}
