using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireExecutiveBoard")]
    public class StudentRegistrationsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IStudentCodeGenerator _codeGenerator;
        private readonly IStudentPlacementService _placementService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<StudentRegistrationsController> _logger;

        public StudentRegistrationsController(
            ParishDbContext context,
            IStudentCodeGenerator codeGenerator,
            IStudentPlacementService placementService,
            IWebHostEnvironment env,
            ILogger<StudentRegistrationsController> logger)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _placementService = placementService;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /StudentRegistrations/Index (Dashboard Quản lý & Lọc Đơn Đăng Ký - TASK-P3-02.3)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            string status = "PENDING",
            string? grade = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var currentYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.IsCurrent)
                ?? await _context.AcademicYears.OrderByDescending(y => y.Id).AsNoTracking().FirstOrDefaultAsync();

            int currentYearId = currentYear?.Id ?? 0;

            var baseQuery = _context.Set<StudentRegistrationRequest>()
                .Include(r => r.CreatedStudent)
                .Include(r => r.AssignedClass)
                .AsNoTracking()
                .AsQueryable();

            // Đếm thống kê KPI
            var totalPending = await _context.Set<StudentRegistrationRequest>().CountAsync(r => r.Status == "PENDING");
            var totalApproved = await _context.Set<StudentRegistrationRequest>().CountAsync(r => r.Status == "APPROVED");
            var totalRejected = await _context.Set<StudentRegistrationRequest>().CountAsync(r => r.Status == "REJECTED");

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                baseQuery = baseQuery.Where(r => r.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(grade) && grade != "ALL")
            {
                baseQuery = baseQuery.Where(r => r.TargetGradeLevel == grade);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string kw = search.Trim().ToLower();
                baseQuery = baseQuery.Where(r =>
                    r.FirstName.ToLower().Contains(kw) ||
                    r.LastName.ToLower().Contains(kw) ||
                    r.ChristianName.ToLower().Contains(kw) ||
                    r.ParentPhone.Contains(kw) ||
                    r.ParentFullName.ToLower().Contains(kw));
            }

            if (fromDate.HasValue)
            {
                var startUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
                baseQuery = baseQuery.Where(r => r.CreatedAt >= startUtc);
            }

            if (toDate.HasValue)
            {
                var endUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc);
                baseQuery = baseQuery.Where(r => r.CreatedAt < endUtc);
            }

            var requests = await baseQuery
                .OrderByDescending(r => r.CreatedAt)
                .Take(300)
                .ToListAsync();

            var availableClasses = await _context.Classes
                .Where(c => c.AcademicYearId == currentYearId)
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            var vm = new AdminRegistrationDashboardViewModel
            {
                StatusFilter = status,
                GradeFilter = grade,
                SearchTerm = search,
                FromDate = fromDate,
                ToDate = toDate,
                CurrentAcademicYearId = currentYearId,
                CurrentAcademicYearName = currentYear?.Name ?? "Chưa cấu hình",
                TotalPending = totalPending,
                TotalApproved = totalApproved,
                TotalRejected = totalRejected,
                Requests = requests,
                AvailableClasses = availableClasses
            };

            return View(vm);
        }

        /// <summary>
        /// 2. GET: /StudentRegistrations/ViewCertificate/{id} (Xem ảnh/PDF Giấy Rửa Tội bảo mật từ PrivateUploads)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ViewCertificate(long id)
        {
            var req = await _context.Set<StudentRegistrationRequest>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null || string.IsNullOrWhiteSpace(req.BaptismCertificatePath))
            {
                return NotFound("Hồ sơ này không đính kèm ảnh Giấy Rửa Tội.");
            }

            // Chống Path Traversal tuyệt đối bằng Path.GetFileName
            string safeFileName = Path.GetFileName(req.BaptismCertificatePath);
            string fullPath = Path.Combine(_env.ContentRootPath, "PrivateUploads", "baptism_certs", safeFileName);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("Không tìm thấy tệp chứng từ trên máy chủ.");
            }

            string ext = Path.GetExtension(safeFileName).ToLowerInvariant();
            string contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                _ => "image/jpeg"
            };

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(fileBytes, contentType);
        }

        /// <summary>
        /// 3. POST: /StudentRegistrations/Approve (Duyệt đơn, Tự sinh StudentCode & Tự động Xếp lớp - TASK-P3-02.4)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve([FromBody] ApproveRegistrationDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Yêu cầu duyệt không hợp lệ." });
            }

            var req = await _context.Set<StudentRegistrationRequest>()
                .FirstOrDefaultAsync(r => r.Id == dto.RequestId);

            if (req == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ đăng ký." });
            }

            if (req.Status == "APPROVED")
            {
                return BadRequest(new { success = false, message = "Hồ sơ này đã được duyệt trước đó." });
            }

            var currentYear = await _context.AcademicYears
                .FirstOrDefaultAsync(y => y.IsCurrent)
                ?? await _context.AcademicYears.OrderByDescending(y => y.Id).FirstOrDefaultAsync();

            if (currentYear == null)
            {
                return BadRequest(new { success = false, message = "Chưa có niên khóa hiện hành để xếp lớp." });
            }

            int yearId = req.AcademicYearId ?? currentYear.Id;

            // Xác định lớp xếp vào: Nếu BĐH chọn lớp cụ thể thì kiểm tra hợp lệ; nếu để trống thì gọi Smart Balancing
            int? targetClassId = dto.TargetClassId;
            if (targetClassId.HasValue && targetClassId.Value > 0)
            {
                bool validClass = await _context.Classes
                    .AnyAsync(c => c.Id == targetClassId.Value && c.AcademicYearId == yearId);
                if (!validClass)
                {
                    return BadRequest(new { success = false, message = "Lớp được chọn không thuộc niên khóa hiện tại." });
                }
            }
            else
            {
                targetClassId = await _placementService.DetermineBestClassForNewStudentAsync(
                    yearId, req.TargetGradeLevel, req.Gender);
            }

            if (!targetClassId.HasValue || targetClassId.Value == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Chưa có lớp nào thuộc khối '{req.TargetGradeLevel}' trong niên khóa hiện tại. Vui lòng tạo lớp hoặc chọn lớp thủ công."
                });
            }

            var assignedClass = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == targetClassId.Value);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Bước 1: Tự động sinh mã Thiếu nhi chuẩn TN-YYYY-XXXX
                string generatedCode = await _codeGenerator.GenerateUniqueCodeAsync();

                // Bước 2: Tạo mới thực thể Student
                var newStudent = new Student
                {
                    StudentCode = generatedCode,
                    ChristianName = req.ChristianName,
                    FirstName = req.FirstName,
                    LastName = req.LastName,
                    Gender = req.Gender,
                    DateOfBirth = req.DateOfBirth,
                    BaptismDate = req.BaptismDate,
                    ParentPhone = req.ParentPhone,
                    IsActive = true,
                    IsDeleted = false
                };

                _context.Students.Add(newStudent);
                await _context.SaveChangesAsync();

                // Bước 3: Tạo bản ghi Enrollment xếp vào lớp
                var enrollment = new Enrollment
                {
                    StudentId = newStudent.Id,
                    ClassRoomId = targetClassId.Value,
                    IsDeleted = false
                };
                _context.Enrollments.Add(enrollment);

                // Bước 4: Cập nhật trạng thái hồ sơ đăng ký
                var reviewerName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "ExecutiveBoard";
                req.Status = "APPROVED";
                req.CreatedStudentId = newStudent.Id;
                req.AssignedClassId = targetClassId.Value;
                req.AcademicYearId = yearId;
                req.ReviewedBy = reviewerName;
                req.ReviewedAt = DateTime.UtcNow;
                req.RejectionReason = null;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "AUDIT REG_APPROVE: User {Reviewer} approved Registration #{RegId} -> Created Student {StudentCode} (ID={StudentId}) in Class {ClassName} (ID={ClassId})",
                    reviewerName, req.Id, newStudent.StudentCode, newStudent.Id, assignedClass?.Name, targetClassId.Value);

                return Json(new
                {
                    success = true,
                    studentCode = newStudent.StudentCode,
                    className = assignedClass?.Name ?? "",
                    message = $"Đã duyệt thành công! Mã Thiếu Nhi: {newStudent.StudentCode} - Xếp vào lớp: {assignedClass?.Name}."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi hệ thống khi duyệt hồ sơ đăng ký #{RegId}", dto.RequestId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi khi khởi tạo hồ sơ học sinh. Giao dịch đã được hoàn tác an toàn."
                });
            }
        }

        /// <summary>
        /// 4. POST: /StudentRegistrations/Reject (Từ chối hoặc Yêu cầu bổ sung hồ sơ - TASK-P3-02.4)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject([FromBody] RejectRegistrationDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập lý do từ chối hoặc yêu cầu bổ sung hồ sơ." });
            }

            var req = await _context.Set<StudentRegistrationRequest>()
                .FirstOrDefaultAsync(r => r.Id == dto.RequestId);

            if (req == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ đăng ký." });
            }

            if (req.Status == "APPROVED")
            {
                return BadRequest(new { success = false, message = "Không thể từ chối hồ sơ đã được duyệt và cấp mã." });
            }

            var reviewerName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "ExecutiveBoard";
            req.Status = "REJECTED";
            req.RejectionReason = dto.RejectionReason.Trim();
            req.ReviewedBy = reviewerName;
            req.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "AUDIT REG_REJECT: User {Reviewer} rejected Registration #{RegId}. Reason: {Reason}",
                reviewerName, req.Id, req.RejectionReason);

            return Json(new
            {
                success = true,
                message = $"Đã cập nhật trạng thái từ chối / yêu cầu bổ sung cho hồ sơ #{req.Id}."
            });
        }
    }
}