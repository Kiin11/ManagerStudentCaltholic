using System.Text.Json;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class DeviceStudentRecognitionService : IDeviceStudentRecognitionService
    {
        private const string CookieName = "Parish_Device_Students";
        private const int MaxRememberedStudents = 5;

        private readonly ParishDbContext _context;
        private readonly IDataProtector _protector;
        private readonly ILogger<DeviceStudentRecognitionService> _logger;

        public DeviceStudentRecognitionService(
            ParishDbContext context,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<DeviceStudentRecognitionService> logger)
        {
            _context = context;
            _protector = dataProtectionProvider.CreateProtector("Parish.DeviceStudentRecognition.v1");
            _logger = logger;
        }

        public void RememberStudentOnDevice(HttpContext httpContext, Student student, ClassRoom? currentClass)
        {
            if (httpContext == null || student == null) return;

            var list = ReadProtectedCookieList(httpContext);
            var cleanCode = student.StudentCode.Trim().ToUpperInvariant();

            list.RemoveAll(x => x.StudentId == student.Id ||
                                string.Equals(x.StudentCode, cleanCode, StringComparison.OrdinalIgnoreCase));

            list.Insert(0, new RememberedStudentCookieItem
            {
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                ChristianName = student.ChristianName,
                FullName = $"{student.FirstName} {student.LastName}".Trim(),
                ClassRoomId = currentClass?.Id ?? 0,
                ClassName = currentClass?.Name ?? "Chưa xếp lớp",
                GradeLevel = currentClass?.GradeLevel ?? string.Empty,
                AcademicYearId = currentClass?.AcademicYearId ?? 0,
                LastQueriedAtUtc = DateTime.UtcNow
            });

            if (list.Count > MaxRememberedStudents)
            {
                list = list.Take(MaxRememberedStudents).ToList();
            }

            WriteProtectedCookieList(httpContext, list);
        }

        public RememberedStudentCookieItem? TryGetRememberedStudent(HttpContext httpContext, string studentCode)
        {
            if (httpContext == null || string.IsNullOrWhiteSpace(studentCode))
                return null;

            var cleanCode = studentCode.Trim().ToUpperInvariant();
            var list = ReadProtectedCookieList(httpContext);

            return list.FirstOrDefault(x => string.Equals(x.StudentCode.Trim(), cleanCode, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<DeviceDetectedStudentContextViewModel> DetectStudentsAndLoadAnnouncementsAsync(HttpContext httpContext)
        {
            var result = new DeviceDetectedStudentContextViewModel();
            if (httpContext == null) return result;

            var cookieItems = ReadProtectedCookieList(httpContext);
            if (!cookieItems.Any()) return result;

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            int currentYearId = currentYear?.Id ?? 0;

            var studentIds = cookieItems.Select(x => x.StudentId).Distinct().ToList();

            // Tự động đồng bộ lại Lớp học & Mã thiếu nhi mới nhất nếu học sinh vừa được kết chuyển lên lớp (FEAT-P4-04)
            var activeEnrollments = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Include(e => e.ClassRoom)
                .Where(e => studentIds.Contains(e.StudentId) &&
                            e.Student.IsActive &&
                            (currentYearId == 0 || e.ClassRoom.AcademicYearId == currentYearId))
                .ToListAsync();

            var enrollmentMap = activeEnrollments
                .GroupBy(e => e.StudentId)
                .ToDictionary(g => g.Key, g => g.First());

            var refreshedList = new List<RememberedStudentCookieItem>();
            foreach (var item in cookieItems)
            {
                if (enrollmentMap.TryGetValue(item.StudentId, out var enr) && enr.Student != null && enr.ClassRoom != null)
                {
                    item.StudentCode = enr.Student.StudentCode;
                    item.ChristianName = enr.Student.ChristianName;
                    item.FullName = $"{enr.Student.FirstName} {enr.Student.LastName}".Trim();
                    item.ClassRoomId = enr.ClassRoomId;
                    item.ClassName = enr.ClassRoom.Name;
                    item.GradeLevel = enr.ClassRoom.GradeLevel;
                    item.AcademicYearId = enr.ClassRoom.AcademicYearId;
                }
                refreshedList.Add(item);
            }

            result.RememberedStudents = refreshedList;

            var classIds = refreshedList.Where(x => x.ClassRoomId > 0).Select(x => x.ClassRoomId).Distinct().ToList();
            var gradeLevels = refreshedList.Where(x => !string.IsNullOrWhiteSpace(x.GradeLevel)).Select(x => x.GradeLevel).Distinct().ToList();

            if (!classIds.Any() && !gradeLevels.Any())
            {
                return result;
            }

            var nowUtc = DateTime.UtcNow;
            var announcements = await _context.Announcements
                .AsNoTracking()
                .Include(a => a.TargetClassRoom)
                .Where(a => a.IsActive &&
                            (!a.ExpireAt.HasValue || a.ExpireAt.Value >= nowUtc) &&
                            ((a.Scope == "CLASS" && a.TargetClassRoomId.HasValue && classIds.Contains(a.TargetClassRoomId.Value)) ||
                             (a.Scope == "GRADE" && a.TargetGradeLevel != null && gradeLevels.Contains(a.TargetGradeLevel))))
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.CreatedAt)
                .Take(12)
                .ToListAsync();

            result.ClassAnnouncements = announcements.Select(a => new StudentClassAnnouncementItemDto
            {
                AnnouncementId = a.Id,
                Title = a.Title,
                Content = a.Content,
                Scope = a.Scope,
                Priority = a.Priority,
                TargetClassRoomId = a.TargetClassRoomId,
                TargetGradeLevel = a.TargetGradeLevel,
                TargetDisplayBadge = a.Scope == "CLASS"
                    ? $"Lớp {a.TargetClassRoom?.Name ?? ""}"
                    : $"Khối {a.TargetGradeLevel}",
                IsPinned = a.IsPinned,
                CreatedAt = a.CreatedAt,
                AuthorName = string.IsNullOrWhiteSpace(a.CreatedBy) ? "Giáo lý viên" : a.CreatedBy
            }).ToList();

            return result;
        }

        public void DetachStudentFromDevice(HttpContext httpContext, string? studentCode = null)
        {
            if (httpContext == null) return;

            if (string.IsNullOrWhiteSpace(studentCode))
            {
                httpContext.Response.Cookies.Delete(CookieName);
                return;
            }

            var cleanCode = studentCode.Trim().ToUpperInvariant();
            var list = ReadProtectedCookieList(httpContext);
            list.RemoveAll(x => string.Equals(x.StudentCode.Trim(), cleanCode, StringComparison.OrdinalIgnoreCase));

            if (!list.Any())
            {
                httpContext.Response.Cookies.Delete(CookieName);
            }
            else
            {
                WriteProtectedCookieList(httpContext, list);
            }
        }

        private List<RememberedStudentCookieItem> ReadProtectedCookieList(HttpContext httpContext)
        {
            try
            {
                if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var encryptedValue) ||
                    string.IsNullOrWhiteSpace(encryptedValue))
                {
                    return new List<RememberedStudentCookieItem>();
                }

                var json = _protector.Unprotect(encryptedValue);
                return JsonSerializer.Deserialize<List<RememberedStudentCookieItem>>(json)
                       ?? new List<RememberedStudentCookieItem>();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Không thể giải mã Cookie thiết bị Parish_Device_Students, tiến hành làm mới.");
                httpContext.Response.Cookies.Delete(CookieName);
                return new List<RememberedStudentCookieItem>();
            }
        }

        private void WriteProtectedCookieList(HttpContext httpContext, List<RememberedStudentCookieItem> items)
        {
            try
            {
                var json = JsonSerializer.Serialize(items);
                var encrypted = _protector.Protect(json);

                httpContext.Response.Cookies.Append(CookieName, encrypted, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = httpContext.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(90),
                    IsEssential = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể ghi Cookie thiết bị Parish_Device_Students.");
            }
        }
    }
}