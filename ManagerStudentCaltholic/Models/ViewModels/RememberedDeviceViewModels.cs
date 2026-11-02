namespace ManagerStudentCaltholic.Models.ViewModels
{
    /// <summary>
    /// Cấu trúc lưu trong Cookie được mã hóa bằng IDataProtectionProvider trên thiết bị của Phụ huynh/Học sinh
    /// </summary>
    public class RememberedStudentCookieItem
    {
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int ClassRoomId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public int AcademicYearId { get; set; }
        public DateTime LastQueriedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Thông báo của Lớp / Khối hiển thị trên màn hình HomePage cho thiết bị đã tra cứu học sinh
    /// </summary>
    public class StudentClassAnnouncementItemDto
    {
        public long AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Scope { get; set; } = "CLASS"; // CLASS | GRADE | ALL
        public string Priority { get; set; } = "NORMAL";
        public int? TargetClassRoomId { get; set; }
        public string? TargetGradeLevel { get; set; }
        public string TargetDisplayBadge { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public DateTime CreatedAt { get; set; }
        public string AuthorName { get; set; } = "Giáo lý viên";
    }

    public class DeviceDetectedStudentContextViewModel
    {
        public bool HasRememberedStudents => RememberedStudents.Any();
        public List<RememberedStudentCookieItem> RememberedStudents { get; set; } = new();
        public List<StudentClassAnnouncementItemDto> ClassAnnouncements { get; set; } = new();
    }
}