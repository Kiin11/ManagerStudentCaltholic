namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class StudentImportRowDto
    {
        public int RowIndex { get; set; }
        public string? StudentCode { get; set; }
        public string ChristianName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime? DateOfBirth { get; set; }
        public DateTime? BaptismDate { get; set; }
        public DateTime? ConfirmationDate { get; set; }
        public string? ParentPhone { get; set; }
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
    }

    public class StudentImportResultDto
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
    }
}
