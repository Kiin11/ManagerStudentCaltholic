using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class AssignStudentsRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn lớp học")]
        public int ClassId { get; set; }

        [Required(ErrorMessage = "Danh sách học sinh không được rỗng")]
        public List<long> StudentIds { get; set; } = new();
    }

    public class ClassEnrollmentViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string AcademicYearName { get; set; } = string.Empty;
        public int AcademicYearId { get; set; }

        // Danh sách học sinh hiện đã có trong lớp
        public List<EnrolledStudentItem> EnrolledStudents { get; set; } = new();

        // Danh sách học sinh chưa có lớp trong niên khóa này
        public List<UnassignedStudentItem> UnassignedStudents { get; set; } = new();
    }

    public class EnrolledStudentItem
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }
        public DateTime EnrolledAt { get; set; }
    }

    public class UnassignedStudentItem
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }
        public string? ParentPhone { get; set; }
    }
}
