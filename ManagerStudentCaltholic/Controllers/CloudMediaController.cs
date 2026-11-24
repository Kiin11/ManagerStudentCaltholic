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

            if (contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers["Content-Security-Policy"] = "sandbox";
            }

            bool isAbsenceDoc = string.Equals(media.Category, CloudMediaCategory.AbsenceDoc, StringComparison.OrdinalIgnoreCase);
            bool canInline = !download && !isAbsenceDoc && InlineSafeContentTypes.Contains(contentType);
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
            bool isProbationary = User.IsInRole(UserRole.ProbationaryTeacher);
            bool isStaff = isLeadership
                || isBranchHead
                || User.IsInRole(UserRole.Teacher)
                || isProbationary;

            var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value?.Trim();

            switch (media.Category.ToUpperInvariant())
            {
                case CloudMediaCategory.SystemLog:
                    return isAdmin;

                case CloudMediaCategory.LessonPlan:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessLessonPlanAsync(media, currentUserId, isBranchHead, isProbationary, managedGrade, ct);

                case CloudMediaCategory.IncidentImage:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessIncidentImageAsync(media, currentUserId, ct);

                case CloudMediaCategory.Avatar:
                    if (string.Equals(media.EntityName, nameof(Models.Entities.User), StringComparison.OrdinalIgnoreCase))
                    {
                        if (media.EntityId.HasValue && media.EntityId.Value == currentUserId)
                        {
                            return true;
                        }

                        if (isLeadership)
                        {
                            return true;
                        }

                        if (!isStaff || !media.EntityId.HasValue || media.EntityId.Value <= 0)
                        {
                            return false;
                        }

                        return await CanStaffAccessUserAvatarAsync(
                            media.EntityId.Value,
                            currentUserId,
                            isBranchHead,
                            isProbationary,
                            managedGrade,
                            ct);
                    }

                    if (string.Equals(media.EntityName, nameof(Student), StringComparison.OrdinalIgnoreCase))
                    {
                        if (isLeadership)
                        {
                            return true;
                        }

                        if (!isStaff || !media.EntityId.HasValue || media.EntityId.Value <= 0)
                        {
                            return false;
                        }

                        return await CanStaffAccessStudentAsync(
                            media.EntityId.Value,
                            currentUserId,
                            isBranchHead,
                            isProbationary,
                            managedGrade,
                            ct);
                    }

                    return false;

                case CloudMediaCategory.SacramentDoc:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessSacramentDocAsync(media, currentUserId, isBranchHead, isProbationary, managedGrade, ct);

                case CloudMediaCategory.AnnouncementDoc:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessAnnouncementDocAsync(media, currentUserId, isBranchHead, isProbationary, managedGrade, ct);

                case CloudMediaCategory.AbsenceDoc:
                    if (isLeadership) return true;
                    if (!isStaff) return false;
                    return await CanAccessAbsenceDocAsync(media, currentUserId, isBranchHead, isProbationary, managedGrade, ct);

                default:
                    return false;
            }
        }

        private async Task<bool> CanAccessLessonPlanAsync(
            CloudMediaFile media,
            long currentUserId,
            bool isBranchHead,
            bool isProbationary,
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

            return await CanStaffAccessClassAsync(
                targetClass.Id,
                currentUserId,
                isBranchHead,
                isProbationary,
                managedGrade,
                ct);
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
            bool isProbationary,
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

            return await CanStaffAccessStudentAsync(studentId.Value, currentUserId, isBranchHead, isProbationary, managedGrade, ct);
        }

        private async Task<bool> CanAccessAnnouncementDocAsync(
            CloudMediaFile media,
            long currentUserId,
            bool isBranchHead,
            bool isProbationary,
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

                if (isProbationary && GradeLevel.IsAfternoonGrade(announcement.TargetGradeLevel))
                {
                    return false;
                }

                if (isBranchHead)
                {
                    if (string.IsNullOrWhiteSpace(managedGrade))
                    {
                        return false;
                    }

                    return string.Equals(announcement.TargetGradeLevel.Trim(), managedGrade.Trim(), StringComparison.OrdinalIgnoreCase);
                }

                var currentYearId = await _context.AcademicYears
                    .AsNoTracking()
                    .Where(y => y.IsCurrent)
                    .Select(y => (int?)y.Id)
                    .FirstOrDefaultAsync(ct);

                if (!currentYearId.HasValue)
                {
                    return false;
                }

                var targetGradeLower = announcement.TargetGradeLevel.Trim().ToLowerInvariant();
                var teacherQuery = _context.ClassTeachers
                    .AsNoTracking()
                    .Where(t => t.UserId == currentUserId
                        && t.AcademicYearId == currentYearId.Value
                        && t.ClassRoom.AcademicYearId == currentYearId.Value
                        && t.ClassRoom.AcademicYear.IsCurrent
                        && t.ClassRoom.GradeLevel.ToLower() == targetGradeLower);

                if (isProbationary)
                {
                    teacherQuery = teacherQuery.Where(t =>
                        t.ClassRoom.Session != GradeLevel.SessionAfternoon &&
                        t.ClassRoom.GradeLevel != GradeLevel.BaoDong &&
                        t.ClassRoom.GradeLevel != GradeLevel.DuBi &&
                        t.ClassRoom.GradeLevel != GradeLevel.DuBiCode);
                }

                return await teacherQuery.AnyAsync(ct);
            }

            if (scope == "CLASS")
            {
                if (!announcement.TargetClassRoomId.HasValue || announcement.TargetClassRoom == null)
                {
                    return false;
                }

                return await CanStaffAccessClassAsync(
                    announcement.TargetClassRoomId.Value,
                    currentUserId,
                    isBranchHead,
                    isProbationary,
                    managedGrade,
                    ct);
            }

            return false;
        }

        private async Task<bool> CanAccessAbsenceDocAsync(
            CloudMediaFile media,
            long currentUserId,
            bool isBranchHead,
            bool isProbationary,
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
                var classRoomId = await _context.AbsenceRequests
                    .AsNoTracking()
                    .Where(r => r.Id == media.EntityId.Value)
                    .Select(r => (int?)r.Enrollment.ClassRoomId)
                    .FirstOrDefaultAsync(ct);

                if (!classRoomId.HasValue || classRoomId.Value <= 0)
                {
                    return false;
                }

                return await CanStaffAccessClassAsync(
                    classRoomId.Value,
                    currentUserId,
                    isBranchHead,
                    isProbationary,
                    managedGrade,
                    ct);
            }

            if (string.Equals(media.EntityName, nameof(Student), StringComparison.OrdinalIgnoreCase))
            {
                return await CanStaffAccessStudentAsync(media.EntityId.Value, currentUserId, isBranchHead, isProbationary, managedGrade, ct);
            }

            return false;
        }

        private async Task<bool> CanStaffAccessClassAsync(
            int classRoomId,
            long currentUserId,
            bool isBranchHead,
            bool isProbationary,
            string? managedGrade,
            CancellationToken ct)
        {
            var targetClass = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Id == classRoomId)
                .Select(c => new
                {
                    c.Id,
                    c.GradeLevel,
                    c.Session,
                    c.AcademicYearId,
                    IsCurrentYear = c.AcademicYear.IsCurrent
                })
                .FirstOrDefaultAsync(ct);

            if (targetClass == null || !targetClass.IsCurrentYear)
            {
                return false;
            }

            if (isProbationary && GradeLevel.IsAfternoonSession(targetClass.Session, targetClass.GradeLevel))
            {
                return false;
            }

            if (isBranchHead)
            {
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    return false;
                }

                return string.Equals(targetClass.GradeLevel?.Trim(), managedGrade.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            return await _context.ClassTeachers
                .AsNoTracking()
                .AnyAsync(t => t.ClassRoomId == classRoomId
                    && t.UserId == currentUserId
                    && t.AcademicYearId == targetClass.AcademicYearId
                    && t.ClassRoom.AcademicYear.IsCurrent, ct);
        }

        private async Task<bool> CanStaffAccessStudentAsync(
            long studentId,
            long currentUserId,
            bool isBranchHead,
            bool isProbationary,
            string? managedGrade,
            CancellationToken ct)
        {
            var currentYearId = await _context.AcademicYears
                .AsNoTracking()
                .Where(y => y.IsCurrent)
                .Select(y => (int?)y.Id)
                .FirstOrDefaultAsync(ct);

            if (!currentYearId.HasValue)
            {
                return false;
            }

            if (isBranchHead)
            {
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    return false;
                }

                var managedGradeLower = managedGrade.Trim().ToLowerInvariant();
                return await _context.Enrollments
                    .AsNoTracking()
                    .AnyAsync(e => e.StudentId == studentId
                        && e.ClassRoom.AcademicYearId == currentYearId.Value
                        && e.ClassRoom.AcademicYear.IsCurrent
                        && e.ClassRoom.GradeLevel.ToLower() == managedGradeLower, ct);
            }

            var assignedQuery = _context.ClassTeachers
                .AsNoTracking()
                .Where(t => t.UserId == currentUserId
                    && t.AcademicYearId == currentYearId.Value
                    && t.ClassRoom.AcademicYearId == currentYearId.Value
                    && t.ClassRoom.AcademicYear.IsCurrent);

            if (isProbationary)
            {
                assignedQuery = assignedQuery.Where(t =>
                    t.ClassRoom.Session != GradeLevel.SessionAfternoon &&
                    t.ClassRoom.GradeLevel != GradeLevel.BaoDong &&
                    t.ClassRoom.GradeLevel != GradeLevel.DuBi &&
                    t.ClassRoom.GradeLevel != GradeLevel.DuBiCode);
            }

            var assignedClassIds = await assignedQuery
                .Select(t => t.ClassRoomId)
                .Distinct()
                .ToListAsync(ct);

            if (assignedClassIds.Count == 0)
            {
                return false;
            }

            return await _context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId
                    && assignedClassIds.Contains(e.ClassRoomId)
                    && e.ClassRoom.AcademicYearId == currentYearId.Value, ct);
        }

        private async Task<bool> CanStaffAccessUserAvatarAsync(
            long targetUserId,
            long currentUserId,
            bool isBranchHead,
            bool isProbationary,
            string? managedGrade,
            CancellationToken ct)
        {
            if (isBranchHead && string.IsNullOrWhiteSpace(managedGrade))
            {
                return false;
            }

            var targetUser = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == targetUserId && u.IsActive)
                .Select(u => new
                {
                    u.Id,
                    u.Role,
                    u.ManagedGradeLevel,
                    u.LinkedStudentId,
                    u.StudentId
                })
                .FirstOrDefaultAsync(ct);

            if (targetUser == null)
            {
                return false;
            }

            bool targetIsStaff = targetUser.Role is UserRole.Admin
                or UserRole.SpiritualDirector
                or UserRole.ExecutiveBoard
                or UserRole.BranchHead
                or UserRole.Teacher
                or UserRole.ProbationaryTeacher;

            if (!targetIsStaff)
            {
                return false;
            }

            var currentYearId = await _context.AcademicYears
                .AsNoTracking()
                .Where(y => y.IsCurrent)
                .Select(y => (int?)y.Id)
                .FirstOrDefaultAsync(ct);

            if (!currentYearId.HasValue)
            {
                return false;
            }

            long? targetLinkedStudentId = targetUser.LinkedStudentId ?? targetUser.StudentId;

            if (isBranchHead)
            {
                var managedGradeLower = managedGrade!.Trim().ToLowerInvariant();

                if (targetUser.Role == UserRole.BranchHead &&
                    string.Equals(targetUser.ManagedGradeLevel?.Trim(), managedGrade.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                bool targetTeachesInBranch = await _context.ClassTeachers
                    .AsNoTracking()
                    .AnyAsync(t => t.UserId == targetUserId
                        && t.AcademicYearId == currentYearId.Value
                        && t.ClassRoom.AcademicYearId == currentYearId.Value
                        && t.ClassRoom.AcademicYear.IsCurrent
                        && t.ClassRoom.GradeLevel.ToLower() == managedGradeLower, ct);

                if (targetTeachesInBranch)
                {
                    return true;
                }

                if (targetLinkedStudentId.HasValue && targetLinkedStudentId.Value > 0)
                {
                    return await _context.Enrollments
                        .AsNoTracking()
                        .AnyAsync(e => e.StudentId == targetLinkedStudentId.Value
                            && e.ClassRoom.AcademicYearId == currentYearId.Value
                            && e.ClassRoom.AcademicYear.IsCurrent
                            && e.ClassRoom.GradeLevel.ToLower() == managedGradeLower, ct);
                }

                return false;
            }

            var callerClassQuery = _context.ClassTeachers
                .AsNoTracking()
                .Where(t => t.UserId == currentUserId
                    && t.AcademicYearId == currentYearId.Value
                    && t.ClassRoom.AcademicYearId == currentYearId.Value
                    && t.ClassRoom.AcademicYear.IsCurrent);

            if (isProbationary)
            {
                callerClassQuery = callerClassQuery.Where(t =>
                    t.ClassRoom.Session != GradeLevel.SessionAfternoon &&
                    t.ClassRoom.GradeLevel != GradeLevel.BaoDong &&
                    t.ClassRoom.GradeLevel != GradeLevel.DuBi &&
                    t.ClassRoom.GradeLevel != GradeLevel.DuBiCode);
            }

            var callerClassIds = await callerClassQuery
                .Select(t => t.ClassRoomId)
                .Distinct()
                .ToListAsync(ct);

            if (callerClassIds.Count == 0)
            {
                return false;
            }

            bool sharesClassAsTeacher = await _context.ClassTeachers
                .AsNoTracking()
                .AnyAsync(t => t.UserId == targetUserId
                    && t.AcademicYearId == currentYearId.Value
                    && callerClassIds.Contains(t.ClassRoomId), ct);

            if (sharesClassAsTeacher)
            {
                return true;
            }

            if (targetUser.Role == UserRole.BranchHead && !string.IsNullOrWhiteSpace(targetUser.ManagedGradeLevel))
            {
                var targetGradeLower = targetUser.ManagedGradeLevel.Trim().ToLowerInvariant();
                bool isCallerBranchHead = await _context.Classes
                    .AsNoTracking()
                    .AnyAsync(c => callerClassIds.Contains(c.Id)
                        && c.GradeLevel.ToLower() == targetGradeLower, ct);

                if (isCallerBranchHead)
                {
                    return true;
                }
            }

            if (targetLinkedStudentId.HasValue && targetLinkedStudentId.Value > 0)
            {
                return await _context.Enrollments
                    .AsNoTracking()
                    .AnyAsync(e => e.StudentId == targetLinkedStudentId.Value
                        && callerClassIds.Contains(e.ClassRoomId), ct);
            }

            return false;
        }
    }
}
