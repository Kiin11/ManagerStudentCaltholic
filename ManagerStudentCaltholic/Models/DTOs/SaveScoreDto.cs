namespace ManagerStudentCaltholic.Models.DTOs
{
    public class SaveScoreDto
    {
        public int StudentId { get; set; }
        public int GradeColumnId { get; set; }
        public int Semester { get; set; }
        public decimal? Score { get; set; }
    }
}
