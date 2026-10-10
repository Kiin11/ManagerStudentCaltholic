using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
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
        private readonly ILessonPlanImportService _importService;

        public FacilitiesController(ParishDbContext context, ILogger<FacilitiesController> logger
            , ILessonPlanImportService importService)
        {
            _context = context;
            _logger = logger;
            _importService = importService;
        }
        /// <summary>
        /// 1. GET: /Facilities (Sơ đồ phòng học, Cảnh báo quá tải, Báo hỏng)
        /// </summary>
        /// <param name="zoneId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(DateTime? viewDate)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var currentYearId = currentYear?.Id ?? 0;

            // Ngày xem số lượng học sinh có mặt (mặc định là ngày hôm nay)
            var inputDate = viewDate ?? DateTime.Today;
            var targetDate = DateTime.SpecifyKind(inputDate.Date, DateTimeKind.Utc);
            ViewBag.ViewDate = targetDate.ToString("yyyy-MM-dd");

            var zones = await _context.BuildingZones
                .Include(z => z.Rooms)
                .OrderBy(z => z.DisplayOrder)
                .AsNoTracking()
                .ToListAsync();

            // 1. Lấy tất cả lớp học trong niên khóa hiện tại
            var allClasses = await _context.Classes
                .Include(c => c.Enrollments)
                .Where(c => c.AcademicYearId == currentYearId)
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.AllClasses = allClasses;

            // 2. Thống kê số lượng học sinh THỰC TẾ ĐI HỌC (ClassAttended == true) theo từng Lớp vào ngày targetDate
            var classIds = allClasses.Select(c => c.Id).ToList();

            var presentCountsByClass = await _context.Attendances
                .Include(a => a.Enrollment)
                .Where(a => a.AttendanceDate == targetDate &&
                            a.ClassAttended && // CHỈ TÍNH ĐI HỌC
                            (a.ClassStatus == "PRESENT" || a.ClassStatus == "LATE") &&
                            classIds.Contains(a.Enrollment.ClassRoomId))
                .GroupBy(a => a.Enrollment.ClassRoomId)
                .Select(g => new
                {
                    ClassId = g.Key,
                    PresentCount = g.Count()
                })
                .ToDictionaryAsync(x => x.ClassId, x => x.PresentCount);

            // 3. Gom nhóm theo từng phòng (RoomId -> MorningClass & AfternoonClass kèm số học sinh có mặt thực tế)
            // Key: RoomId -> (MorningClass, MorningPresent, AfternoonClass, AfternoonPresent)
            var roomOccupancy = new Dictionary<int, (ClassRoom? Morning, int MorningPresent, ClassRoom? Afternoon, int AfternoonPresent)>();

            foreach (var r in zones.SelectMany(z => z.Rooms))
            {
                var assigned = allClasses.Where(c => c.ClassRoomLocationId == r.Id).ToList();

                var morningClass = assigned.FirstOrDefault(c => c.GradeLevel == "Khai Tâm" || c.GradeLevel == "Rước Lễ" || c.GradeLevel == "Thêm Sức");
                var afternoonClass = assigned.FirstOrDefault(c => c.GradeLevel == "Bao Đồng");

                int morningPresent = morningClass != null && presentCountsByClass.ContainsKey(morningClass.Id)
                    ? presentCountsByClass[morningClass.Id] : 0;

                int afternoonPresent = afternoonClass != null && presentCountsByClass.ContainsKey(afternoonClass.Id)
                    ? presentCountsByClass[afternoonClass.Id] : 0;

                roomOccupancy[r.Id] = (morningClass, morningPresent, afternoonClass, afternoonPresent);
            }

            ViewBag.RoomOccupancy = roomOccupancy;

            // Danh sách sự cố chờ bảo trì
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

        #region Assign Room to Class APIs
        /// <summary>
        /// API Gán phòng học cho lớp - Cho phép 1 phòng có 1 lớp Sáng và 1 lớp Chiều
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> AssignRoomToClass([FromForm] int classId, [FromForm] int locationId)
        {
            try
            {
                if (classId <= 0 || locationId <= 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn lớp học và phòng học hợp lệ." });
                }

                var targetClass = await _context.Classes
                    .Include(c => c.Enrollments)
                    .FirstOrDefaultAsync(c => c.Id == classId);

                if (targetClass == null)
                    return Json(new { success = false, message = "Không tìm thấy thông tin lớp học." });

                var location = await _context.ClassRoomLocations.FindAsync(locationId);
                if (location == null)
                    return Json(new { success = false, message = "Không tìm thấy thông tin phòng học." });

                // 1. Phân định ca học:
                // Khối Sáng: Khai Tâm, Rước Lễ, Thêm Sức
                // Khối Chiều: Bao Đồng, Dự Bị
                bool isMorning = targetClass.GradeLevel == "Khai Tâm" || targetClass.GradeLevel == "Rước Lễ" || targetClass.GradeLevel == "Thêm Sức";
                string targetShift = isMorning ? "MORNING" : "AFTERNOON";
                string targetShiftName = isMorning ? "Ca Sáng (09:00)" : "Ca Chiều (15:00)";

                // 2. Kiểm tra xem phòng này đã có lớp nào CÙNG CA học chưa
                var conflictingClass = await _context.Classes
                    .FirstOrDefaultAsync(c => c.AcademicYearId == targetClass.AcademicYearId &&
                                              c.ClassRoomLocationId == locationId &&
                                              c.Id != targetClass.Id &&
                                              (isMorning
                                                  ? (c.GradeLevel == "Khai Tâm" || c.GradeLevel == "Rước Lễ" || c.GradeLevel == "Thêm Sức")
                                                  : (c.GradeLevel == "Bao Đồng" || c.GradeLevel == "Dự Bị")));

                if (conflictingClass != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Phòng '{location.RoomName}' đã được gán cho lớp '{conflictingClass.Name}' ({conflictingClass.GradeLevel}) trong {targetShiftName}. Mỗi phòng chỉ nhận tối đa 1 lớp Sáng và 1 lớp Chiều!"
                    });
                }

                // 3. Gán phòng và cập nhật Shift cho lớp
                targetClass.ClassRoomLocationId = locationId;
                targetClass.RoomName = location.RoomName;
                targetClass.Shift = targetShift;

                // 4. Đồng bộ vào ClassSchedules
                var schedule = await _context.ClassSchedules.FirstOrDefaultAsync(s => s.ClassRoomId == targetClass.Id);
                var startTime = isMorning ? new TimeSpan(9, 0, 0) : new TimeSpan(15, 0, 0);
                var endTime = isMorning ? new TimeSpan(10, 30, 0) : new TimeSpan(16, 30, 0);

                if (schedule != null)
                {
                    schedule.ClassRoomLocationId = locationId;
                    schedule.StartTime = startTime;
                    schedule.EndTime = endTime;
                    schedule.Shift = targetShift;
                }
                else
                {
                    _context.ClassSchedules.Add(new ClassSchedule
                    {
                        ClassRoomId = targetClass.Id,
                        ClassRoomLocationId = locationId,
                        DayOfWeek = DayOfWeek.Sunday,
                        StartTime = startTime,
                        EndTime = endTime,
                        Shift = targetShift
                    });
                }

                await _context.SaveChangesAsync();

                int enrolledCount = targetClass.Enrollments.Count;
                string warningMessage = enrolledCount > location.Capacity
                    ? $" (Lưu ý: Sĩ số {enrolledCount} em vượt sức chứa {location.Capacity} chỗ của phòng!)"
                    : "";

                return Json(new
                {
                    success = true,
                    message = $"Đã gán lớp '{targetClass.Name}' vào phòng '{location.RoomName}' ({targetShiftName}) thành công!{warningMessage}"
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Lỗi xử lý: " + inner });
            }
        }

        /// <summary>
        /// Hủy gán phòng học cho lớp
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> UnassignRoom([FromForm] int classId)
        {
            var cls = await _context.Classes.FindAsync(classId);
            if (cls == null) return Json(new { success = false, message = "Không tìm thấy lớp học." });

            cls.ClassRoomLocationId = null;
            cls.RoomName = string.Empty;

            var schedule = await _context.ClassSchedules.FirstOrDefaultAsync(s => s.ClassRoomId == classId);
            if (schedule != null)
            {
                _context.ClassSchedules.Remove(schedule);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Đã hủy xếp phòng cho lớp '{cls.Name}'." });
        }
        #endregion
    }
}
