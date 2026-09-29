using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class StudentViewModel
    {
        public long Id { get; set; }

        [Display(Name = "Mã Thiếu Nhi / QR")]
        public string? StudentCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Tên thánh")]
        [StringLength(50, ErrorMessage = "Tên thánh không quá 50 ký tự")]
        [Display(Name = "Tên Thánh")]
        public string ChristianName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Họ và Tên đệm")]
        [StringLength(90, ErrorMessage = "Họ và tên đệm không quá 90 ký tự")]
        [Display(Name = "Họ và Tên Đệm")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Tên")]
        [StringLength(10, ErrorMessage = "Tên không quá 10 ký tự")]
        [Display(Name = "Tên")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Họ và Tên")]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [Required(ErrorMessage = "Vui lòng chọn Giới tính")]
        [Display(Name = "Giới Tính")]
        public string Gender { get; set; } = "Nam";

        [Required(ErrorMessage = "Vui lòng nhập Ngày sinh")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày Sinh")]
        public DateTime DateOfBirth { get; set; } = DateTime.UtcNow.AddYears(-10);

        [DataType(DataType.Date)]
        [Display(Name = "Ngày Rửa Tội")]
        public DateTime? BaptismDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Ngày Thêm Sức")]
        public DateTime? ConfirmationDate { get; set; }

        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ (VD: 0912345678)")]
        [Display(Name = "SĐT Phụ Huynh")]
        public string? ParentPhone { get; set; }

        [Display(Name = "Trạng Thái")]
        public bool IsActive { get; set; } = true;
    }
}
