using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class GradeManagementViewModel
    {
        public string AcademicYear { get; set; } = "2025-2026";
        public int Semester { get; set; } = 1;
        public int ClassId { get; set; }
        public List<GradeColumn> Columns { get; set; } = new();
        public List<StudentGradeRowViewModel> Rows { get; set; } = new();
    }
}
