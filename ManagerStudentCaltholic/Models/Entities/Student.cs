using ManagerStudentCaltholic.Interface.Models;
using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.Entities
{
    public class Student : ISoftDelete
    {
        public long Id { get; set; }

        [Required, MaxLength(50)]
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
        public DateTime? FeastDay { get; set; }             // Ngày kính Thánh Bổn mạng
        public DateTime? BaptismDate { get; set; }          // Mốc 1: Ngày Rửa tội
        public DateTime? FirstCommunionDate { get; set; }   // Mốc 2: Ngày Xưng tội & Rước lễ lần đầu
        public DateTime? ConfirmationDate { get; set; }     // Mốc 3: Ngày Thêm sức
        public DateTime? BaoDongPromiseDate { get; set; }   // Mốc 4: Ngày lãnh nhận Tuyên hứa Bao Đồng
        public DateTime? CatechistPromiseDate { get; set; } // Mốc 5: Ngày Hoàn thành Dự Bị & Tuyên hứa GLV (>= 18 tuổi)

        [MaxLength(15)]
        public string? ParentPhone { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
