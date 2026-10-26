using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class StudentLookupRequestDto
    {
        [Required(ErrorMessage = "Vui lòng nhập Mã Thiếu Nhi (hoặc mã in trên thẻ QR)")]
        [StringLength(30, ErrorMessage = "Mã thiếu nhi không vượt quá 30 ký tự")]
        [Display(Name = "Mã Thiếu Nhi")]
        public string StudentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Ngày sinh của em")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày Sinh")]
        public DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-10);

        [Required(ErrorMessage = "Vui lòng nhập 4 số cuối SĐT Phụ huynh đã đăng ký")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "Vui lòng nhập đúng 4 chữ số cuối của SĐT Phụ huynh")]
        [Display(Name = "4 Số Cuối SĐT Phụ Huynh")]
        public string PhoneLast4Digits { get; set; } = string.Empty;
    }

    public class StudentLookupResultViewModel
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public DateTime DateOfBirth { get; set; }

        // Chỉ lưu SĐT đã che (VD: 0912***678) để không bị leak PII trên HTML
        public string MaskedParentPhone { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string AcademicYearName { get; set; } = string.Empty;
        public string? RoomNumber { get; set; }
        public List<string> Teachers { get; set; } = new();

        // 1. Chuyên cần Thánh Lễ Chúa Nhật
        public int TotalSunMassSessions { get; set; }
        public int AttendedSunMassCount { get; set; }
        public int SunMassLinedUpCount { get; set; }
        public int SunMassPresentCount { get; set; }
        public int SunMassLateCount { get; set; }
        public int SunMassAbsentCount { get; set; }
        public double SunMassAttendanceRate { get; set; }

        // 2. Chuyên cần Thánh Lễ Thứ Năm
        public int TotalThuMassSessions { get; set; }
        public int AttendedThuMassCount { get; set; }
        public int ThuMassAbsentCount { get; set; }
        public double ThuMassAttendanceRate { get; set; }

        // 3. Chuyên cần Giờ Học Giáo Lý
        public int TotalClassSessions { get; set; }
        public int AttendedClassCount { get; set; }
        public int ClassLateCount { get; set; }
        public int ClassAbsentCount { get; set; }
        public double ClassAttendanceRate { get; set; }

        // Thuộc tính tổng hợp
        public int TotalMassSessions { get; set; }
        public int AttendedMassCount { get; set; }
        public double MassAttendanceRate { get; set; }

        public SemesterGradeDetail HK1 { get; set; } = new();
        public SemesterGradeDetail HK2 { get; set; } = new();
        public decimal? YearAverage { get; set; }
        public string OverallRank { get; set; } = "Chưa hoàn tất";
        public bool IsEligibleForSacrament { get; set; } = false;
        public string? QrCodeBase64 { get; set; }
    }

    public class SemesterGradeDetail
    {
        public int Semester { get; set; }
        public List<GradeItemView> Items { get; set; } = new();
        public decimal? SemesterAverage { get; set; }
        public string Rank { get; set; } = "Đang cập nhật";
    }

    public class GradeItemView
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ScoreType { get; set; } = string.Empty;
        public int WeightFactor { get; set; } = 1;
        public decimal? Score { get; set; }
        public bool IsSacramentExam { get; set; }
    }
}