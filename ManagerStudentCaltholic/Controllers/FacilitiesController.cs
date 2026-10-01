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
        public async Task<IActionResult> Index()
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var currentYearId = currentYear?.Id ?? 0;

            var zones = await _context.BuildingZones
                .Include(z => z.Rooms)
                .OrderBy(z => z.DisplayOrder)
                .AsNoTracking()
                .ToListAsync();

            // Tính sĩ số thực tế đang gán vào phòng học
            var schedules = await _context.ClassSchedules
                .Include(s => s.ClassRoom)
                    .ThenInclude(c => c.Enrollments)
                .Where(s => s.ClassRoom.AcademicYearId == currentYearId)
                .AsNoTracking()
                .ToListAsync();

            var roomOccupancy = schedules
                .GroupBy(s => s.ClassRoomLocationId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Classes = g.Select(x => x.ClassRoom.Name).Distinct().ToList(),
                        MaxEnrolled = g.Max(x => x.ClassRoom.Enrollments.Count)
                    }
                );

            ViewBag.RoomOccupancy = roomOccupancy;

            var pendingIncidents = await _context.RoomIncidentReports
                .Include(r => r.ClassRoomLocation)
                .Include(r => r.ReporterUser)
                .Where(r => r.Status != "RESOLVED" && r.Status != "CANCELLED")
                .OrderByDescending(r => r.ReportedAt)
                .Take(15)
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
        [IgnoreAntiforgeryToken] // Tránh lỗi 400 Bad Request khi Fetch API
        public async Task<IActionResult> ReportIncident([FromForm] int locationId, [FromForm] string deviceType, [FromForm] string? severity, [FromForm] string description)
        {
            try
            {
                if (locationId <= 0 || string.IsNullOrWhiteSpace(description))
                {
                    return Json(new { success = false, message = "Vui lòng chọn phòng và nhập mô tả sự cố hư hỏng!" });
                }

                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!long.TryParse(userIdStr, out var reporterUserId))
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity!.Name);
                    reporterUserId = user?.Id ?? 1;
                }

                var report = new RoomIncidentReport
                {
                    ClassRoomLocationId = locationId,
                    ReporterUserId = reporterUserId,
                    DeviceType = string.IsNullOrWhiteSpace(deviceType) ? "Khác" : deviceType,
                    Severity = severity ?? "NORMAL",
                    Description = description.Trim(),
                    Status = "PENDING",
                    ReportedAt = DateTime.UtcNow
                };

                _context.RoomIncidentReports.Add(report);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã gửi báo cáo sự cố thành công đến Ban Quản trị!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi xử lý: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        /// <summary>
        /// 3. POST: /Facilities/ResolveIncident (BĐH duyệt sửa chữa xong)
        /// </summary>
        /// <param name="reportId"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> ResolveIncident([FromForm] long reportId, [FromForm] string? notes)
        {
            var incident = await _context.RoomIncidentReports.FindAsync(reportId);
            if (incident == null) return Json(new { success = false, message = "Không tìm thấy sự cố." });

            incident.Status = "RESOLVED";
            incident.ResolvedAt = DateTime.UtcNow;
            incident.ResolutionNotes = notes ?? "Đã sửa chữa hoàn tất";

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã cập nhật trạng thái sửa chữa!" });
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
            ViewBag.CurrentYear = currentYear;

            var selectedClassId = classRoomId ?? classes.FirstOrDefault()?.Id ?? 0;
            ViewBag.SelectedClassId = selectedClassId;

            var plans = await _context.ClassLessonPlans
                .Include(p => p.ClassRoom)
                .Where(p => p.ClassRoomId == selectedClassId)
                .OrderBy(p => p.LessonDate)
                .AsNoTracking()
                .ToListAsync();

            return View(plans);
        }

        /// <summary>
        /// 5. POST: /Facilities/CreateZone (Thêm Khu Vực / Dãy Nhà Mới)
        /// </summary>
        /// <param name="zoneName"></param>
        /// <param name="zoneCode"></param>
        /// <param name="description"></param>
        /// <param name="displayOrder"></param>
        /// <returns></returns>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> CreateZone([FromForm] string zoneName, [FromForm] string? zoneCode, [FromForm] string? description, [FromForm] int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(zoneName))
            {
                return Json(new { success = false, message = "Tên khu vực không được để trống!" });
            }

            var zone = new BuildingZone
            {
                ZoneName = zoneName.Trim(),
                ZoneCode = zoneCode?.Trim(),
                Description = description?.Trim(),
                DisplayOrder = displayOrder > 0 ? displayOrder : 1
            };

            _context.BuildingZones.Add(zone);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Thêm khu '{zone.ZoneName}' thành công!" });
        }

        /// <summary>
        /// 6. POST: /Facilities/CreateRoom (Thêm Phòng Học Mới)
        /// </summary>
        /// <param name="buildingZoneId"></param>
        /// <param name="roomName"></param>
        /// <param name="floorNumber"></param>
        /// <param name="capacity"></param>
        /// <param name="equipmentNotes"></param>
        /// <returns></returns>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> CreateRoom([FromForm] int buildingZoneId, [FromForm] string roomName, [FromForm] int floorNumber, [FromForm] int capacity, [FromForm] string? equipmentNotes)
        {
            if (buildingZoneId <= 0 || string.IsNullOrWhiteSpace(roomName))
            {
                return Json(new { success = false, message = "Vui lòng chọn khu vực và đặt tên phòng học!" });
            }

            var room = new ClassRoomLocation
            {
                BuildingZoneId = buildingZoneId,
                RoomName = roomName.Trim(),
                FloorNumber = floorNumber,
                Capacity = capacity > 0 ? capacity : 40,
                EquipmentNotes = equipmentNotes?.Trim(),
                IsAvailable = true
            };

            _context.ClassRoomLocations.Add(room);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Thêm phòng '{room.RoomName}' thành công!" });
        }
    }
}
