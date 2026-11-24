using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme},{JwtBearerDefaults.AuthenticationScheme}")]
    public class CloudMediaController : Controller
    {
        private static readonly HashSet<string> InlineSafeContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/webp",
            "image/jpeg",
            "image/png",
            "image/gif",
            "application/pdf"
        };

        private readonly ParishDbContext _context;
        private readonly IGoogleDriveStorageService _storageService;
        private readonly ILogger<CloudMediaController> _logger;

        public CloudMediaController(
            ParishDbContext context,
            IGoogleDriveStorageService storageService,
            ILogger<CloudMediaController> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
        }

        [HttpGet("CloudMedia/File/{id:long}")]
        public async Task<IActionResult> File(long id, [FromQuery] bool download = false, CancellationToken ct = default)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId) || currentUserId <= 0)
            {
                return Unauthorized();
            }

            var media = await _context.CloudMediaFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id, ct);

            if (media == null)
            {
                return NotFound();
            }

            if (!await CanAccessMediaAsync(media, currentUserId, ct))
            {
                _logger.LogWarning(
                    "Từ chối truy cập CloudMediaFile Id={MediaId} Category={Category} từ UserId={UserId}",
                    media.Id,
                    media.Category,
                    currentUserId);
                return Forbid();
            }

            var downloadResult = await _storageService.DownloadAsync(id, ct);
            if (downloadResult == null)
            {
                return NotFound("Không tìm thấy tệp dữ liệu trên kho lưu trữ.");
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Cache-Control"] = ResolveCacheControlHeader(media.Category);

            var contentType = string.IsNullOrWhiteSpace(downloadResult.ContentType)
                ? "application/octet-stream"
                : downloadResult.ContentType;

            var safeFileName = string.IsNullOrWhiteSpace(downloadResult.FileName)
                ? $"file_{id}"
                : Path.GetFileName(downloadResult.FileName);

            bool canInline = !download && InlineSafeContentTypes.Contains(contentType);
            if (canInline)
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(safeFileName)}\"";
                return File(downloadResult.ContentStream, contentType);
            }

            return File(downloadResult.ContentStream, contentType, safeFileName);
        }

        private static string ResolveCacheControlHeader(string category)
        {
            if (string.Equals(category, CloudMediaCategory.SystemLog, StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, CloudMediaCategory.SacramentDoc, StringComparison.OrdinalIgnoreCase))
            {
                return "no-store, no-cache, must-revalidate";
            }

            if (string.Equals(category, CloudMediaCategory.IncidentImage, StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, CloudMediaCategory.Avatar, StringComparison.OrdinalIgnoreCase))
            {
                return "private, max-age=300";
            }

            return "private, no-cache";
        }

        private async Task<bool> CanAccessMediaAsync(CloudMediaFile media, long currentUserId, CancellationToken ct)
        {
            bool isAdmin = User.IsInRole(UserRole.Admin);
            bool isLeadership = isAdmin
                || User.IsInRole(UserRole.SpiritualDirector)
                || User.IsInRole(UserRole.ExecutiveBoard);
            bool isBranchHead = User.IsInRole(UserRole.BranchHead);
            bool isStaff = isLeadership
                || isBranchHead
                || User.IsInRole(UserRole.Teacher)
                || User.IsInRole(UserRole.ProbationaryTeacher);

            var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;

            switch (media.Category.ToUpperInvariant())
            {
                case CloudMediaCategory.SystemLog:
                    return isAdmin;

                case CloudMediaCategory.LessonPlan:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessLessonPlanAsync(media, currentUserId, isBranchHead, managedGrade, ct);

                case CloudMediaCategory.IncidentImage:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessIncidentImageAsync(media, currentUserId, ct);

                case CloudMediaCategory.Avatar:
                    if (string.Equals(media.EntityName, nameof(Models.Entities.User), StringComparison.OrdinalIgnoreCase))
                    {
                        return isStaff || (media.EntityId.HasValue && media.EntityId.Value == currentUserId);
                    }

                    if (string.Equals(media.EntityName, nameof(Student), StringComparison.OrdinalIgnoreCase))
                    {
                        return isStaff;
                    }

                    return false;

                case CloudMediaCategory.SacramentDoc:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessSacramentDocAsync(media, currentUserId, isBranchHead, managedGrade, ct);

                case CloudMediaCategory.AnnouncementDoc:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessAnnouncementDocAsync(media, currentUserId, isBranchHead, managedGrade, ct);

                case CloudMediaCategory.AbsenceDoc:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessAbsenceDocAsync(media, currentUserId, isBranchHead, managedGrade, ct);

                default:
                    return false;
            }
        }

        private async Task<bool> CanAccessLessonPlanAsync(
            CloudMediaFile media,
            long currentUserId,
            bool isBranchHead,
            string? managedGrade,
            CancellationToken ct)
        {
            ClassRoom? targetClass = null;

            if (string.Equals(media.EntityName, nameof(ClassLessonDocument), StringComparison.OrdinalIgnoreCase)
                && media.EntityId.HasValue)
            {
                targetClass = await _context.ClassLessonDocuments
                    .AsNoTracking()
                    .Where(d => d.Id == media.EntityId.Value)
                    .Select(d => d.ClassRoom)
                    .FirstOrDefaultAsync(ct);
            }
            else if (string.Equals(media.EntityName, nameof(ClassCurriculumVersion), StringComparison.OrdinalIgnoreCase)
                && media.EntityId.HasValue)
            {
                targetClass = await _context.ClassCurriculumVersions
                    .AsNoTracking()
                    .Where(v => v.Id == media.EntityId.Value)
                    .Select(v => v.CurriculumDocument.ClassRoom)
                    .FirstOrDefaultAsync(ct);
            }
            else if (string.Equals(media.EntityName, nameof(ClassCurriculumDocument), StringComparison.OrdinalIgnoreCase)
                && media.EntityId.HasValue)
            {
                targetClass = await _context.ClassCurriculumDocuments
                    .AsNoTracking()
                    .Where(cd => cd.Id == media.EntityId.Value)
                    .Select(cd => cd.ClassRoom)
                    .FirstOrDefaultAsync(ct);
            }

            if (targetClass == null)
            {
                targetClass = await _context.ClassLessonDocuments
                    .AsNoTracking()
                    .Where(d => d.CloudFileId == media.Id)
                    .Select(d => d.ClassRoom)
                    .FirstOrDefaultAsync(ct)
                    ?? await _context.ClassCurriculumVersions
                        .AsNoTracking()
                        .Where(v => v.CloudFileId == media.Id)
                        .Select(v => v.CurriculumDocument.ClassRoom)
                        .FirstOrDefaultAsync(ct)
                    ?? await _context.ClassCurriculumDocuments
                        .AsNoTracking()
                        .Where(cd => cd.CloudFileId == media.Id)
                        .Select(cd => cd.ClassRoom)
                        .FirstOrDefaultAsync(ct);
            }

            if (targetClass == null)
            {
                return false;
            }

            return await CanStaffAccessClassAsync(targetClass.Id, targetClass.GradeLevel, currentUserId, isBranchHead, managedGrade, ct);
        }

        private async Task<bool> CanAccessIncidentImageAsync(CloudMediaFile media, long currentUserId, CancellationToken ct)
        {
            long? reporterUserId = null;

            if ((string.IsNullOrWhiteSpace(media.EntityName)
                    || string.Equals(media.EntityName, nameof(RoomIncidentReport), StringComparison.OrdinalIgnoreCase))
                && media.EntityId.HasValue)
            {
                reporterUserId = await _context.RoomIncidentReports
                    .AsNoTracking()
                    .Where(r => r.Id == media.EntityId.Value)
                    .Select(r => (long?)r.ReporterUserId)
                    .FirstOrDefaultAsync(ct);
            }

            if (!reporterUserId.HasValue)
            {
                reporterUserId = await _context.RoomIncidentReports
                    .AsNoTracking()
                    .Where(r => r.CloudImageFileId == media.Id)
                    .Select(r => (long?)r.ReporterUserId)
                    .FirstOrDefaultAsync(ct);
            }

            return reporterUserId.HasValue && reporterUserId.Value == currentUserId;
        }

        private async Task<bool> CanAccessSacramentDocAsync(
            CloudMediaFile media,
            long currentUserId,
            bool isBranchHead,
            string? managedGrade,
            CancellationToken ct)
        {
            long? studentId = null;

            if (string.Equals(media.EntityName, nameof(StudentSacramentRecord), StringComparison.OrdinalIgnoreCase)
                && media.EntityId.HasValue)
            {
                studentId = await _context.StudentSacramentRecords
                    .AsNoTracking()
                    .Where(r => r.Id == media.EntityId.Value)
                    .Select(r => (long?)r.StudentId)
                    .FirstOrDefaultAsync(ct);
            }
            else if (string.Equals(media.EntityName, nameof(Student), StringComparison.OrdinalIgnoreCase)
                && media.EntityId.HasValue)
            {
                studentId = media.EntityId.Value;
            }

            if (!studentId.HasValue || studentId.Value <= 0)
            {
                return false;
            }

            return await CanStaffAccessStudentAsync(studentId.Value, currentUserId, isBranchHead, managedGrade, ct);
        }

        private async Task<bool> CanAccessAnnouncementDocAsync(
            CloudMediaFile media,
            long currentUserId,
            bool isBranchHead,
            string? managedGrade,
            CancellationToken ct)
        {
            if (!media.EntityId.HasValue
                || (!string.IsNullOrWhiteSpace(media.EntityName)
                    && !string.Equals(media.EntityName, nameof(Announcement), StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var announcement = await _context.Announcements
                .AsNoTracking()
                .Include(a => a.TargetClassRoom)
                .FirstOrDefaultAsync(a => a.Id == media.EntityId.Value, ct);

            if (announcement == null)
            {
                return false;
            }

            var scope = announcement.Scope?.Trim().ToUpperInvariant() ?? string.Empty;
            if (scope == "ALL" || scope == "PARISH")
            {
                return true; // Đã kiểm tra isStaff ở lớp ngoài
            }

            if (scope == "GRADE")
            {
                if (string.IsNullOrWhiteSpace(announcement.TargetGradeLevel))
                {
                    return false;
                }

                if (isBranchHead
                    && !string.IsNullOrWhiteSpace(managedGrade)
                    && string.Equals(announcement.TargetGradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var currentYearId = await _context.AcademicYears
                    .AsNoTracking()
                    .Where(y => y.IsCurrent)
                    .Select(y => (int?)y.Id)
                    .FirstOrDefaultAsync(ct);

                return await _context.ClassTeachers
                    .AsNoTracking()
                    .AnyAsync(t => t.UserId == currentUserId
                        && (!currentYearId.HasValue || t.AcademicYearId == currentYearId.Value)
                        && t.ClassRoom.GradeLevel == announcement.TargetGradeLevel, ct);
            }

            if (scope == "CLASS")
            {
                if (!announcement.TargetClassRoomId.HasValue || announcement.TargetClassRoom == null)
                {
                    return false;
                }

                return await CanStaffAccessClassAsync(
                    announcement.TargetClassRoomId.Value,
                    announcement.TargetClassRoom.GradeLevel,
                    currentUserId,
                    isBranchHead,
                    managedGrade,
                    ct);
            }

            return false;
        }

        private async Task<bool> CanAccessAbsenceDocAsync(
            CloudMediaFile media,
            long currentUserId,
            bool isBranchHead,
            string? managedGrade,
            CancellationToken ct)
        {
            if (!media.EntityId.HasValue)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(media.EntityName)
                || string.Equals(media.EntityName, nameof(AbsenceRequest), StringComparison.OrdinalIgnoreCase))
            {
                var classInfo = await _context.AbsenceRequests
                    .AsNoTracking()
                    .Where(r => r.Id == media.EntityId.Value)
                    .Select(r => new
                    {
                        r.Enrollment.ClassRoomId,
                        r.Enrollment.ClassRoom.GradeLevel
                    })
                    .FirstOrDefaultAsync(ct);

                if (classInfo == null)
                {
                    return false;
                }

                return await CanStaffAccessClassAsync(
                    classInfo.ClassRoomId,
                    classInfo.GradeLevel,
                    currentUserId,
                    isBranchHead,
                    managedGrade,
                    ct);
            }

            if (string.Equals(media.EntityName, nameof(Student), StringComparison.OrdinalIgnoreCase))
            {
                return await CanStaffAccessStudentAsync(media.EntityId.Value, currentUserId, isBranchHead, managedGrade, ct);
            }

            return false;
        }

        private async Task<bool> CanStaffAccessClassAsync(
            int classRoomId,
            string? classGradeLevel,
            long currentUserId,
            bool isBranchHead,
            string? managedGrade,
            CancellationToken ct)
        {
            if (isBranchHead
                && !string.IsNullOrWhiteSpace(managedGrade)
                && string.Equals(classGradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return await _context.ClassTeachers
                .AsNoTracking()
                .AnyAsync(t => t.ClassRoomId == classRoomId && t.UserId == currentUserId, ct);
        }

        private async Task<bool> CanStaffAccessStudentAsync(
            long studentId,
            long currentUserId,
            bool isBranchHead,
            string? managedGrade,
            CancellationToken ct)
        {
            var currentYearId = await _context.AcademicYears
                .AsNoTracking()
                .Where(y => y.IsCurrent)
                .Select(y => (int?)y.Id)
                .FirstOrDefaultAsync(ct);

            if (isBranchHead && !string.IsNullOrWhiteSpace(managedGrade))
            {
                bool inManagedGrade = await _context.Enrollments
                    .AsNoTracking()
                    .AnyAsync(e => e.StudentId == studentId
                        && (!currentYearId.HasValue || e.ClassRoom.AcademicYearId == currentYearId.Value)
                        && e.ClassRoom.GradeLevel == managedGrade, ct);

                if (inManagedGrade)
                {
                    return true;
                }
            }

            var assignedClassIds = await _context.ClassTeachers
                .AsNoTracking()
                .Where(t => t.UserId == currentUserId
                    && (!currentYearId.HasValue || t.AcademicYearId == currentYearId.Value))
                .Select(t => t.ClassRoomId)
                .ToListAsync(ct);

            if (assignedClassIds.Count == 0)
            {
                return false;
            }

            return await _context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId && assignedClassIds.Contains(e.ClassRoomId), ct);
        }
    }
}
