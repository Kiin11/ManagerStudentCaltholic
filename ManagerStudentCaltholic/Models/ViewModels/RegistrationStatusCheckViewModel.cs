using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class RegistrationStatusCheckViewModel
    {
        public string? ParentPhone { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public bool HasSearched { get; set; }
        public List<StudentRegistrationRequest> Results { get; set; } = new();
    }
}
