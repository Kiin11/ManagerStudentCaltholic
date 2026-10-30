using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.DTOs
{

    public class ApproveSacramentBatchRequestDto
    {
        [Required]
        public long BatchId { get; set; }

    }
}
