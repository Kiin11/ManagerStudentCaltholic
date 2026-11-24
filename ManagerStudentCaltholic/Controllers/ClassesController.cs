using System.Security.Claims;
using DocumentFormat.OpenXml.Office2016.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class ClassesController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<ClassesController> _logger;
        private readonly IStudentExcelService _excelService;

        public ClassesController(ParishDbContext context, ILogger<ClassesController> logger,
            IStudentExcelService excelService)
        {
            _context = context;
            _logger = logger;
            _excelService = excelService;
        }

        private long CurrentUserId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        /// <summary>
        /// Kiểm tra quyền xem danh sách thiếu nhi / xuất dữ liệu của một Lớp học theo vai trò user.
        /// </summary>
        private async Task<bool> CanAccessClassRosterAsync(ClassRoom targetClass)
        {
            if (IsLeadership) return true;
            if (CurrentUserId <= 0) return false;

            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent);

            if (currentYear == null || targetClass.AcademicYearId != currentYear.Id)
            {
                return false;
            }

            if (User.IsInRole(UserRole.ProbationaryTeacher) &&
                GradeLevel.IsAfternoonSession(targetClass.Session, targetClass.GradeLevel))
            {
                return false;
            }

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    return false;
                }

                return string.Equals(targetClass.GradeLevel?.Trim(), managedGrade, StringComparison.OrdinalIgnoreCase);
            }

            return await _context.ClassTeachers
                .AsNoTracking()
                .AnyAsync(ct => ct.ClassRoomId == targetClass.Id
                    && ct.AcademicYearId == currentYear.Id
                    && ct.UserId == CurrentUserId);
        }

        /// <summary>
        /// 1. GET: /Classes?academicYearId=...&gradeLevel=...
        /// </summary>
        /// <param name="academicYearId"></param>
        /// <param name="gradeLevel"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(int? academicYearId, string? gradeLevel)
        {
            // Lấy danh sách toàn bộ niên khóa để đổ vào dropdown bộ lọc
            var academicYears = await _context.AcademicYears
                .OrderByDescending(y => y.StartDate)
                .AsNoTracking()
                .ToListAsync();

            if (!academicYears.Any())
            {
                TempData["ErrorMessage"] = "Chưa có niên khóa nào được tạo. Vui lòng tạo niên khóa trước khi quản lý lớp học.";
                return RedirectToAction("Index", "AcademicYears");
            }

            // Mặc định chọn Niên khóa đang kích hoạt (IsCurrent), nếu không có thì lấy niên khóa mới nhất
            var currentYear = academicYears.FirstOrDefault(y => y.IsCurrent);
            var defaultYear = currentYear ?? academicYears.First();
            var selectedYearId = academicYearId ?? defaultYear.Id;

            // Truy vấn danh sách lớp học theo niên khóa đã chọn
            var query = _context.Classes
                .Include(c => c.AcademicYear)
                .Include(c => c.Enrollments)
                .Include(c => c.ClassTeachers)
                .AsSplitQuery()
                .Where(c => c.AcademicYearId == selectedYearId);

            // BỘ LỌC PHẠM VI (SCOPE FILTER THEO VAI TRÒ)
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(managedGrade))
                {
                    var gradeLower = managedGrade.ToLowerInvariant();
                    // Ép buộc chỉ lấy các lớp thuộc đúng khối phụ trách
                    query = query.Where(c => c.GradeLevel.ToLower() == gradeLower);
                    ViewBag.SelectedGrade = managedGrade;
                    ViewBag.IsGradeLocked = true; // Cờ báo cho Razor View khóa dropdown chọn khối
                }
                else
                {
                    // Fail-closed: Trưởng khối chưa được gán ManagedGradeLevel không thấy lớp nào
                    query = query.Where(_ => false);
                    ViewBag.SelectedGrade = "NONE";
                    ViewBag.IsGradeLocked = true;
                }
            }
            else
            {
                if (!IsLeadership)
                {
                    var uid = CurrentUserId;
                    if (uid <= 0 || currentYear == null || selectedYearId != currentYear.Id)
                    {
                        query = query.Where(_ => false);
                    }
                    else
                    {
                        query = query.Where(c =>
                            c.AcademicYearId == currentYear.Id &&
                            c.AcademicYear.IsCurrent &&
                            c.ClassTeachers.Any(ct => ct.UserId == uid && ct.AcademicYearId == currentYear.Id));

                        if (User.IsInRole(UserRole.ProbationaryTeacher))
                        {
                            query = query.Where(c =>
                                c.Session != GradeLevel.SessionAfternoon &&
                                c.GradeLevel != GradeLevel.BaoDong &&
                                c.GradeLevel != "Bao Dong" &&
                                c.GradeLevel != GradeLevel.DuBi &&
                                c.GradeLevel != "Du Bi" &&
                                c.GradeLevel != GradeLevel.DuBiCode);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(gradeLevel) && gradeLevel != "ALL")
                {
                    if (GradeLevel.IsProbationaryGrade(gradeLevel))
                    {
                        query = query.Where(c => c.GradeLevel == GradeLevel.DuBi || c.GradeLevel == GradeLevel.DuBiCode);
                        ViewBag.SelectedGrade = GradeLevel.DuBi;
                    }
                    else
                    {
                        query = query.Where(c => c.GradeLevel == gradeLevel);
                        ViewBag.SelectedGrade = gradeLevel;
                    }
                }
                else
                {
                    ViewBag.SelectedGrade = "ALL";
                }
            }

            var classes = await query
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.AcademicYears = new SelectList(academicYears, "Id", "Name", selectedYearId);
            ViewBag.SelectedYearId = selectedYearId;

            return View(classes);
        }

        /// <summary>
        /// 2. POST: /Classes/Create (Tạo lớp học mới)
        /// </summary>
        /// <param name="classRoom"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> Create(ClassRoom classRoom)
        {
            // Bỏ kiểm tra validate các thuộc tính quan hệ (đối tượng điều hướng)
            ModelState.Remove("AcademicYear");
            ModelState.Remove("ClassTeachers");
            ModelState.Remove("Enrollments");
            ModelState.Remove("ClassRoomLocation");

            if (string.IsNullOrWhiteSpace(classRoom.Name))
            {
                ModelState.AddModelError("Name", "Tên lớp học không được để trống.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu nhập không hợp lệ, vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Index), new { academicYearId = classRoom.AcademicYearId });
            }

            // SEC-NEW-05 FIX: Nếu là Trưởng khối (BranchHead), bắt buộc chỉ được tạo lớp thuộc khối mình phụ trách
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade) ||
                    !string.Equals(classRoom.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = $"Bạn chỉ có quyền tạo lớp thuộc khối '{managedGrade}'!";
                    return RedirectToAction(nameof(Index), new { academicYearId = classRoom.AcademicYearId });
                }
            }

            // 1. Đồng bộ ClassRoomLocationId và RoomName
            if (!string.IsNullOrWhiteSpace(classRoom.RoomName))
            {
                var roomNameTrimmed = classRoom.RoomName.Trim();
                var location = await _context.ClassRoomLocations
                    .FirstOrDefaultAsync(r => r.RoomName.ToLower() == roomNameTrimmed.ToLower());

                if (location != null)
                {
                    classRoom.ClassRoomLocationId = location.Id;
                    classRoom.RoomName = location.RoomName;
                }
            }

            if (GradeLevel.IsProbationaryGrade(classRoom.GradeLevel))
            {
                classRoom.GradeLevel = GradeLevel.DuBi;
            }
            classRoom.Session = GradeLevel.ResolveSession(classRoom.GradeLevel);
            if (string.IsNullOrWhiteSpace(classRoom.Shift))
            {
                classRoom.Shift = classRoom.Session == GradeLevel.SessionAfternoon ? "Chiều CN" : "Sáng CN";
            }

            _context.Classes.Add(classRoom);
            await _context.SaveChangesAsync();

            // 2. Tự động đồng bộ sang bảng ClassSchedules để sơ đồ mặt bằng hiển thị ngay
            if (classRoom.ClassRoomLocationId.HasValue)
            {
                bool isMorning = GradeLevel.IsMorningSession(classRoom.Session, classRoom.GradeLevel);
                var schedule = new ClassSchedule
                {
                    ClassRoomId = classRoom.Id,
                    ClassRoomLocationId = classRoom.ClassRoomLocationId.Value,
                    DayOfWeek = DayOfWeek.Sunday,
                    StartTime = isMorning ? new TimeSpan(9, 0, 0) : new TimeSpan(15, 0, 0),
                    EndTime = isMorning ? new TimeSpan(10, 30, 0) : new TimeSpan(16, 30, 0),
                    Shift = isMorning ? "MORNING" : "AFTERNOON"
                };
                _context.ClassSchedules.Add(schedule);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Tạo lớp '{classRoom.Name}' thành công!";
            return RedirectToAction(nameof(Index), new { academicYearId = classRoom.AcademicYearId });
        }

        /// <summary>
        /// TASK-03.1 (Phase 6): Khởi tạo nhanh 4 lớp chuẩn Khối Dự Bị (Dự Bị 1..4) mặc định Session = Afternoon
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> QuickCreateProbationaryClasses(int academicYearId)
        {
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!GradeLevel.IsProbationaryGrade(managedGrade))
                {
                    TempData["ErrorMessage"] = $"Bạn chỉ có quyền khởi tạo lớp thuộc khối '{managedGrade}'!";
                    return RedirectToAction(nameof(Index), new { academicYearId });
                }
            }

            var targetYear = academicYearId > 0
                ? await _context.AcademicYears.FirstOrDefaultAsync(y => y.Id == academicYearId)
                : await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);

            if (targetYear == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy niên khóa hợp lệ để khởi tạo 4 lớp Dự Bị.";
                return RedirectToAction(nameof(Index));
            }

            var existingNames = await _context.Classes
                .Where(c => c.AcademicYearId == targetYear.Id)
                .Select(c => c.Name)
                .ToListAsync();

            var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
            var createdNames = new List<string>();

            foreach (var className in GradeLevel.DefaultProbationaryClassNames)
            {
                if (existingSet.Contains(className))
                    continue;

                _context.Classes.Add(new ClassRoom
                {
                    Name = className,
                    GradeLevel = GradeLevel.DuBi,
                    Session = GradeLevel.SessionAfternoon,
                    Shift = "Chiều CN",
                    AcademicYearId = targetYear.Id
                });
                createdNames.Add(className);
            }

            if (createdNames.Any())
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã khởi tạo nhanh {createdNames.Count} lớp Khối Dự Bị (Ca Chiều): {string.Join(", ", createdNames)}!";
            }
            else
            {
                TempData["SuccessMessage"] = $"Niên khóa {targetYear.Name} đã có đủ 4 lớp chuẩn Khối Dự Bị (Dự Bị 1..4).";
            }

            return RedirectToAction(nameof(Index), new { academicYearId = targetYear.Id, gradeLevel = GradeLevel.DuBi });
        }
        /// <summary>
        /// 3. POST: /Classes/Delete/{id} (Xóa lớp học)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Policy = "RequireExecutiveBoard")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var targetClass = await _context.Classes
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (targetClass == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            if (targetClass.Enrollments.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa lớp '{targetClass.Name}' vì đã có {targetClass.Enrollments.Count} thiếu nhi trong danh sách lớp.";
                return RedirectToAction(nameof(Index), new { academicYearId = targetClass.AcademicYearId });
            }

            try
            {
                _context.Classes.Remove(targetClass);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Đã xóa lớp học: {Name} (ID: {Id})", targetClass.Name, id);
                TempData["SuccessMessage"] = $"Đã xóa lớp '{targetClass.Name}' thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa lớp học ID: {Id}", id);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa lớp học.";
            }

            return RedirectToAction(nameof(Index), new { academicYearId = targetClass.AcademicYearId });
        }

        /// <summary>
        /// Get Techers for a specific class, ordered by role (HEAD, MEMBER, then others).
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetTeachers(int classId)
        {
            if (classId <= 0)
            {
                return BadRequest(new { success = false, message = "Mã lớp học không hợp lệ." });
            }

            var targetClass = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (targetClass == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy lớp học yêu cầu." });
            }

            if (!await CanAccessClassRosterAsync(targetClass))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Bạn không có quyền xem danh sách GLV của lớp này." });
            }

            var teachers = await _context.ClassTeachers
                .Where(t => t.ClassRoomId == classId)
                .OrderBy(t => t.RoleInClass == "HEAD" ? 1 : (t.RoleInClass == "MEMBER" ? 2 : 3))
                .Select(t => new
                {
                    t.Id,
                    t.TeacherName,
                    t.PhoneNumber,
                    t.RoleInClass,
                    RoleName = t.RoleInClass == "HEAD" ? "Chủ nhiệm" : (t.RoleInClass == "MEMBER" ? "Đồng hành" : "Dự bị / Trợ tá")
                })
                .AsNoTracking()
                .ToListAsync();

            return Json(new { success = true, data = teachers });
        }

        /// <summary>
        /// Action Phân công thêm GLV vào lớp
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> AssignTeacher([FromBody] AssignTeacherRequestDto request)
        {
            try
            {
                if (request == null || request.UserId <= 0 || request.ClassRoomId <= 0)
                {
                    return Json(new { success = false, message = "Dữ liệu phân công không hợp lệ." });
                }

                var targetClass = await _context.Classes.FindAsync(request.ClassRoomId);
                if (targetClass == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học yêu cầu." });
                }

                // 1. Kiểm tra Scope phân quyền cho Trưởng khối (BranchHead)
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(managedGrade) ||
                        !string.Equals(targetClass.GradeLevel?.Trim(), managedGrade, StringComparison.OrdinalIgnoreCase))
                    {
                        return Json(new { success = false, message = $"Bạn chỉ có quyền phân công cho các lớp thuộc khối {managedGrade}!" });
                    }
                }

                // 2. Lấy thông tin tài khoản User
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null || !user.IsActive)
                {
                    return Json(new { success = false, message = "Tài khoản GLV không tồn tại hoặc đã bị khóa." });
                }

                if (user.Role == UserRole.Admin || user.Role == UserRole.SpiritualDirector || user.Role == UserRole.Parent)
                {
                    return Json(new { success = false, message = "Không thể phân công Quản trị viên, Cha Tuyên Úy hoặc Phụ huynh làm GLV giảng dạy." });
                }

                // 3. Kiểm tra xem GLV đã được phân công vào chính lớp này trong niên khóa chưa
                var isAlreadyInThisClass = await _context.ClassTeachers.AnyAsync(ct =>
                    ct.ClassRoomId == request.ClassRoomId &&
                    ct.AcademicYearId == targetClass.AcademicYearId &&
                    (ct.UserId == user.Id || ct.TeacherName == user.Username));

                if (isAlreadyInThisClass)
                {
                    return Json(new { success = false, message = $"Giáo lý viên này đã được phân công vào lớp {targetClass.Name} rồi." });
                }

                // =========================================================================
                // 4. LOGIC PHÂN CÔNG CA HỌC (SESSION) & CHẶN DỰ BỊ DẠY CA CHIỀU (TASK-03.2)
                // =========================================================================
                // Ca Sáng (Morning): Khai Tâm, Rước Lễ, Thêm Sức
                // Ca Chiều (Afternoon): Bao Đồng, Dự Bị
                bool isTargetAfternoon = GradeLevel.IsAfternoonSession(targetClass.Session, targetClass.GradeLevel);
                bool isTargetMorning = !isTargetAfternoon;

                // TASK-03.2: Chặn tuyệt đối tài khoản GLV Dự Bị đứng lớp dạy các lớp thuộc Ca Chiều (Bao Đồng, Dự Bị)
                if (user.Role == UserRole.ProbationaryTeacher && isTargetAfternoon)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Chặn phân công: Tài khoản '{user.Username}' có vai trò GLV Dự Bị (ProbationaryTeacher), chỉ được phép giảng dạy/trợ tá các lớp Ca Sáng (Khai Tâm, Rước Lễ, Thêm Sức) và không được đứng lớp Ca Chiều ({targetClass.GradeLevel})!"
                    });
                }

                // Lấy toàn bộ các lớp mà GLV này đang dạy trong cùng niên khóa
                var existingAssignments = await _context.ClassTeachers
                    .Include(ct => ct.ClassRoom)
                    .Where(ct => ct.AcademicYearId == targetClass.AcademicYearId &&
                                (ct.UserId == user.Id || ct.TeacherName == user.Username))
                    .ToListAsync();

                // 4.1. GLV Dự Bị chỉ được trợ giảng tối đa 1 lớp Ca Sáng
                if (user.Role == UserRole.ProbationaryTeacher && existingAssignments.Count >= 1)
                {
                    var assignedClassNames = string.Join(", ", existingAssignments.Select(a => $"{a.ClassRoom.Name} ({a.ClassRoom.GradeLevel})"));
                    return Json(new
                    {
                        success = false,
                        message = $"GLV Dự Bị '{user.Username}' đang trợ giảng lớp Ca Sáng ({assignedClassNames}). Chỉ được phân công tối đa 1 lớp Ca Sáng!"
                    });
                }

                // 4.2. Kiểm tra nếu đã dạy đủ 2 lớp trong niên khóa
                if (existingAssignments.Count >= 2)
                {
                    var assignedClassNames = string.Join(", ", existingAssignments.Select(a => $"{a.ClassRoom.Name} ({a.ClassRoom.GradeLevel})"));
                    return Json(new
                    {
                        success = false,
                        message = $"Giáo lý viên đã đủ định mức 2 lớp trong niên khóa này ({assignedClassNames}). Không thể phân công thêm!"
                    });
                }

                // 4.3. Kiểm tra nếu đã dạy 1 lớp thuộc Ca Sáng
                if (isTargetMorning)
                {
                    var morningClass = existingAssignments.FirstOrDefault(a => !GradeLevel.IsAfternoonSession(a.ClassRoom.Session, a.ClassRoom.GradeLevel));
                    if (morningClass != null)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Giáo lý viên đã phụ trách lớp Ca Sáng: '{morningClass.ClassRoom.Name} ({morningClass.ClassRoom.GradeLevel})'. Mỗi GLV chỉ được dạy tối đa 1 lớp Ca Sáng (Khai Tâm, Rước Lễ, Thêm Sức)!"
                        });
                    }
                }

                // 4.4. Kiểm tra nếu đã dạy 1 lớp thuộc Ca Chiều (Bao Đồng, Dự Bị)
                if (isTargetAfternoon)
                {
                    var afternoonClass = existingAssignments.FirstOrDefault(a => GradeLevel.IsAfternoonSession(a.ClassRoom.Session, a.ClassRoom.GradeLevel));
                    if (afternoonClass != null)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Giáo lý viên đã phụ trách lớp Ca Chiều: '{afternoonClass.ClassRoom.Name} ({afternoonClass.ClassRoom.GradeLevel})'. Mỗi GLV chỉ được dạy tối đa 1 lớp Ca Chiều!"
                        });
                    }
                }

                // 5. Xác định họ tên hiển thị chuẩn
                var nameCandidate = $"{user.ChristianName} {user.FirstName} {user.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(nameCandidate))
                {
                    nameCandidate = user.Username;
                }

                var teacherDisplayName = !string.IsNullOrWhiteSpace(user.ChristianName) && !nameCandidate.Contains(user.ChristianName)
                    ? $"{user.ChristianName} {nameCandidate}".Trim()
                    : nameCandidate;

                // 6. Tạo bản ghi ClassTeacher mới
                var classTeacher = new ClassTeacher
                {
                    ClassRoomId = targetClass.Id,
                    AcademicYearId = targetClass.AcademicYearId,
                    UserId = user.Id,
                    TeacherName = teacherDisplayName,
                    PhoneNumber = string.IsNullOrWhiteSpace(user.PhoneNumber) ? null : user.PhoneNumber.Trim(),
                    RoleInClass = string.IsNullOrWhiteSpace(request.RoleInClass) ? "HEAD" : request.RoleInClass,
                    AssignedAt = DateTime.UtcNow
                };

                _context.ClassTeachers.Add(classTeacher);
                await _context.SaveChangesAsync();

                var shiftName = isTargetMorning ? "Khối Sáng" : "Khối Chiều";
                return Json(new
                {
                    success = true,
                    message = $"Đã phân công GLV '{teacherDisplayName}' vào lớp {targetClass.Name} ({shiftName}) thành công!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống tại ClassesController.{Action}", nameof(AssignTeacher));
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi xử lý yêu cầu. Vui lòng thử lại sau." });
            }
        }

        /// <summary>
        /// Action Hủy phân công GLV
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> RemoveTeacher(long id)
        {
            var item = await _context.ClassTeachers
        .Include(ct => ct.ClassRoom)
        .FirstOrDefaultAsync(ct => ct.Id == id);

            if (item == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bản ghi phân công." });
            }

            // SEC-NEW-05 FIX: Chặn Trưởng khối hủy phân công GLV của khối khác
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade) ||
                    !string.Equals(item.ClassRoom.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = $"Bạn chỉ có quyền hủy phân công GLV thuộc khối {managedGrade}!" });
                }
            }

            _context.ClassTeachers.Remove(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã hủy phân công GLV." });
        }

        /// <summary>
        /// Export Excel danh sách thiếu nhi trong lớp học
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> ExportExcel(int id)
        {
            try
            {
                var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                if (targetClass == null) return NotFound();

                // Kiểm tra phạm vi quyền hạn theo Khối / Lớp / Ca học
                if (!await CanAccessClassRosterAsync(targetClass))
                {
                    return Forbid();
                }

                var bytes = await _excelService.ExportClassListToExcelAsync(id);
                var fileName = $"Danh_Sach_Lop_{targetClass.Name.Replace(" ", "_")}.xlsx";

                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xuất danh sách lớp ID {ClassId}", id);
                TempData["ErrorMessage"] = "Không thể xuất danh sách lớp lúc này.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// GET List of available teachers (excluding Admin and SpiritualDirector) for assignment to classes.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Roles = UserRole.Admin + "," + UserRole.SpiritualDirector + "," + UserRole.ExecutiveBoard + "," + UserRole.BranchHead)]
        public async Task<IActionResult> GetAvailableTeachers(int? classRoomId)
        {
            try
            {
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(managedGrade))
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Tài khoản Trưởng khối chưa được gán Khối phụ trách." });
                    }

                    if (classRoomId.HasValue && classRoomId.Value > 0)
                    {
                        var scopedClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classRoomId.Value);
                        if (scopedClass != null && !string.Equals(scopedClass.GradeLevel?.Trim(), managedGrade, StringComparison.OrdinalIgnoreCase))
                        {
                            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = $"Bạn chỉ có quyền xem danh sách phân công cho khối {managedGrade}." });
                        }
                    }
                }

                // 1. Xác định niên khóa hiện tại
                var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
                var yearId = currentYear?.Id ?? 0;

                // 2. Xác định ca của lớp đang được chọn phân công (nếu có truyền classRoomId)
                bool isTargetMorning = false;
                bool isTargetAfternoon = false;

                if (classRoomId.HasValue && classRoomId.Value > 0)
                {
                    var targetClass = await _context.Classes.FindAsync(classRoomId.Value);
                    if (targetClass != null)
                    {
                        yearId = targetClass.AcademicYearId;
                        // Ca Sáng (Morning): Khai Tâm, Rước Lễ, Thêm Sức
                        // Ca Chiều (Afternoon): Bao Đồng, Dự Bị
                        isTargetAfternoon = GradeLevel.IsAfternoonSession(targetClass.Session, targetClass.GradeLevel);
                        isTargetMorning = !isTargetAfternoon;
                    }
                }

                // 3. Lấy toàn bộ tài khoản Users hợp lệ (loại trừ Admin, Cha Tuyên Úy, Phụ huynh)
                var users = await _context.Users
                    .Where(u => u.IsActive &&
                                u.Role != UserRole.Admin &&
                                u.Role != UserRole.SpiritualDirector &&
                                u.Role != UserRole.Parent)
                    .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        FullName = $"{u.ChristianName} {u.FirstName} {u.LastName}".Trim(),
                        u.PhoneNumber,
                        u.Role,
                        u.ManagedGradeLevel
                    })
                    .AsNoTracking()
                    .ToListAsync();

                // 4. Lấy tất cả phân công của các GLV trong niên khóa này để kiểm tra định mức
                var currentAssignments = await _context.ClassTeachers
                    .Include(ct => ct.ClassRoom)
                    .Where(ct => ct.AcademicYearId == yearId && ct.UserId.HasValue)
                    .AsNoTracking()
                    .ToListAsync();

                // Nhóm phân công theo UserId
                var assignmentsByUser = currentAssignments
                    .GroupBy(ct => ct.UserId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var availableTeachers = new List<object>();

                foreach (var u in users)
                {
                    assignmentsByUser.TryGetValue(u.Id, out var userClasses);
                    userClasses ??= new List<ClassTeacher>();

                    // TASK-03.2: Chặn tuyệt đối tài khoản GLV Dự Bị hiển thị trong dropdown lớp Ca Chiều (Bao Đồng, Dự Bị)
                    if (isTargetAfternoon && u.Role == UserRole.ProbationaryTeacher)
                    {
                        continue;
                    }

                    // GLV Dự Bị chỉ dạy tối đa 1 lớp Ca Sáng
                    if (u.Role == UserRole.ProbationaryTeacher && userClasses.Count >= 1)
                    {
                        continue;
                    }

                    // Nếu đã đủ 2 lớp -> Ẩn hoàn toàn khỏi danh sách
                    if (userClasses.Count >= 2)
                    {
                        continue;
                    }

                    // Nếu đang mở lớp CA SÁNG: Ẩn những GLV đã dạy 1 lớp Ca Sáng (Khai Tâm, Rước Lễ, Thêm Sức)
                    if (isTargetMorning)
                    {
                        bool hasMorning = userClasses.Any(c => !GradeLevel.IsAfternoonSession(c.ClassRoom.Session, c.ClassRoom.GradeLevel));
                        if (hasMorning)
                        {
                            continue; // Ẩn đi
                        }
                    }

                    // Nếu đang mở lớp CA CHIỀU: Ẩn những GLV đã dạy 1 lớp Ca Chiều (Bao Đồng, Dự Bị)
                    if (isTargetAfternoon)
                    {
                        bool hasAfternoon = userClasses.Any(c => GradeLevel.IsAfternoonSession(c.ClassRoom.Session, c.ClassRoom.GradeLevel));
                        if (hasAfternoon)
                        {
                            continue; // Ẩn đi
                        }
                    }

                    // Ghi chú trạng thái hiện tại để hiển thị trên UI
                    string statusNote = "";
                    if (userClasses.Count == 1)
                    {
                        var existingClass = userClasses.First();
                        statusNote = GradeLevel.IsAfternoonSession(existingClass.ClassRoom.Session, existingClass.ClassRoom.GradeLevel)
                            ? " (Đã dạy Chiều: " + existingClass.ClassRoom.Name + ")"
                            : " (Đã dạy Sáng: " + existingClass.ClassRoom.Name + ")";
                    }

                    availableTeachers.Add(new
                    {
                        u.Id,
                        u.Username,
                        u.FullName,
                        u.PhoneNumber,
                        u.Role,
                        u.ManagedGradeLevel,
                        AssignedCount = userClasses.Count,
                        StatusNote = statusNote
                    });
                }

                return Json(new { success = true, data = availableTeachers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống tại ClassesController.{Action}", nameof(AssignTeacher));
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi xử lý yêu cầu. Vui lòng thử lại sau." });
            }
        }

        /// <summary>
        /// API lấy danh sách các phòng học còn trống theo ca học của khối trong niên khóa
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailableRooms(int academicYearId, string gradeLevel)
        {
            try
            {
                // 1. Xác định ca của khối được chọn:
                // Khối Sáng (Morning): Khai Tâm, Rước Lễ, Thêm Sức
                // Khối Chiều (Afternoon): Bao Đồng, Dự Bị
                bool isMorning = GradeLevel.IsMorningGrade(gradeLevel);

                // 2. Lấy toàn bộ phòng học (bỏ qua phòng HĐGX và Nhà Sinh Hoạt Chung)
                var allRooms = await _context.ClassRoomLocations
                    .Include(r => r.BuildingZone)
                    .Where(r => r.IsAvailable &&
                                !r.RoomName.Contains("HĐGX") &&
                                !r.RoomName.Contains("Sinh Hoạt Chung"))
                    .OrderBy(r => r.BuildingZone.DisplayOrder)
                    .ThenBy(r => r.FloorNumber)
                    .ThenBy(r => r.RoomName)
                    .AsNoTracking()
                    .ToListAsync();

                // 3. Tìm các phòng đã được lớp khác sử dụng cùng ca trong niên khóa này
                var occupiedRoomIds = await _context.Classes
                    .Where(c => c.AcademicYearId == academicYearId && c.ClassRoomLocationId.HasValue)
                    .Where(c => isMorning
                        ? (c.Session != GradeLevel.SessionAfternoon &&
                           c.GradeLevel != GradeLevel.BaoDong &&
                           c.GradeLevel != GradeLevel.DuBi &&
                           c.GradeLevel != GradeLevel.DuBiCode)
                        : (c.Session == GradeLevel.SessionAfternoon ||
                           c.GradeLevel == GradeLevel.BaoDong ||
                           c.GradeLevel == GradeLevel.DuBi ||
                           c.GradeLevel == GradeLevel.DuBiCode))
                    .Select(c => c.ClassRoomLocationId!.Value)
                    .Distinct()
                    .ToListAsync();

                // 4. Lọc ra các phòng còn trống cho ca này
                var availableRooms = allRooms
                    .Where(r => !occupiedRoomIds.Contains(r.Id))
                    .Select(r => new
                    {
                        r.Id,
                        r.RoomName,
                        ZoneName = r.BuildingZone?.ZoneName ?? "Khu vực khác",
                        r.FloorNumber,
                        r.Capacity
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    shiftName = isMorning ? "Ca Sáng (09:00)" : "Ca Chiều (15:00)",
                    data = availableRooms
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống tại ClassesController.{Action}", nameof(GetAvailableRooms));
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi xử lý yêu cầu. Vui lòng thử lại sau." });
            }
        }

        /// <summary>
        /// GET: /Classes/GetClassStudents?classId=...
        /// Lấy danh sách thiếu nhi của lớp học theo quyền truy cập của user hiện tại.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetClassStudents(int classId)
        {
            try
            {
                if (classId <= 0)
                {
                    return Json(new { success = false, message = "Mã lớp học không hợp lệ." });
                }

                var targetClass = await _context.Classes
                    .Include(c => c.AcademicYear)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == classId);

                if (targetClass == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lớp học yêu cầu." });
                }

                if (!await CanAccessClassRosterAsync(targetClass))
                {
                    return Json(new { success = false, message = "Bạn không có quyền xem danh sách thiếu nhi của lớp này." });
                }

                bool isAfternoon = GradeLevel.IsAfternoonSession(targetClass.Session, targetClass.GradeLevel);

                var students = await _context.Enrollments
                    .AsNoTracking()
                    .Include(e => e.Student)
                    .Where(e => e.ClassRoomId == classId && e.Student != null && !e.Student.IsDeleted)
                    .OrderBy(e => e.Student.LastName)
                    .ThenBy(e => e.Student.FirstName)
                    .ThenBy(e => e.Student.StudentCode)
                    .Select(e => new
                    {
                        id = e.Student.Id,
                        studentCode = e.Student.StudentCode,
                        christianName = e.Student.ChristianName,
                        fullName = (e.Student.FirstName + " " + e.Student.LastName).Trim(),
                        gender = e.Student.Gender,
                        dateOfBirth = e.Student.DateOfBirth.ToString("dd/MM/yyyy"),
                        parentPhone = e.Student.ParentPhone,
                        isActive = e.Student.IsActive
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        classId = targetClass.Id,
                        className = targetClass.Name,
                        gradeLevel = targetClass.GradeLevel,
                        sessionDisplay = isAfternoon ? "Ca Chiều" : "Ca Sáng",
                        roomName = string.IsNullOrWhiteSpace(targetClass.RoomName) ? "Chưa bố trí" : targetClass.RoomName,
                        academicYearName = targetClass.AcademicYear?.Name ?? string.Empty,
                        totalStudents = students.Count,
                        students
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi lấy danh sách thiếu nhi lớp ID {ClassId}", classId);
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi tải danh sách lớp." });
            }
        }
    }

}
