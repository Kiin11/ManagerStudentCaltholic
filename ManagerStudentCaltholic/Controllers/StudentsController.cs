using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
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

        public StudentsController(
            ParishDbContext context,
            IStudentCodeGenerator codeGenerator,
            ILogger<StudentsController> logger,
            IStudentExcelService excelService,
            IQrCodeService qrCodeService)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _logger = logger;
            _excelService = excelService;
            _qrService = qrCodeService;
        }

        private long CurrentUserId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        /// <summary>
        /// SEC-NEW-04 FIX: Kiểm tra quyền truy cập của GLV / Trưởng khối đối với một Lớp học
        /// </summary>
        private async Task<bool> CanAccessClassAsync(ClassRoom targetClass)
        {
            if (IsLeadership) return true;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrWhiteSpace(managedGrade) &&
                    string.Equals(targetClass.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return await _context.ClassTeachers
                .AnyAsync(ct => ct.ClassRoomId == targetClass.Id && ct.UserId == CurrentUserId);
        }

        /// <summary>
        /// SEC-NEW-04 FIX: Kiểm tra quyền truy cập của GLV / Trưởng khối đối với một Thiếu nhi cụ thể
        /// </summary>
        private async Task<bool> CanAccessStudentAsync(long studentId)
        {
            if (IsLeadership) return true;

            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade)) return false; // Fail-closed

                return await _context.Enrollments
                    .Include(e => e.ClassRoom)
                    .AnyAsync(e => e.StudentId == studentId &&
                                   (yearId == 0 || e.ClassRoom.AcademicYearId == yearId) &&
                                   e.ClassRoom.GradeLevel == managedGrade);
            }

            // Nếu là Teacher: Chỉ được truy cập học sinh thuộc lớp mình dạy
            var assignedClassIds = await _context.ClassTeachers
                .Where(ct => ct.UserId == CurrentUserId && (yearId == 0 || ct.AcademicYearId == yearId))
                .Select(ct => ct.ClassRoomId)
                .ToListAsync();

            return await _context.Enrollments
                .AnyAsync(e => e.StudentId == studentId && assignedClassIds.Contains(e.ClassRoomId));
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchKeyword, string? gender, bool? isActive, int page = 1)
        {
            const int pageSize = 15;
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            IQueryable<Student> query = _context.Students.AsNoTracking();

            // SEC-NEW-04 FIX: Phân tách phạm vi hiển thị (Fail-Closed cho BranchHead và Teacher)
            if (!IsLeadership)
            {
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    if (string.IsNullOrWhiteSpace(managedGrade))
                    {
                        query = query.Where(_ => false);
                    }
                    else
                    {
                        var studentIdsInGrade = await _context.Enrollments
                            .Include(e => e.ClassRoom)
                            .Where(e => e.ClassRoom.AcademicYearId == yearId && e.ClassRoom.GradeLevel == managedGrade)
                            .Select(e => e.StudentId)
                            .Distinct()
                            .ToListAsync();

                        query = query.Where(s => studentIdsInGrade.Contains(s.Id));
                    }
                }
                else if (User.IsInRole(UserRole.Teacher))
                {
                    var assignedClassIds = await _context.ClassTeachers
                        .Where(ct => ct.UserId == CurrentUserId && ct.AcademicYearId == yearId)
                        .Select(ct => ct.ClassRoomId)
                        .ToListAsync();

                    var studentIdsInClasses = await _context.Enrollments
                        .Where(e => assignedClassIds.Contains(e.ClassRoomId))
                        .Select(e => e.StudentId)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(s => studentIdsInClasses.Contains(s.Id));
                }
            }

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
                .OrderBy(s => s.StudentCode)
                .ThenBy(s => s.LastName)
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

                var entity = new Student
                {
                    StudentCode = model.StudentCode.Trim().ToUpper(),
                    ChristianName = model.ChristianName.Trim(),
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    Gender = model.Gender,
                    DateOfBirth = DateTime.SpecifyKind(model.DateOfBirth, DateTimeKind.Utc),
                    BaptismDate = model.BaptismDate.HasValue ? DateTime.SpecifyKind(model.BaptismDate.Value, DateTimeKind.Utc) : null,
                    ConfirmationDate = model.ConfirmationDate.HasValue ? DateTime.SpecifyKind(model.ConfirmationDate.Value, DateTimeKind.Utc) : null,
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
        /// 3. GET: /Students/GetStudentAjax/{id} (SEC-NEW-04 FIX: Chặn IDOR xem trộm SĐT phụ huynh lớp khác)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStudentAjax(long id)
        {
            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (student == null) return NotFound();

            if (!await CanAccessStudentAsync(id))
            {
                return Forbid();
            }

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
        /// 4. POST: /Students/Edit (SEC-NEW-04 FIX: Chỉ Admin, BĐH hoặc Trưởng khối đúng phạm vi được sửa)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StudentViewModel model)
        {
            if (ModelState.IsValid)
            {
                var target = await _context.Students.FindAsync(model.Id);
                if (target == null) return NotFound();

                if (!await CanAccessStudentAsync(target.Id))
                {
                    return Forbid();
                }

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
        /// 5. POST: /Students/ToggleActive/{id} (SEC-NEW-04 FIX: Chỉ Admin và BĐH được khóa/mở khóa hồ sơ gốc)
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "RequireExecutiveBoard")]
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

        [HttpGet]
        [Authorize(Policy = "RequireExecutiveBoard")]
        public IActionResult DownloadTemplate()
        {
            var content = _excelService.GenerateTemplateFile();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_Danh_Sach_Thieu_Nhi.xlsx");
        }

        /// <summary>
        /// 6. POST: /Students/ImportExcel (SEC-NEW-04 FIX: Giới hạn quyền RequireExecutiveBoard + Kiểm tra định dạng & dung lượng)
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "RequireExecutiveBoard")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn 1 file Excel (.xlsx) hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            const long maxFileSize = 5 * 1024 * 1024; // 5MB
            if (excelFile.Length > maxFileSize || Path.GetExtension(excelFile.FileName).ToLowerInvariant() != ".xlsx")
            {
                TempData["ErrorMessage"] = "Chỉ chấp nhận file định dạng .xlsx và dung lượng tối đa 5MB.";
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
                TempData["ErrorMessage"] = "Không thể xử lý file Excel. Vui lòng kiểm tra lại đúng biểu mẫu.";
            }

            return RedirectToAction(nameof(Index));
        }

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
        /// 7. GET: /Students/PrintBadges (SEC-NEW-04 FIX: Kiểm tra quyền phụ trách lớp trước khi in thẻ)
        /// </summary>
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

            if (!await CanAccessClassAsync(targetClass))
            {
                return Forbid();
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