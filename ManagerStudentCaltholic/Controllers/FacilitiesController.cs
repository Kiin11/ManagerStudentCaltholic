using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize]
    public class FacilitiesController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<FacilitiesController> _logger;

        public FacilitiesController(ParishDbContext context, ILogger<FacilitiesController> logger)
        {
            _context = context;
            _logger = logger;
        }
        /// <summary>
        /// 1. GET: /Facilities (Sơ đồ phòng học, Cảnh báo quá tải, Báo hỏng)
        /// </summary>
        /// <param name="zoneId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(int? zoneId)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var currentYearId = currentYear?.Id ?? 0;

            // 1. Danh sách Khu vực (BuildingZones) kèm danh sách phòng
            var zonesQuery = _context.BuildingZones
                .Include(z => z.Rooms)
                .OrderBy(z => z.DisplayOrder)
                .AsNoTracking();

            var zones = await zonesQuery.ToListAsync();

            // 2. Thống kê sĩ số các lớp đang gán vào từng phòng để kiểm tra Quá Tải (Capacity Check - TASK-905)
            // Ghép nối qua ClassSchedules hoặc RoomNumber tạm thời
            var schedules = await _context.ClassSchedules
                .Include(s => s.ClassRoom)
                    .ThenInclude(c => c.Enrollments)
                .Where(s => s.ClassRoom.AcademicYearId == currentYearId)
                .AsNoTracking()
                .ToListAsync();

            // Gom nhóm sĩ số lớp theo ClassRoomLocationId
            var roomOccupancy = schedules
                .GroupBy(s => s.ClassRoomLocationId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Classes = g.Select(x => x.ClassRoom.Name).Distinct().ToList(),
                        MaxEnrolled = g.Max(x => x.ClassRoom.Enrollments.Count),
                        Schedules = g.ToList()
                    }
                );

            ViewBag.RoomOccupancy = roomOccupancy;
            ViewBag.SelectedZoneId = zoneId;

            // 3. Danh sách sự cố thiết bị đang chờ xử lý
            var pendingIncidents = await _context.RoomIncidentReports
                .Include(r => r.ClassRoomLocation)
                .Include(r => r.ReporterUser)
                .Where(r => r.Status != "RESOLVED" && r.Status != "CANCELLED")
                .OrderByDescending(r => r.ReportedAt)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.PendingIncidents = pendingIncidents;

            return View(zones);
        }

        /// <summary>
        /// 2. POST: /Facilities/ReportIncident (GLV Báo hỏng phòng học - TASK-905)
        /// </summary>
        /// <param name="locationId"></param>
        /// <param name="deviceType"></param>
        /// <param name="severity"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportIncident(int locationId, string deviceType, string severity, string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return Json(new { success = false, message = "Vui lòng nhập mô tả sự cố hư hỏng." });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdStr, out var reporterUserId))
            {
                return Json(new { success = false, message = "Không xác định được danh tính người báo cáo." });
            }

            var report = new RoomIncidentReport
            {
                ClassRoomLocationId = locationId,
                ReporterUserId = reporterUserId,
                DeviceType = deviceType,
                Severity = severity ?? "NORMAL",
                Description = description.Trim(),
                Status = "PENDING",
                ReportedAt = DateTime.UtcNow
            };

            _context.RoomIncidentReports.Add(report);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Người dùng {UserId} báo hỏng tại phòng ID {RoomId}: {Device} ({Severity})",
                reporterUserId, locationId, deviceType, severity);

            return Json(new { success = true, message = "Đã gửi báo cáo sự cố thành công đến Ban Quản trị Cơ sở vật chất!" });
        }

        /// <summary>
        /// 3. POST: /Facilities/ResolveIncident (BĐH duyệt sửa chữa xong)
        /// </summary>
        /// <param name="reportId"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> ResolveIncident(long reportId, string? notes)
        {
            var incident = await _context.RoomIncidentReports.FindAsync(reportId);
            if (incident == null)
            {
                return Json(new { success = false, message = "Không tìm thấy báo cáo sự cố." });
            }

            incident.Status = "RESOLVED";
            incident.ResolvedAt = DateTime.UtcNow;
            incident.ResolutionNotes = notes;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã đánh dấu khắc phục sự cố thành công!" });
        }

        /// <summary>
        /// 4. GET: /Facilities/LessonPlans (Tra cứu Kế hoạch năm học - TASK-904)
        /// </summary>
        /// <param name="classRoomId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> LessonPlans(int? classRoomId)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var classes = await _context.Classes
                .Where(c => c.AcademicYearId == yearId)
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Classes = classes;

            var selectedClassId = classRoomId ?? classes.FirstOrDefault()?.Id ?? 0;
            ViewBag.SelectedClassId = selectedClassId;

            var plans = await _context.ClassLessonPlans
                .Where(p => p.ClassRoomId == selectedClassId)
                .OrderBy(p => p.LessonDate)
                .AsNoTracking()
                .ToListAsync();

            return View(plans);
        }
    }
}
