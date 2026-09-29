using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    public class StudentsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IStudentCodeGenerator _codeGenerator;
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(ParishDbContext context, IStudentCodeGenerator codeGenerator, ILogger<StudentsController> logger)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /Students (Tìm kiếm & Phân trang)
        /// </summary>
        /// <param name="searchKeyword"></param>
        /// <param name="gender"></param>
        /// <param name="isActive"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(string? searchKeyword, string? gender, bool? isActive, int page = 1)
        {
            const int pageSize = 15;
            var query = _context.Students.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var term = searchKeyword.Trim().ToLower();
                query = query.Where(s =>
                    s.FirstName.ToLower().Contains(term) ||
                    s.LastName.ToLower().Contains(term) ||
                    s.ChristianName.ToLower().Contains(term) ||
                    s.StudentCode.ToLower().Contains(term) ||
                    (s.ParentPhone != null && s.ParentPhone.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(gender) && gender != "ALL")
            {
                query = query.Where(s => s.Gender == gender);
            }

            if (isActive.HasValue)
            {
                query = query.Where(s => s.IsActive == isActive.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var students = await query
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.Gender = gender ?? "ALL";
            ViewBag.IsActive = isActive;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(students);
        }

        /// <summary>
        /// 2. POST: /Students/Create
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(model.StudentCode))
                {
                    model.StudentCode = await _codeGenerator.GenerateUniqueCodeAsync();
                }
                else
                {
                    var exists = await _context.Students.AnyAsync(s => s.StudentCode.ToLower() == model.StudentCode.Trim().ToLower());
                    if (exists)
                    {
                        TempData["ErrorMessage"] = $"Mã thiếu nhi '{model.StudentCode}' đã tồn tại.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                // Sử dụng đường dẫn namespace đầy đủ để không bị nhầm lẫn class:
                var entity = new Student
                {
                    StudentCode = model.StudentCode.Trim().ToUpper(),
                    ChristianName = model.ChristianName.Trim(),
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    Gender = model.Gender,
                    DateOfBirth = DateTime.SpecifyKind(model.DateOfBirth, DateTimeKind.Utc),
                    BaptismDate = model.BaptismDate.HasValue
                        ? DateTime.SpecifyKind(model.BaptismDate.Value, DateTimeKind.Utc)
                        : null,
                    ConfirmationDate = model.ConfirmationDate.HasValue
                        ? DateTime.SpecifyKind(model.ConfirmationDate.Value, DateTimeKind.Utc)
                        : null,
                    ParentPhone = model.ParentPhone?.Trim(),
                    IsActive = true
                };

                _context.Students.Add(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Đã thêm thiếu nhi: {Code} - {Christian} {First} {Last}", entity.StudentCode, entity.ChristianName, entity.FirstName, entity.LastName);
                TempData["SuccessMessage"] = $"Thêm mới thiếu nhi {entity.ChristianName} {entity.FirstName} {entity.LastName} thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Thông tin không hợp lệ, vui lòng kiểm tra lại.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 3. GET: /Students/GetStudentAjax/{id}
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetStudentAjax(long id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            return Json(new
            {
                id = student.Id,
                studentCode = student.StudentCode,
                christianName = student.ChristianName,
                firstName = student.FirstName,
                lastName = student.LastName,
                gender = student.Gender,
                dateOfBirth = student.DateOfBirth.ToString("yyyy-MM-dd"),
                baptismDate = student.BaptismDate?.ToString("yyyy-MM-dd"),
                confirmationDate = student.ConfirmationDate?.ToString("yyyy-MM-dd"),
                parentPhone = student.ParentPhone,
                isActive = student.IsActive
            });
        }

        /// <summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StudentViewModel model)
        {
            if (ModelState.IsValid)
            {
                var target = await _context.Students.FindAsync(model.Id);
                if (target == null) return NotFound();

                target.ChristianName = model.ChristianName.Trim();
                target.FirstName = model.FirstName.Trim();
                target.LastName = model.LastName.Trim();
                target.Gender = model.Gender;
                target.DateOfBirth = DateTime.SpecifyKind(model.DateOfBirth, DateTimeKind.Utc);
                target.BaptismDate = model.BaptismDate.HasValue ? DateTime.SpecifyKind(model.BaptismDate.Value, DateTimeKind.Utc) : null;
                target.ConfirmationDate = model.ConfirmationDate.HasValue ? DateTime.SpecifyKind(model.ConfirmationDate.Value, DateTimeKind.Utc) : null;
                target.ParentPhone = model.ParentPhone?.Trim();
                target.IsActive = model.IsActive;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Cập nhật hồ sơ thiếu nhi {target.FirstName} {target.LastName} thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// POST: /Students/ToggleActive/{id}
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(long id)
        {
            var target = await _context.Students.FindAsync(id);
            if (target != null)
            {
                target.IsActive = !target.IsActive;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã {(target.IsActive ? "kích hoạt" : "tạm dừng")} hồ sơ {target.FirstName} {target.LastName}!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
