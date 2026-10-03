namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Cấu hình danh mục các cột điểm theo Niên khóa và Khối lớp (TASK-1001, TASK-1002)
    /// </summary>
    public class GradeConfiguration
    {
        public int Id { get; set; }

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        // Áp dụng cho khối: "Khai Tâm", "Rước Lễ", "Thêm Sức", "Bao Đồng" hoặc "ALL"
        [Required, MaxLength(50)]
        public string GradeLevel { get; set; } = "ALL";

        // Tùy chọn: nếu cột điểm chỉ đặc thù cho 1 lớp cụ thể (vd: Rước Lễ 2A, Thêm Sức 3B)
        public int? ClassRoomId { get; set; }
        public ClassRoom? ClassRoom { get; set; }

        // Học kỳ: 1 (HK1), 2 (HK2), hoặc 0 (Cả năm)
        public int Semester { get; set; } = 1;

        // Mã loại cột: "15_MIN", "45_MIN", "SEMESTER_EXAM", "SACRAMENT_ORAL", "PRAYER"
        [Required, MaxLength(30)]
        public string ScoreType { get; set; } = "15_MIN";

        // Tên hiển thị trên bảng điểm: "15 phút Lần 1", "Kiểm tra 1 tiết", "Vấn đáp Bí tích"...
        [Required, MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        // Hệ số trọng số tính điểm trung bình (thường: 15p = 1, 1T = 2, Thi HK = 3)
        public int WeightFactor { get; set; } = 1;

        // Thứ tự cột hiển thị (từ trái qua phải trên Grid View)
        public int ColumnIndex { get; set; } = 1;

        // Đánh dấu cột điểm dành riêng cho khảo hạch Bí tích (RL2, TS3, BD3)
        public bool IsSacramentExam { get; set; } = false;

        // Cột bắt buộc phải nhập điểm để đủ điều kiện xét bí tích/lên lớp
        public bool IsRequired { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<GradeRecord> GradeRecords { get; set; } = new List<GradeRecord>();
    }
}
