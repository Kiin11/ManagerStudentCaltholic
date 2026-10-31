using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;

namespace ManagerStudentCaltholic.Models.DTOs
{
    /// <summary>
    /// TASK-P3-12.1: Quy tắc đặt tên khóa chuẩn trên Redis cho Điểm danh & Giờ học
    /// </summary>
    public static class RedisAttendanceKeys
    {
        public static string ClassSheet(int classId, DateTime date) =>
            $"ATT:CLASS:{classId}:DATE:{date:yyyy-MM-dd}";

        public static string QrStudentLookup(DateTime date, string studentCode) =>
            $"ATT:QR:DATE:{date:yyyy-MM-dd}:CODE:{studentCode.Trim().ToUpperInvariant()}";

        public static string ClassSessionInfo(int classId, DateTime date) =>
            $"ATT:SESSION:CLASS:{classId}:DATE:{date:yyyy-MM-dd}";

        public static string DirtyPartitionsIndex(DateTime date) =>
            $"ATT:DIRTY:DATE:{date:yyyy-MM-dd}";
    }

    /// <summary>
    /// Cấu trúc lưu trữ Sổ điểm danh của 1 lớp trong ngày trên Redis
    /// </summary>
    public class RedisClassAttendancePayload
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public DateTime AttendanceDate { get; set; }
        public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAtUtc { get; set; }
        public bool IsDirty { get; set; }
        public List<StudentAttendanceRowItem> Students { get; set; } = new();
    }

    /// <summary>
    /// Cấu trúc tra cứu nhanh O(1) khi quét mã QR tại cổng nhà thờ
    /// </summary>
    public class RedisQrStudentLookupItem
    {
        public long EnrollmentId { get; set; }
        public long StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string ChristianName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Nam";
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Thông tin Bài học, Phòng học & Nhiệm vụ của GLV trong giờ học (Lưu trên Redis)
    /// </summary>
    public class ClassSessionContextDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }

        // Thông tin Phòng học & Ca sinh hoạt từ ClassSchedules / ClassRoom
        public string RoomName { get; set; } = "Chưa xếp phòng";
        public string ShiftName { get; set; } = "Ca Sáng (09:00 - 10:30)";
        public string TimeSlotDisplay { get; set; } = "09:00 - 10:30";

        // Thông tin Bài giảng trong ngày từ ClassLessonPlans
        public bool HasLessonPlan { get; set; }
        public string? LiturgicalDay { get; set; }
        public string? LessonCode { get; set; }
        public string TopicTitle { get; set; } = "Sinh hoạt & Học Giáo lý định kỳ";
        public string? KeyPoints { get; set; }
        public string? AssignedTeacherName { get; set; }
        public bool IsExamDay { get; set; }
        public bool IsDayOff { get; set; }

        // Thống kê tác vụ cần GLV xử lý trong giờ học
        public int PendingAbsenceRequestsCount { get; set; }
        public string? LatestPinnedAnnouncementTitle { get; set; }
        public bool IsServedFromRedis { get; set; } = true;
        public bool HasUnsyncedChanges { get; set; }
        public List<InClassTaskChecklistItem> Tasks { get; set; } = new();
    }

    public class InClassTaskChecklistItem
    {
        public string TaskCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "bg-primary";
        public string? ActionUrl { get; set; }
        public bool IsCompleted { get; set; }
    }
}