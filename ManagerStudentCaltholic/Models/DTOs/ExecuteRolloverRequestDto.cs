using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.DTOs
{
    public class ExecuteRolloverRequestDto
    {
        [Required]
        public int SourceClassId { get; set; }

        [Required]
        public int TargetYearId { get; set; }

        // Lớp đích tại niên khóa mới dành cho học sinh ĐƯỢC LÊN LỚP
        public int? PromoteToClassId { get; set; }

        // Lớp đích tại niên khóa mới dành cho học sinh Ở LẠI LỚP
        public int? RetainInClassId { get; set; }

        public List<StudentRolloverDecisionItem> Decisions { get; set; } = new();
    }

    public class StudentRolloverDecisionItem
    {
        public long StudentId { get; set; }
        public bool IsPromoted { get; set; } // True: vào PromoteToClassId, False: vào RetainInClassId
        public bool Skip { get; set; }       // Bỏ qua nếu không muốn xếp lớp
    }
}
