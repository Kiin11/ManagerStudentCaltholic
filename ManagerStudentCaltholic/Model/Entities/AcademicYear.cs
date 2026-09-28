using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Model.Entities
{
    public class AcademicYear
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = "2026-2027";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsCurrent { get; set; } = true;

        public ICollection<ClassRoom> Classes { get; set; } = new List<ClassRoom>();
    }
}
