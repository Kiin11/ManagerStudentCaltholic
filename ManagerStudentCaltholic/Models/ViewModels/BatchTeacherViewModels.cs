using System.ComponentModel.DataAnnotations;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class BatchTeacherItemDto
    {
        [Required(ErrorMessage = "Thiếu Tên Thánh")]
        public string ChristianName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Thiếu Họ và Tên đệm")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Thiếu Tên chính")]
        public string LastName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }

        // Khối dự kiến phân công: Khai Tâm, Rước Lễ, Thêm Sức, Bao Đồng (nếu có)
        public string? ManagedGradeLevel { get; set; }
    }

    public class BatchCreateTeachersRequestDto
    {
        public string DefaultPassword { get; set; } = "GLV@2026!"; // Mật khẩu khởi tạo chung
        public List<BatchTeacherItemDto> Teachers { get; set; } = new();
    }

    public class CreatedTeacherResultItem
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string InitialPassword { get; set; } = string.Empty;
        public string? ManagedGradeLevel { get; set; }
    }

    public class BatchCreateTeachersResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalCreated { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<CreatedTeacherResultItem> CreatedTeachers { get; set; } = new();
    }

    // DTO gửi yêu cầu vô hiệu hóa hàng loạt
    public class BatchDeactivateTeachersRequestDto
    {
        public List<long> UserIds { get; set; } = new();
        public string? Reason { get; set; } = "Nghỉ dạy / Tạm ngưng sinh hoạt niên khóa mới";
    }

    // DTO trả về thông tin GLV chưa phân công lớp
    public class UnassignedTeacherItemDto
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? ManagedGradeLevel { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
