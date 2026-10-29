using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class AdminRegistrationDashboardViewModel
    {
        public string StatusFilter { get; set; } = "ALL";
        public string? GradeFilter { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int CurrentAcademicYearId { get; set; }
        public string CurrentAcademicYearName { get; set; } = string.Empty;

        public int TotalPending { get; set; }
        public int TotalApproved { get; set; }
        public int TotalRejected { get; set; }

        public List<StudentRegistrationRequest> Requests { get; set; } = new();
        public List<ClassRoom> AvailableClasses { get; set; } = new();
    }
}
