namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class StudentGradeRowViewModel
    {
        public int StudentId { get; set; }
        public string HolyName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int Semester { get; set; }
        public Dictionary<string, decimal?> ScoresByCode { get; set; } = new();
        public decimal? SemesterAverage { get; set; }
        public string Classification { get; set; } = string.Empty;
    }
}
