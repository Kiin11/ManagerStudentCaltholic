using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Options;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
        private readonly IPasswordHasherService _passwordHasher;
        private readonly IProfileHistoryService _profileHistoryService;
        private readonly IGoogleDriveStorageService _storageService;
        private readonly IImageOptimizationService _imageOptimizationService;
        private readonly GoogleDriveOptions _driveOptions;
        private readonly ICacheService _cacheService;

        public StudentsController(
            ParishDbContext context,
            IStudentCodeGenerator codeGenerator,
            ILogger<StudentsController> logger,
            IStudentExcelService excelService,
            IQrCodeService qrCodeService,
            IPasswordHasherService passwordHasher,
            IProfileHistoryService profileHistoryService,
            IGoogleDriveStorageService storageService,
            IImageOptimizationService imageOptimizationService,
            IOptions<GoogleDriveOptions> driveOptions,
            ICacheService cacheService)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _logger = logger;
            _excelService = excelService;
            _qrService = qrCodeService;
            _passwordHasher = passwordHasher;
            _profileHistoryService = profileHistoryService;
            _storageService = storageService;
            _imageOptimizationService = imageOptimizationService;
            _driveOptions = driveOptions.Value;
            _cacheService = cacheService;
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

            if (User.IsInRole(UserRole.ProbationaryTeacher) &&
                GradeLevel.IsAfternoonSession(targetClass.Session, targetClass.GradeLevel))
            {
                return false;
            }

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

            // Nếu là Teacher / ProbationaryTeacher: Chỉ được truy cập học sinh thuộc lớp mình dạy (Dự Bị chỉ Ca Sáng)
            var assignedQuery = _context.ClassTeachers
                .Where(ct => ct.UserId == CurrentUserId && (yearId == 0 || ct.AcademicYearId == yearId));

            if (User.IsInRole(UserRole.ProbationaryTeacher))
            {
                assignedQuery = assignedQuery.Where(ct =>
                    ct.ClassRoom.Session != GradeLevel.SessionAfternoon &&
                    ct.ClassRoom.GradeLevel != GradeLevel.BaoDong &&
                    ct.ClassRoom.GradeLevel != GradeLevel.DuBi &&
                    ct.ClassRoom.GradeLevel != GradeLevel.DuBiCode);
            }

            var assignedClassIds = await assignedQuery
                .Select(ct => ct.ClassRoomId)
                .ToListAsync();

            return await _context.Enrollments
                .AnyAsync(e => e.StudentId == studentId && assignedClassIds.Contains(e.ClassRoomId));
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchKeyword, string? gender, bool? isActive, int? classId, int page = 1)
        {
            const int pageSize = 15;
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var activeClasses = await _context.Classes
                .AsNoTracking()
                .Where(c => yearId == 0 || c.AcademicYearId == yearId)
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .ToListAsync();

            List<ClassRoom> authorizedClasses;
            if (IsLeadership)
            {
                authorizedClasses = activeClasses;
            }
            else if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                var assignedClassIds = await _context.ClassTeachers
                    .Where(ct => ct.UserId == CurrentUserId && (yearId == 0 || ct.AcademicYearId == yearId))
                    .Select(ct => ct.ClassRoomId)
                    .ToListAsync();

                authorizedClasses = activeClasses
                    .Where(c => (!string.IsNullOrWhiteSpace(managedGrade) &&
                                 string.Equals(c.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase)) ||
                                assignedClassIds.Contains(c.Id))
                    .ToList();
            }
            else if (User.IsInRole(UserRole.Teacher) || User.IsInRole(UserRole.ProbationaryTeacher))
            {
                var teacherClassQuery = _context.ClassTeachers
                    .Where(ct => ct.UserId == CurrentUserId && (yearId == 0 || ct.AcademicYearId == yearId));

                if (User.IsInRole(UserRole.ProbationaryTeacher))
                {
                    teacherClassQuery = teacherClassQuery.Where(ct =>
                        ct.ClassRoom.Session != GradeLevel.SessionAfternoon &&
                        ct.ClassRoom.GradeLevel != GradeLevel.BaoDong &&
                        ct.ClassRoom.GradeLevel != GradeLevel.DuBi &&
                        ct.ClassRoom.GradeLevel != GradeLevel.DuBiCode);
                }

                var assignedClassIds = await teacherClassQuery
                    .Select(ct => ct.ClassRoomId)
                    .ToListAsync();

                authorizedClasses = activeClasses
                    .Where(c => assignedClassIds.Contains(c.Id))
                    .ToList();
            }
            else
            {
                authorizedClasses = new List<ClassRoom>();
            }

            if (classId.HasValue && classId.Value > 0 && !authorizedClasses.Any(c => c.Id == classId.Value))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem danh sách thiếu nhi của lớp này.";
                return RedirectToAction(nameof(Index));
            }

            IQueryable<Student> query = _context.Students.AsNoTracking();

            // SEC-NEW-04 FIX: Phân tách phạm vi hiển thị (Fail-Closed cho BranchHead, Teacher, ProbationaryTeacher)
            if (!IsLeadership)
            {
                var authorizedClassIds = authorizedClasses.Select(c => c.Id).ToList();
                if (!authorizedClassIds.Any())
                {
                    query = query.Where(_ => false);
                }
                else
                {
                    var studentIdsInAuthorizedClasses = await _context.Enrollments
                        .Where(e => authorizedClassIds.Contains(e.ClassRoomId))
                        .Select(e => e.StudentId)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(s => studentIdsInAuthorizedClasses.Contains(s.Id));
                }
            }

            if (classId.HasValue && classId.Value > 0)
            {
                var studentIdsInSelectedClass = await _context.Enrollments
                    .Where(e => e.ClassRoomId == classId.Value)
                    .Select(e => e.StudentId)
                    .Distinct()
                    .ToListAsync();

                query = query.Where(s => studentIdsInSelectedClass.Contains(s.Id));
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

            // TASK-03.3 & TASK-03.4: Nạp lớp hiện tại và tài khoản Dự Bị/GLV liên kết cho các thiếu nhi hiển thị
            var pageStudentIds = students.Select(s => s.Id).ToList();
            var studentClassMap = new Dictionary<long, ClassRoom>();
            var studentUserMap = new Dictionary<long, User>();

            if (pageStudentIds.Any())
            {
                var currentEnrollments = await _context.Enrollments
                    .Include(e => e.ClassRoom)
                    .AsNoTracking()
                    .Where(e => pageStudentIds.Contains(e.StudentId) &&
                                (yearId == 0 || e.ClassRoom.AcademicYearId == yearId))
                    .OrderByDescending(e => e.Id)
                    .ToListAsync();

                foreach (var en in currentEnrollments)
                {
                    if (!studentClassMap.ContainsKey(en.StudentId) && en.ClassRoom != null)
                    {
                        studentClassMap[en.StudentId] = en.ClassRoom;
                    }
                }

                var linkedUsers = await _context.Users
                    .AsNoTracking()
                    .Where(u => (u.LinkedStudentId.HasValue && pageStudentIds.Contains(u.LinkedStudentId.Value)) ||
                                (u.StudentId.HasValue && pageStudentIds.Contains(u.StudentId.Value)))
                    .ToListAsync();

                foreach (var lu in linkedUsers)
                {
                    long? sid = lu.LinkedStudentId ?? lu.StudentId;
                    if (sid.HasValue && !studentUserMap.ContainsKey(sid.Value))
                    {
                        studentUserMap[sid.Value] = lu;
                    }
                }
            }

            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.Gender = gender ?? "ALL";
            ViewBag.IsActive = isActive;
            ViewBag.SelectedClassId = classId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.Classes = activeClasses;
            ViewBag.ActiveClasses = activeClasses;
            ViewBag.AuthorizedClasses = authorizedClasses;
            ViewBag.StudentCurrentClassMap = studentClassMap;
            ViewBag.StudentLinkedUserMap = studentUserMap;

            return View(students);
        }

        [HttpPost]
        [Authorize(Policy = "RequireExecutiveBoard")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Thông tin hồ sơ không hợp lệ, vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Index));
            }

            if (model.CatechistPromiseDate.HasValue &&
                !AgeValidationHelper.IsAtLeastAge(model.DateOfBirth, model.CatechistPromiseDate.Value, 18))
            {
                int age = AgeValidationHelper.CalculateAgeAtDate(model.DateOfBirth, model.CatechistPromiseDate.Value);
                TempData["ErrorMessage"] = $"Không thể lưu: Học viên mới {age} tuổi tại ngày Tuyên hứa GLV ({model.CatechistPromiseDate.Value:dd/MM/yyyy}), bắt buộc phải đủ 18 tuổi trở lên.";
                return RedirectToAction(nameof(Index));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                ClassRoom? selectedClass = null;
                if (model.InitialClassId.HasValue && model.InitialClassId.Value > 0)
                {
                    selectedClass = await _context.Classes
                        .FirstOrDefaultAsync(c => c.Id == model.InitialClassId.Value);

                    if (selectedClass != null && GradeLevel.IsProbationaryGrade(selectedClass.GradeLevel))
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = "Khối Dự Bị không cho phép tạo mới học sinh trực tiếp. Vui lòng tạo tài khoản vai trò 'Dự Bị' tại Quản Trị Người Dùng trước, sau đó thêm vào lớp Dự Bị 1..4!";
                        return RedirectToAction(nameof(Index));
                    }
                }

                // FEAT-P4-03: Sinh mã tự động theo công thức tên lớp (VD: Thêm Sức 3A -> TS3A001)
                // hoặc kiểm tra tính duy nhất nếu người dùng nhập sẵn StudentCode
                string finalStudentCode = await _codeGenerator.GenerateOrValidateStudentCodeAsync(
                    selectedClass?.Name,
                    model.StudentCode);

                var entity = new Student
                {
                    StudentCode = finalStudentCode,
                    ChristianName = (model.ChristianName ?? string.Empty).Trim(),
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    Id = model.Id,
                    Gender = model.Gender,
                    DateOfBirth = DateTime.SpecifyKind(model.DateOfBirth.Date, DateTimeKind.Utc),
                    BaptismDate = model.BaptismDate.HasValue ? DateTime.SpecifyKind(model.BaptismDate.Value.Date, DateTimeKind.Utc) : null,
                    FirstCommunionDate = model.FirstCommunionDate.HasValue ? DateTime.SpecifyKind(model.FirstCommunionDate.Value.Date, DateTimeKind.Utc) : null,
                    ConfirmationDate = model.ConfirmationDate.HasValue ? DateTime.SpecifyKind(model.ConfirmationDate.Value.Date, DateTimeKind.Utc) : null,
                    BaoDongPromiseDate = model.BaoDongPromiseDate.HasValue ? DateTime.SpecifyKind(model.BaoDongPromiseDate.Value.Date, DateTimeKind.Utc) : null,
                    CatechistPromiseDate = model.CatechistPromiseDate.HasValue ? DateTime.SpecifyKind(model.CatechistPromiseDate.Value.Date, DateTimeKind.Utc) : null,
                    ParentPhone = model.ParentPhone?.Trim(),
                    IsActive = true
                };

                _context.Students.Add(entity);
                await _context.SaveChangesAsync();

                // Nếu có chọn lớp khi tạo mới -> Tự động ghi danh vào lớp đó luôn
                if (selectedClass != null)
                {
                    _context.Enrollments.Add(new Enrollment
                    {
                        StudentId = entity.Id,
                        ClassRoomId = selectedClass.Id,
                        AcademicYearId = selectedClass.AcademicYearId
                    });
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã thêm thiếu nhi {entity.ChristianName} {entity.FirstName} {entity.LastName} với mã '{entity.StudentCode}'" +
                                             (selectedClass != null ? $" vào lớp {selectedClass.Name}!" : "!");
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi tạo mới hồ sơ thiếu nhi");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi lưu hồ sơ thiếu nhi. Vui lòng thử lại.";
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

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var latestEnrollment = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .AsNoTracking()
                .Where(e => e.StudentId == id && (yearId == 0 || e.ClassRoom.AcademicYearId == yearId))
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            var linkedUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.LinkedStudentId == id || u.StudentId == id);

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
                firstCommunionDate = student.FirstCommunionDate?.ToString("yyyy-MM-dd"),
                confirmationDate = student.ConfirmationDate?.ToString("yyyy-MM-dd"),
                baoDongPromiseDate = student.BaoDongPromiseDate?.ToString("yyyy-MM-dd"),
                catechistPromiseDate = student.CatechistPromiseDate?.ToString("yyyy-MM-dd"),
                parentPhone = student.ParentPhone,
                isActive = student.IsActive,
                currentClassName = latestEnrollment?.ClassRoom?.Name,
                currentGradeLevel = latestEnrollment?.ClassRoom?.GradeLevel,
                isInProbationaryGrade = GradeLevel.IsProbationaryGrade(latestEnrollment?.ClassRoom?.GradeLevel),
                linkedUser = linkedUser == null ? null : new
                {
                    userId = linkedUser.Id,
                    username = linkedUser.Username,
                    role = linkedUser.Role,
                    isActive = linkedUser.IsActive
                }
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
                if (model.CatechistPromiseDate.HasValue &&
                    !AgeValidationHelper.IsAtLeastAge(model.DateOfBirth, model.CatechistPromiseDate.Value, 18))
                {
                    int age = AgeValidationHelper.CalculateAgeAtDate(model.DateOfBirth, model.CatechistPromiseDate.Value);
                    TempData["ErrorMessage"] = $"Không thể lưu: Học viên mới {age} tuổi tại ngày Tuyên hứa GLV ({model.CatechistPromiseDate.Value:dd/MM/yyyy}), bắt buộc phải đủ 18 tuổi trở lên.";
                    return RedirectToAction(nameof(Index));
                }

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
                target.DateOfBirth = DateTime.SpecifyKind(model.DateOfBirth.Date, DateTimeKind.Utc);
                target.BaptismDate = model.BaptismDate.HasValue ? DateTime.SpecifyKind(model.BaptismDate.Value.Date, DateTimeKind.Utc) : null;
                target.FirstCommunionDate = model.FirstCommunionDate.HasValue ? DateTime.SpecifyKind(model.FirstCommunionDate.Value.Date, DateTimeKind.Utc) : null;
                target.ConfirmationDate = model.ConfirmationDate.HasValue ? DateTime.SpecifyKind(model.ConfirmationDate.Value.Date, DateTimeKind.Utc) : null;
                target.BaoDongPromiseDate = model.BaoDongPromiseDate.HasValue ? DateTime.SpecifyKind(model.BaoDongPromiseDate.Value.Date, DateTimeKind.Utc) : null;
                target.CatechistPromiseDate = model.CatechistPromiseDate.HasValue ? DateTime.SpecifyKind(model.CatechistPromiseDate.Value.Date, DateTimeKind.Utc) : null;
                target.ParentPhone = model.ParentPhone?.Trim();
                target.IsActive = model.IsActive;

                var linkedUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.LinkedStudentId == target.Id || u.StudentId == target.Id);
                if (linkedUser != null)
                {
                    linkedUser.DateOfBirth = target.DateOfBirth;
                    linkedUser.BaptismDate = target.BaptismDate;
                    linkedUser.FirstCommunionDate = target.FirstCommunionDate;
                    linkedUser.ConfirmationDate = target.ConfirmationDate;
                    linkedUser.BaoDongPromiseDate = target.BaoDongPromiseDate;
                    linkedUser.CatechistPromiseDate = target.CatechistPromiseDate;
                }

                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"staff_lookup:student_360:{target.Id}");
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
                await using var stream = excelFile.OpenReadStream();
                StudentImportResultDto result;
                if (stream.CanSeek)
                {
                    if (!await FileUploadSecurityHelper.ValidateStreamMagicBytesAsync(stream, ".xlsx", HttpContext.RequestAborted))
                    {
                        TempData["ErrorMessage"] = "Nội dung file Excel (.xlsx) không hợp lệ hoặc sai chữ ký định dạng.";
                        return RedirectToAction(nameof(Index));
                    }
                    result = await _excelService.ImportStudentsFromExcelAsync(stream);
                }
                else
                {
                    if (!await FileUploadSecurityHelper.ValidateFormFileMagicBytesAsync(excelFile, ".xlsx", HttpContext.RequestAborted))
                    {
                        TempData["ErrorMessage"] = "Nội dung file Excel (.xlsx) không hợp lệ hoặc sai chữ ký định dạng.";
                        return RedirectToAction(nameof(Index));
                    }
                    await using var readStream = excelFile.OpenReadStream();
                    result = await _excelService.ImportStudentsFromExcelAsync(readStream);
                }

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

            var badgeStudentIds = activeStudents.Select(s => s.Id).ToList();
            var avatarMap = activeStudents
                .Where(s => s.AvatarCloudFileId.HasValue)
                .ToDictionary(s => s.Id, s => s.AvatarCloudFileId!.Value);

            if (badgeStudentIds.Count > 0)
            {
                var linkedUserAvatars = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.AvatarCloudFileId.HasValue &&
                                ((u.LinkedStudentId.HasValue && badgeStudentIds.Contains(u.LinkedStudentId.Value)) ||
                                 (u.StudentId.HasValue && badgeStudentIds.Contains(u.StudentId.Value))))
                    .Select(u => new
                    {
                        StudentId = u.LinkedStudentId ?? u.StudentId!.Value,
                        AvatarId = u.AvatarCloudFileId!.Value
                    })
                    .ToListAsync();

                foreach (var item in linkedUserAvatars)
                {
                    avatarMap.TryAdd(item.StudentId, item.AvatarId);
                }
            }

            ViewBag.StudentAvatarMap = avatarMap;

            return View(viewModel);
        }

        /// <summary>
        /// Bổ sung 2 API Preview 
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>& Kiểm tra trùng mã trong Controllers/StudentsController.cs:
        [HttpGet]
        public async Task<IActionResult> PreviewNextCode(int? classId)
        {
            var suggestedCode = await _codeGenerator.GenerateByClassIdAsync(classId);
            return Json(new { success = true, suggestedCode });
        }

        [HttpGet]
        public async Task<IActionResult> CheckCodeUnique(string code, long? excludeId = null)
        {
            var (isUnique, normalizedCode, conflictMessage) = await _codeGenerator.ValidateCodeUniquenessAsync(code, excludeId);
            return Json(new
            {
                success = true,
                isUnique,
                normalizedCode,
                message = isUnique ? $"Mã '{normalizedCode}' hợp lệ và có thể sử dụng." : conflictMessage
            });
        }

        /// <summary>
        /// TASK-04.2 (Phase 6): Trang chi tiết hồ sơ Thiếu nhi kèm 5 Cột mốc Đức tin & Timeline song song Học/Dạy
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            if (id <= 0) return NotFound();

            if (!await CanAccessStudentAsync(id))
            {
                return Forbid();
            }

            var viewModel = await _profileHistoryService.BuildStudentProfileAsync(id);
            if (viewModel == null)
            {
                return NotFound("Không tìm thấy hồ sơ thiếu nhi.");
            }

            return View(viewModel);
        }

        /// <summary>
        /// Tải lên ảnh đại diện cho Thiếu nhi (tối ưu WebP 512x512, lưu Google Drive qua CloudMedia)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(long studentId, IFormFile avatarFile)
        {
            if (studentId <= 0)
            {
                return BadRequest(new { success = false, message = "Mã thiếu nhi không hợp lệ." });
            }

            if (!await CanAccessStudentAsync(studentId))
            {
                return Forbid();
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ thiếu nhi." });
            }

            if (avatarFile == null || avatarFile.Length <= 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn tệp ảnh đại diện hợp lệ." });
            }

            long maxAvatarBytes = _driveOptions.MaxAvatarBytes > 0 ? _driveOptions.MaxAvatarBytes : 5 * 1024 * 1024;
            if (avatarFile.Length > maxAvatarBytes)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Dung lượng ảnh đại diện vượt quá giới hạn tối đa ({maxAvatarBytes / (1024 * 1024)} MB)."
                });
            }

            var ext = Path.GetExtension(avatarFile.FileName);
            if (!_imageOptimizationService.IsSupportedImageExtension(ext))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Định dạng ảnh không hợp lệ. Chỉ chấp nhận JPG, JPEG, PNG, WebP hoặc GIF."
                });
            }

            CloudMediaFile media;
            try
            {
                await using var inputStream = avatarFile.OpenReadStream();
                var opt = await _imageOptimizationService.OptimizeToWebpAsync(
                    inputStream,
                    maxWidth: 512,
                    maxHeight: 512,
                    quality: 82,
                    ct: HttpContext.RequestAborted);

                await using var optimizedStream = opt.OptimizedStream;
                var uploadFileName = string.IsNullOrWhiteSpace(avatarFile.FileName)
                    ? $"avatar_{student.StudentCode}{opt.FileExtension}"
                    : Path.ChangeExtension(Path.GetFileName(avatarFile.FileName), opt.FileExtension);

                media = await _storageService.UploadAsync(new CloudUploadRequest
                {
                    Content = optimizedStream,
                    OriginalFileName = uploadFileName,
                    ContentType = opt.ContentType,
                    Category = CloudMediaCategory.Avatar,
                    EntityName = nameof(Student),
                    EntityId = student.Id,
                    FolderSegments = new[] { "Students_Avatars" },
                    UploadedBy = User.Identity?.Name,
                    UploadedByUserId = CurrentUserId > 0 ? CurrentUserId : null,
                    AllowLocalFallback = true
                }, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải lên ảnh đại diện thiếu nhi StudentId={StudentId}", student.Id);
                return StatusCode(500, new { success = false, message = "Không thể lưu ảnh đại diện. Vui lòng thử lại." });
            }

            long? oldAvatarId = student.AvatarCloudFileId;
            student.AvatarCloudFileId = media.Id;
            await _context.SaveChangesAsync();

            if (oldAvatarId.HasValue && oldAvatarId.Value != media.Id)
            {
                bool stillReferencedByUser = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.AvatarCloudFileId == oldAvatarId.Value);
                if (!stillReferencedByUser)
                {
                    await _storageService.DeleteAsync(oldAvatarId.Value);
                }
            }

            await _cacheService.RemoveAsync($"staff_lookup:student_360:{studentId}");

            _logger.LogInformation(
                "Đã cập nhật ảnh đại diện thiếu nhi StudentId={StudentId} ({StudentCode}), CloudFileId={CloudFileId} bởi {Operator}",
                student.Id, student.StudentCode, media.Id, User.Identity?.Name);

            return Json(new
            {
                success = true,
                message = "Đã cập nhật ảnh đại diện thiếu nhi thành công!",
                data = new
                {
                    cloudFileId = media.Id,
                    proxyUrl = $"/CloudMedia/File/{media.Id}"
                }
            });
        }

        /// <summary>
        /// TASK-03.3 (Phase 6): Cấp tài khoản GLV Dự Bị (ProbationaryTeacher) trực tiếp từ hồ sơ Thiếu nhi đã xếp Khối Dự Bị
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProvisionProbationaryAccount([FromBody] ProvisionProbationaryUserRequestDto request)
        {
            if (request == null || request.StudentId <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu yêu cầu cấp tài khoản Dự Bị không hợp lệ." });
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
            if (student == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hồ sơ thiếu nhi." });
            }

            // Kiểm tra tránh tạo trùng tài khoản cho cùng 1 học viên (1-1 qua LinkedStudentId hoặc StudentId)
            var existingUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.LinkedStudentId == student.Id ||
                                          (u.StudentId == student.Id && (u.Role == UserRole.ProbationaryTeacher || u.Role == UserRole.Teacher)));
            if (existingUser != null)
            {
                return Json(new
                {
                    success = false,
                    message = $"Thiếu nhi '{student.ChristianName} {student.FirstName} {student.LastName}' đã có tài khoản liên kết '{existingUser.Username}' (Vai trò: {existingUser.Role}). Không thể cấp trùng!"
                });
            }

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            bool isEnrolledInProbationary = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .AnyAsync(e => e.StudentId == student.Id &&
                               (yearId == 0 || e.ClassRoom.AcademicYearId == yearId) &&
                               (e.ClassRoom.GradeLevel == GradeLevel.DuBi || e.ClassRoom.GradeLevel == GradeLevel.DuBiCode));

            if (!isEnrolledInProbationary)
            {
                return Json(new
                {
                    success = false,
                    message = "Chỉ được cấp tài khoản GLV Dự Bị cho học viên đã được xếp vào lớp thuộc Khối Dự Bị (Dự Bị 1..4)."
                });
            }

            string baseUsername;
            if (!string.IsNullOrWhiteSpace(request.CustomUsername))
            {
                baseUsername = request.CustomUsername.Trim().ToLowerInvariant();
                if (baseUsername.Length < 3 || baseUsername.Length > 50)
                {
                    return Json(new { success = false, message = "Tên đăng nhập phải từ 3 đến 50 ký tự." });
                }

                bool customExists = await _context.Users.AnyAsync(u => u.Username.ToLower() == baseUsername);
                if (customExists)
                {
                    return Json(new { success = false, message = $"Tên đăng nhập '{baseUsername}' đã tồn tại trong hệ thống." });
                }
            }
            else
            {
                var cleanCode = Regex.Replace(student.StudentCode.Trim().ToLowerInvariant(), @"[^a-z0-9_]", "");
                if (string.IsNullOrEmpty(cleanCode))
                {
                    cleanCode = student.Id.ToString();
                }

                baseUsername = $"db_{cleanCode}";
                if (baseUsername.Length > 45) baseUsername = baseUsername[..45];

                var candidate = baseUsername;
                int suffix = 1;
                while (await _context.Users.AnyAsync(u => u.Username.ToLower() == candidate))
                {
                    candidate = $"{baseUsername}_{suffix++}";
                }
                baseUsername = candidate;
            }

            string initialPassword = string.IsNullOrWhiteSpace(request.InitialPassword)
                ? GenerateSecureInitialPassword()
                : request.InitialPassword.Trim();

            if (initialPassword.Length < 6)
            {
                return Json(new { success = false, message = "Mật khẩu khởi tạo phải có tối thiểu 6 ký tự." });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            User? createdUser = null;

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                createdUser = new User
                {
                    Username = baseUsername,
                    PasswordHash = _passwordHasher.HashPassword(initialPassword),
                    ChristianName = student.ChristianName,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    DateOfBirth = student.DateOfBirth,
                    FeastDay = student.FeastDay,
                    BaptismDate = student.BaptismDate,
                    FirstCommunionDate = student.FirstCommunionDate,
                    ConfirmationDate = student.ConfirmationDate,
                    BaoDongPromiseDate = student.BaoDongPromiseDate,
                    CatechistPromiseDate = student.CatechistPromiseDate,
                    PhoneNumber = student.ParentPhone,
                    Role = UserRole.ProbationaryTeacher,
                    LinkedStudentId = student.Id,
                    StudentId = student.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(createdUser);
                await _context.SaveChangesAsync();

                var oldStudentCode = student.StudentCode;
                if (!string.Equals(oldStudentCode, createdUser.Username, StringComparison.Ordinal))
                {
                    int? historyYearId = currentYear?.Id ?? (await _context.AcademicYears
                        .AsNoTracking()
                        .OrderByDescending(y => y.StartDate)
                        .Select(y => (int?)y.Id)
                        .FirstOrDefaultAsync());

                    if (!string.IsNullOrWhiteSpace(oldStudentCode) && historyYearId.HasValue)
                    {
                        _context.StudentCodeHistories.Add(new StudentCodeHistory
                        {
                            StudentId = student.Id,
                            SourceAcademicYearId = historyYearId.Value,
                            TargetAcademicYearId = historyYearId.Value,
                            OriginalCode = oldStudentCode.Length > 50 ? oldStudentCode[..50] : oldStudentCode,
                            ArchivedYearCode = oldStudentCode.Length > 50 ? oldStudentCode[..50] : oldStudentCode,
                            NewCode = createdUser.Username,
                            RolloverOutcome = "PROBATIONARY_ACCOUNT",
                            TargetClassName = "Khối Dự Bị",
                            ChangedBy = User.Identity?.Name ?? "System",
                            ChangedAt = DateTime.UtcNow
                        });
                    }

                    student.StudentCode = createdUser.Username;
                }

                _context.UserRoleHistories.Add(new UserRoleHistory
                {
                    UserId = createdUser.Id,
                    OldRole = "Student",
                    NewRole = UserRole.ProbationaryTeacher,
                    AcademicYearId = currentYear?.Id,
                    ChangedByUserId = CurrentUserId > 0 ? CurrentUserId : null,
                    ChangedAt = DateTime.UtcNow,
                    Reason = $"Cấp tài khoản GLV Dự Bị từ hồ sơ thiếu nhi {oldStudentCode}"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            });

            _logger.LogInformation("Đã cấp tài khoản Dự Bị '{Username}' (ID: {UserId}) cho Student ID {StudentId} bởi User {Operator}",
                createdUser!.Username, createdUser.Id, student.Id, User.Identity?.Name);

            return Json(new
            {
                success = true,
                message = $"Đã cấp tài khoản GLV Dự Bị '{createdUser.Username}' thành công!",
                data = new
                {
                    userId = createdUser.Id,
                    username = createdUser.Username,
                    initialPassword,
                    role = createdUser.Role
                }
            });
        }

        /// <summary>
        /// TASK-03.4 (Phase 6): Cập nhật Ngày Hoàn thành Dự Bị & Tuyên hứa GLV (bắt buộc >= 18 tuổi) và nâng cấp Role lên Teacher
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCatechistPromiseAndUpgrade([FromBody] CatechistPromiseUpgradeRequestDto request)
        {
            if (request == null || request.StudentId <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu cập nhật Tuyên hứa GLV không hợp lệ." });
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
            if (student == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hồ sơ thiếu nhi." });
            }

            var promiseUtc = DateTime.SpecifyKind(request.CatechistPromiseDate.Date, DateTimeKind.Utc);
            int ageAtPromise = AgeValidationHelper.CalculateAgeAtDate(student.DateOfBirth, promiseUtc);

            // Ràng buộc cứng tối thiểu 18 tuổi tại ngày Tuyên hứa GLV
            if (!AgeValidationHelper.IsAtLeastAge(student.DateOfBirth, promiseUtc, 18))
            {
                return Json(new
                {
                    success = false,
                    isUnderAge = true,
                    ageAtPromise,
                    message = $"CHẶN LƯU: Học viên mới {ageAtPromise} tuổi tại ngày {promiseUtc:dd/MM/yyyy} (Ngày sinh: {student.DateOfBirth:dd/MM/yyyy}). Quy định bắt buộc phải đủ 18 tuổi trở lên mới được ghi nhận Hoàn thành Dự Bị & Tuyên hứa GLV!"
                });
            }

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            bool roleUpgraded = false;
            string? finalRole = null;
            string? linkedUsername = null;
            long? linkedUserId = null;

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                student.CatechistPromiseDate = promiseUtc;
                if (request.BaoDongPromiseDate.HasValue)
                {
                    student.BaoDongPromiseDate = DateTime.SpecifyKind(request.BaoDongPromiseDate.Value.Date, DateTimeKind.Utc);
                }

                var linkedUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.LinkedStudentId == student.Id || u.StudentId == student.Id);

                if (linkedUser != null)
                {
                    linkedUserId = linkedUser.Id;
                    linkedUsername = linkedUser.Username;
                    finalRole = linkedUser.Role;

                    linkedUser.CatechistPromiseDate = student.CatechistPromiseDate;
                    linkedUser.BaoDongPromiseDate = student.BaoDongPromiseDate;
                    linkedUser.DateOfBirth = DateTime.SpecifyKind(student.DateOfBirth.Date, DateTimeKind.Utc);

                    if (request.UpgradeRoleToTeacher && linkedUser.Role == UserRole.ProbationaryTeacher)
                    {
                        linkedUser.Role = UserRole.Teacher;
                        finalRole = UserRole.Teacher;
                        roleUpgraded = true;

                        _context.UserRoleHistories.Add(new UserRoleHistory
                        {
                            UserId = linkedUser.Id,
                            OldRole = UserRole.ProbationaryTeacher,
                            NewRole = UserRole.Teacher,
                            AcademicYearId = currentYear?.Id,
                            ChangedByUserId = CurrentUserId > 0 ? CurrentUserId : null,
                            ChangedAt = DateTime.UtcNow,
                            Reason = $"Hoàn thành Dự Bị & Tuyên hứa GLV ngày {promiseUtc:dd/MM/yyyy} (đủ {ageAtPromise} tuổi)"
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            });

            if (linkedUserId.HasValue)
            {
                await _cacheService.RemoveAsync(CacheKeys.UserPrincipalState(linkedUserId.Value));
            }
            await _cacheService.RemoveAsync($"staff_lookup:student_360:{student.Id}");

            string msg = roleUpgraded
                ? $"Đã lưu Ngày Tuyên hứa GLV ({promiseUtc:dd/MM/yyyy}, {ageAtPromise} tuổi) và nâng cấp tài khoản '{linkedUsername}' lên Giáo lý viên chính thức (Teacher)!"
                : $"Đã lưu Ngày Tuyên hứa GLV ({promiseUtc:dd/MM/yyyy}, {ageAtPromise} tuổi) thành công!";

            return Json(new
            {
                success = true,
                ageAtPromise,
                roleUpgraded,
                linkedUsername,
                newRole = finalRole,
                message = msg
            });
        }

        private static string GenerateSecureInitialPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string special = "@#$!%*?&";
            const string allChars = upper + lower + digits + special;

            Span<char> password = stackalloc char[10];
            password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
            password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
            password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
            password[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
            }

            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }
    }
}