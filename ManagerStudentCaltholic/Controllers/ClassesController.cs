using DocumentFormat.OpenXml.Office2016.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
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
            var defaultYear = academicYears.FirstOrDefault(y => y.IsCurrent) ?? academicYears.First();
            var selectedYearId = academicYearId ?? defaultYear.Id;

            // Truy vấn danh sách lớp học theo niên khóa đã chọn
            var query = _context.Classes
                .Include(c => c.AcademicYear)
                .Include(c => c.Enrollments)
                .Include(c=> c.ClassTeachers)
                .Where(c => c.AcademicYearId == selectedYearId);

            // BỘ LỌC PHẠM VI (SCOPE FILTER CHO TRƯỞNG KHỐI)
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrEmpty(managedGrade))
                {
                    // Ép buộc chỉ lấy các lớp thuộc đúng khối phụ trách
                    query = query.Where(c => c.GradeLevel == managedGrade);
                    ViewBag.SelectedGrade = managedGrade;
                    ViewBag.IsGradeLocked = true; // Cờ báo cho Razor View khóa dropdown chọn khối
                }
            }
            else if (!string.IsNullOrEmpty(gradeLevel))
            {
                query = query.Where(c => c.GradeLevel == gradeLevel);
                ViewBag.SelectedGrade = gradeLevel;
            }

            var classes = await query
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.AcademicYears = new SelectList(academicYears, "Id", "Name", selectedYearId);
            ViewBag.SelectedYearId = selectedYearId;
            ViewBag.SelectedGrade = gradeLevel ?? "ALL";

            return View(classes);
        }

        /// <summary>
        /// 2. POST: /Classes/Create (Tạo lớp học mới)
        /// </summary>
        /// <param name="classRoom"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,GradeLevel,RoomName,AcademicYearId")] ClassRoom classRoom)
        {
            // Nếu là Trưởng khối thì bắt buộc lớp tạo ra phải thuộc khối phụ trách
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.Equals(classRoom.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = $"Bạn chỉ có quyền tạo lớp học thuộc khối {managedGrade}!";
                    return RedirectToAction(nameof(Index), new { academicYearId = classRoom.AcademicYearId });
                }
            }

            // Kiểm tra trùng tên lớp trong cùng một niên khóa
            var isDuplicate = await _context.Classes.AnyAsync(c =>
                c.AcademicYearId == classRoom.AcademicYearId &&
                c.Name.Trim().ToLower() == classRoom.Name.Trim().ToLower());

            if (isDuplicate)
            {
                ModelState.AddModelError("Name", $"Lớp '{classRoom.Name}' đã tồn tại trong niên khóa này.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu nhập không hợp lệ, vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Index), new { academicYearId = classRoom.AcademicYearId });
            }

            try
            {
                _context.Classes.Add(classRoom);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tạo thành công lớp học: {Name} - Khối: {GradeLevel} - Niên khóa ID: {YearId}",
                    classRoom.Name, classRoom.GradeLevel, classRoom.AcademicYearId);

                TempData["SuccessMessage"] = $"Tạo lớp '{classRoom.Name}' thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi tạo lớp học {Name}", classRoom.Name);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu thông tin lớp học.";
            }

            return RedirectToAction(nameof(Index), new { academicYearId = classRoom.AcademicYearId, gradeLevel = classRoom.GradeLevel });
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
        public class AssignTeacherRequestDto
        {
            public int ClassRoomId { get; set; }
            public long UserId { get; set; }
            public string RoleInClass { get; set; } = "HEAD";
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
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
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    if (!string.Equals(targetClass.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
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

                if (user.Role == UserRole.Admin || user.Role == UserRole.SpiritualDirector)
                {
                    return Json(new { success = false, message = "Không thể phân công Cha Tuyên Úy hoặc Admin làm GLV giảng dạy." });
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
                // 4. LOGIC MỚI: QUY ĐỊNH PHÂN CÔNG TỐI ĐA 2 LỚP (1 KHỐI SÁNG & 1 KHỐI CHIỀU)
                // =========================================================================
                // Khối Sáng: Khai Tâm, Rước Lễ, Thêm Sức
                // Khối Chiều: Bao Đồng
                bool isTargetMorning = targetClass.GradeLevel != "Bao Đồng"; // Khai Tâm, Rước Lễ, Thêm Sức
                bool isTargetAfternoon = targetClass.GradeLevel == "Bao Đồng";

                // Lấy toàn bộ các lớp mà GLV này đang dạy trong cùng niên khóa
                var existingAssignments = await _context.ClassTeachers
                    .Include(ct => ct.ClassRoom)
                    .Where(ct => ct.AcademicYearId == targetClass.AcademicYearId &&
                                (ct.UserId == user.Id || ct.TeacherName == user.Username))
                    .ToListAsync();

                // 4.1. Kiểm tra nếu đã dạy đủ 2 lớp trong niên khóa
                if (existingAssignments.Count >= 2)
                {
                    var assignedClassNames = string.Join(", ", existingAssignments.Select(a => $"{a.ClassRoom.Name} ({a.ClassRoom.GradeLevel})"));
                    return Json(new
                    {
                        success = false,
                        message = $"Giáo lý viên đã đủ định mức 2 lớp trong niên khóa này ({assignedClassNames}). Không thể phân công thêm!"
                    });
                }

                // 4.2. Kiểm tra nếu đã dạy 1 lớp thuộc Khối Sáng
                if (isTargetMorning)
                {
                    var morningClass = existingAssignments.FirstOrDefault(a => a.ClassRoom.GradeLevel != "Bao Đồng");
                    if (morningClass != null)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Giáo lý viên đã phụ trách lớp Khối Sáng: '{morningClass.ClassRoom.Name} ({morningClass.ClassRoom.GradeLevel})'. Mỗi GLV chỉ được dạy tối đa 1 lớp Khối Sáng (Khai Tâm, Rước Lễ, Thêm Sức)!"
                        });
                    }
                }

                // 4.3. Kiểm tra nếu đã dạy 1 lớp thuộc Khối Chiều (Bao Đồng)
                if (isTargetAfternoon)
                {
                    var afternoonClass = existingAssignments.FirstOrDefault(a => a.ClassRoom.GradeLevel == "Bao Đồng");
                    if (afternoonClass != null)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Giáo lý viên đã phụ trách lớp Khối Chiều: '{afternoonClass.ClassRoom.Name} (Bao Đồng)'. Mỗi GLV chỉ được dạy tối đa 1 lớp Khối Chiều!"
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
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Lỗi Database/Server: " + innerMsg });
            }
        }

        /// <summary>
        /// Action Hủy phân công GLV
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> RemoveTeacher(long id)
        {
            var item = await _context.ClassTeachers.FindAsync(id);
            if (item == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bản ghi phân công." });
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
                var targetClass = await _context.Classes.FindAsync(id);
                if (targetClass == null) return NotFound();

                var bytes = await _excelService.ExportClassListToExcelAsync(id);
                var fileName = $"Danh_Sach_Lop_{targetClass.Name.Replace(" ", "_")}.xlsx";

                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xuất danh sách: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// GET List of available teachers (excluding Admin and SpiritualDirector) for assignment to classes.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        // GET: /Classes/GetAvailableTeachers?classRoomId=...
        [HttpGet]
        public async Task<IActionResult> GetAvailableTeachers(int? classRoomId)
        {
            try
            {
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
                        // Khối Sáng: Khai Tâm, Rước Lễ, Thêm Sức
                        // Khối Chiều: Bao Đồng
                        isTargetMorning = targetClass.GradeLevel != "Bao Đồng";
                        isTargetAfternoon = targetClass.GradeLevel == "Bao Đồng";
                    }
                }

                // 3. Lấy toàn bộ tài khoản Users hợp lệ (loại trừ Admin & Cha Tuyên Úy)
                var users = await _context.Users
                    .Where(u => u.IsActive &&
                                u.Role != UserRole.Admin &&
                                u.Role != UserRole.SpiritualDirector)
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

                    // Nếu đã đủ 2 lớp -> Ẩn hoàn toàn khỏi danh sách
                    if (userClasses.Count >= 2)
                    {
                        continue;
                    }

                    // Nếu đang mở lớp KHỐI SÁNG: Ẩn những GLV đã dạy 1 lớp Khối Sáng (Khai Tâm, Rước Lễ, Thêm Sức)
                    if (isTargetMorning)
                    {
                        bool hasMorning = userClasses.Any(c => c.ClassRoom.GradeLevel != "Bao Đồng");
                        if (hasMorning)
                        {
                            continue; // Ẩn đi
                        }
                    }

                    // Nếu đang mở lớp KHỐI CHIỀU: Ẩn những GLV đã dạy 1 lớp Khối Chiều (Bao Đồng)
                    if (isTargetAfternoon)
                    {
                        bool hasAfternoon = userClasses.Any(c => c.ClassRoom.GradeLevel == "Bao Đồng");
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
                        statusNote = existingClass.ClassRoom.GradeLevel == "Bao Đồng"
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
                return Json(new { success = false, message = "Lỗi khi lấy danh sách GLV: " + ex.Message });
            }
        }

    }

    public class AssignTeacherRequestDto
    {
        public int ClassRoomId { get; set; }
        public long UserId { get; set; }
        public string RoleInClass { get; set; } = "HEAD";
    }
}
