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

        public static string QrNegativeLookup(DateTime date, string studentCode) =>
            $"ATT:QR:MISS:DATE:{date:yyyy-MM-dd}:CODE:{studentCode.Trim().ToUpperInvariant()}";

        public static string ActiveCampaignByClass(int classId, DateTime date) =>
            $"ATT:CAMPAIGN:CLASS:{classId}:DATE:{date:yyyy-MM-dd}";

        public static string CampaignScanMarker(long campaignId, long enrollmentId, DateTime date) =>
            $"ATT:CAMPAIGN:SCAN:{campaignId}:ENR:{enrollmentId}:DATE:{date:yyyy-MM-dd}";

        public static string ClassesByYear(int academicYearId) =>
            $"ATT:CLASSES:YEAR:{academicYearId}";

        public static string ClassMetadata(int classId) =>
            $"ATT:META:CLASS:{classId}";

        public static string TeacherAuthorizedClassIds(long userId, int academicYearId) =>
            $"ATT:AUTH:TEACHER:{userId}:YEAR:{academicYearId}";

        public static string OfflineStudentsCache(int academicYearId) =>
            $"ATT:OFFLINE:STUDENTS:YEAR:{academicYearId}";
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
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedIp { get; set; }
        public string? LastActionType { get; set; }
        public bool IsDirty { get; set; }
        /// <summary>
        /// Số phiên bản tăng dần sau mỗi thay đổi trên Redis nhằm ngăn mất dữ liệu khi luồng quét QR và luồng Flush chạy song song.
        /// </summary>
        public long Revision { get; set; }
        /// <summary>
        /// Tập EnrollmentId thực sự thay đổi kể từ lần Flush DB gần nhất (chỉ Upsert đúng học sinh thay đổi thay vì cả lớp).
        /// </summary>
        public HashSet<long>? DirtyEnrollmentIds { get; set; }
        /// <summary>
        /// Thông tin Audit riêng cho từng EnrollmentId khi nhiều GLV quét cùng một lớp trong cùng chu kỳ gom nhóm.
        /// </summary>
        public Dictionary<long, RedisDirtyAuditMeta>? DirtyAuditMetaByEnrollment { get; set; }
        /// <summary>
        /// Danh sách lượt điểm danh Chiến dịch 1 tháng chờ đồng bộ theo lô cùng bảng Attendances.
        /// </summary>
        public List<RedisPendingCampaignCheckIn>? PendingCampaignCheckIns { get; set; }
        public List<StudentAttendanceRowItem> Students { get; set; } = new();
    }

    public class RedisDirtyAuditMeta
    {
        public string ActionType { get; set; } = "QR_SCAN";
        public string? ModifiedBy { get; set; }
        public string? IpAddress { get; set; }
    }

    public class RedisPendingCampaignCheckIn
    {
        public long CampaignId { get; set; }
        public long StudentId { get; set; }
        public long EnrollmentId { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime CheckInTimeUtc { get; set; } = DateTime.UtcNow;
    }

    public class RedisActiveCampaignCacheItem
    {
        public bool HasActiveCampaign { get; set; }
        public long? CampaignId { get; set; }
    }

    public class RedisClassMetaItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Session { get; set; } = string.Empty;
        public int AcademicYearId { get; set; }
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