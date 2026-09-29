using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Model.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    public class ClassesController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<ClassesController> _logger;

        public ClassesController(ParishDbContext context, ILogger<ClassesController> logger)
        {
            _context = context;
            _logger = logger;
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

            // Lọc theo Khối / Ngành nếu có chọn
            if (!string.IsNullOrWhiteSpace(gradeLevel) && gradeLevel != "ALL")
            {
                query = query.Where(c => c.GradeLevel == gradeLevel);
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,GradeLevel,RoomName,AcademicYearId")] ClassRoom classRoom)
        {
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
        public async Task<IActionResult> AssignTeacher([FromBody] ClassTeacher model)
        {
            if (string.IsNullOrWhiteSpace(model.TeacherName))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên Giáo lý viên." });
            }

            var targetClass = await _context.Classes.FindAsync(model.ClassRoomId);
            if (targetClass == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học." });
            }

            var isExist = await _context.ClassTeachers.AnyAsync(ct =>
                ct.ClassRoomId == model.ClassRoomId &&
                ct.AcademicYearId == targetClass.AcademicYearId &&
                ct.TeacherName.Trim().ToLower() == model.TeacherName.Trim().ToLower());

            if (isExist)
            {
                return Json(new { success = false, message = $"Giáo lý viên '{model.TeacherName}' đã được phân công vào lớp này rồi." });
            }

            model.AcademicYearId = targetClass.AcademicYearId;
            model.AssignedAt = DateTime.UtcNow;

            _context.ClassTeachers.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã phân công GLV '{model.TeacherName}' thành công!" });
        }

        /// <summary>
        /// Action Hủy phân công GLV
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
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
    }
}
