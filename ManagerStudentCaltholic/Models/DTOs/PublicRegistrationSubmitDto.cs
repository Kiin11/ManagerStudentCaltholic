using System.ComponentModel.DataAnnotations;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class PublicRegistrationSubmitDto
    {
        [Required(ErrorMessage = "Vui lòng nhập Tên Thánh")]
        [MaxLength(50)]
        public string ChristianName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Họ và tên đệm")]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Tên thiếu nhi")]
        [MaxLength(20)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Gender { get; set; } = "Nam";

        [Required(ErrorMessage = "Vui lòng chọn Ngày sinh")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [DataType(DataType.Date)]
        public DateTime? BaptismDate { get; set; }

        [MaxLength(100)]
        public string? BaptismParish { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ tên Cha/Mẹ hoặc Người giám hộ")]
        [MaxLength(100)]
        public string ParentFullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Số điện thoại liên hệ")]
        [RegularExpression(@"^(0|\+84)[0-9]{8,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
        public string ParentPhone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Địa chỉ Email không hợp lệ")]
        [MaxLength(100)]
        public string? ParentEmail { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? ParishSubDivision { get; set; }

        [Required]
        public string RegistrationType { get; set; } = "NEW"; // NEW | TRANSFER

        [Required]
        public string TargetGradeLevel { get; set; } = "Khai Tâm";

        /// <summary>
        /// Honeypot field chống Bot tự động điền form (TASK-P3-02.5)
        /// </summary>
        public string? WebsiteUrlHoneypot { get; set; }

        public IFormFile? CertificateFile { get; set; }
    }

}