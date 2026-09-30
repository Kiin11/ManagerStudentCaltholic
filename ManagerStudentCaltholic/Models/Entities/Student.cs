using ManagerStudentCaltholic.Interface.Models;
using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class Student : ISoftDelete
    {
        public long Id { get; set; }

        [Required, MaxLength(20)]
        public string StudentCode { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string ChristianName { get; set; } = string.Empty;

        [Required, MaxLength(90)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Gender { get; set; } = "Nam";

        public DateTime DateOfBirth { get; set; }
        public DateTime? BaptismDate { get; set; }          // Ngày Rửa tội
        public DateTime? ConfirmationDate { get; set; } // Ngày Thêm sức

        [MaxLength(15)]
        public string? ParentPhone { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
