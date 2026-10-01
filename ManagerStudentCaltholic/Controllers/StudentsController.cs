using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class StudentsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IStudentCodeGenerator _codeGenerator;
        private readonly ILogger<StudentsController> _logger;
        private readonly IStudentExcelService _excelService;
        private readonly IQrCodeService _qrService;

        public StudentsController(ParishDbContext context, IStudentCodeGenerator codeGenerator, 
            ILogger<StudentsController> logger, IStudentExcelService excelService, IQrCodeService qrCodeService)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _logger = logger;
            _excelService = excelService;
            _qrService = qrCodeService;
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

            // Nếu là Trưởng khối, chỉ cho phép tìm kiếm/xem hồ sơ của học sinh đang học trong khối của mình
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrEmpty(managedGrade))
                {
                    var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
                    var yearId = currentYear?.Id ?? 0;

                    // Tìm danh sách ID học sinh đang học ở các lớp thuộc khối này
                    var studentIdsInGrade = await _context.Enrollments
                        .Include(e => e.ClassRoom)
                        .Where(e => e.ClassRoom.AcademicYearId == yearId && e.ClassRoom.GradeLevel == managedGrade)
                        .Select(e => e.StudentId)
                        .Distinct()
                        .ToListAsync();

                    var scopedQuery = _context.Students.AsNoTracking().Where(s => studentIdsInGrade.Contains(s.Id));

                    if (!string.IsNullOrWhiteSpace(searchKeyword))
                    {
                        var term = searchKeyword.Trim().ToLower();
                        scopedQuery = scopedQuery.Where(s =>
                            s.FirstName.ToLower().Contains(term) ||
                            s.LastName.ToLower().Contains(term) ||
                            s.ChristianName.ToLower().Contains(term) ||
                            s.StudentCode.ToLower().Contains(term) ||
                            (s.ParentPhone != null && s.ParentPhone.Contains(term)));
                    }

                    var totalScopedCount = await scopedQuery.CountAsync();
                    var totalScopedPages = (int)Math.Ceiling(totalScopedCount / (double)pageSize);
                    page = Math.Max(1, Math.Min(page, Math.Max(1, totalScopedPages)));

                    var scopedStudents = await scopedQuery
                        .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                        .Skip((page - 1) * pageSize).Take(pageSize)
                        .ToListAsync();

                    ViewBag.SearchKeyword = searchKeyword;
                    ViewBag.Gender = gender ?? "ALL";
                    ViewBag.IsActive = isActive;
                    ViewBag.CurrentPage = page;
                    ViewBag.TotalPages = totalScopedPages;
                    ViewBag.TotalCount = totalScopedCount;

                    return View(scopedStudents);
                }
            }

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
        [Authorize(Policy = "RequireExecutiveBoard")]
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

        /// <summary>
        /// Tải file mẫu Excel
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var content = _excelService.GenerateTemplateFile();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_Danh_Sach_Thieu_Nhi.xlsx");
        }

        /// <summary>
        /// Upload và Import Excel hàng loạt
        /// </summary>
        /// <param name="excelFile"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn 1 file Excel (.xlsx) hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = excelFile.OpenReadStream();
                var result = await _excelService.ImportStudentsFromExcelAsync(stream);

                if (result.SuccessCount > 0)
                {
                    TempData["SuccessMessage"] = $"Nạp thành công {result.SuccessCount} hồ sơ thiếu nhi vào hệ thống!";
                }

                if (result.FailureCount > 0)
                {
                    TempData["ErrorMessage"] = $"Có {result.FailureCount} dòng bị lỗi: " + string.Join("; ", result.ErrorMessages.Take(3));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nạp file Excel");
                TempData["ErrorMessage"] = "Lỗi khi xử lý file Excel: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// GET: /Students/DownloadQr/{studentCode} (Tải ảnh PNG lẻ)
        /// </summary>
        /// <param name="studentCode"></param>
        /// <returns></returns>
        [HttpGet]
        public IActionResult DownloadQr(string studentCode)
        {
            if (string.IsNullOrWhiteSpace(studentCode)) return NotFound();

            var cleanCode = studentCode.Trim().ToUpper();
            var pngBytes = _qrService.GenerateQrCodePng(cleanCode, pixelsPerModule: 15);

            if (pngBytes.Length == 0) return BadRequest("Không thể sinh mã QR");

            return File(pngBytes, "image/png", $"QR_{cleanCode}.png");
        }

        /// <summary>
        /// GET: /Students/PrintBadges?classId=... (Trang in thẻ học viên có QR)
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> PrintBadges(int classId)
        {
            var targetClass = await _context.Classes
                .Include(c => c.AcademicYear)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (targetClass == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lớp học yêu cầu.";
                return RedirectToAction("Index", "Classes");
            }

            var activeStudents = targetClass.Enrollments
                .Select(e => e.Student)
                .Where(s => s.IsActive)
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .ToList();

            var badgeItems = activeStudents.Select(s => new StudentBadgeCardItem
            {
                StudentId = s.Id,
                StudentCode = s.StudentCode,
                ChristianName = s.ChristianName,
                FullName = $"{s.FirstName} {s.LastName}".Trim(),
                Gender = s.Gender,
                DateOfBirth = s.DateOfBirth,
                ClassName = targetClass.Name,
                QrBase64 = _qrService.GenerateQrCodeBase64(s.StudentCode, pixelsPerModule: 6)
            }).ToList();

            var viewModel = new PrintStudentBadgesViewModel
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AcademicYearName = targetClass.AcademicYear.Name,
                Badges = badgeItems
            };

            return View(viewModel);
        }
    }
}
