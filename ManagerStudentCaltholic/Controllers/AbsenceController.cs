using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Options;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    // SEC-04 FIX 1: Mặc định yêu cầu quyền RequireStaff ở cấp Controller
    [Authorize(Policy = "RequireStaff")]
    public class AbsenceController : Controller
    {
        private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".webp"
        };

        private readonly ParishDbContext _context;
        private readonly ICacheService _cache;
        private readonly IGoogleDriveStorageService _storageService;
        private readonly IImageOptimizationService _imageOptimizationService;
        private readonly GoogleDriveOptions _driveOptions;
        private readonly ILogger<AbsenceController> _logger;

        public AbsenceController(
            ParishDbContext context,
            ICacheService cache,
            IGoogleDriveStorageService storageService,
            IImageOptimizationService imageOptimizationService,
            IOptions<GoogleDriveOptions> driveOptions,
            ILogger<AbsenceController> logger)
        {
            _context = context;
            _cache = cache;
            _storageService = storageService;
            _imageOptimizationService = imageOptimizationService;
            _driveOptions = driveOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// 1. Phụ huynh nộp đơn xin nghỉ trực tuyến (Public không cần đăng nhập)
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)] // SEC-04 FIX 2: Chặn spam & brute-force SĐT
        public async Task<IActionResult> SubmitRequest([FromBody] SubmitAbsenceRequestDto dto)
        {
            var verification = await VerifyAbsenceSubmissionAsync(dto);
            if (!verification.IsValid)
            {
                return Json(new { success = false, message = verification.ErrorMessage });
            }

            var newRequest = BuildAbsenceRequestEntity(dto, verification.Enrollment!.Id);

            _context.AbsenceRequests.Add(newRequest);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Gửi đơn xin phép thành công! Giáo lý viên chủ nhiệm sẽ sớm xem và xác nhận." });
        }

        /// <summary>
        /// 1b. Phụ huynh nộp đơn xin nghỉ kèm tệp minh chứng (giấy khám bệnh / đơn xin phép)
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> SubmitRequestWithFile([FromForm] SubmitAbsenceRequestDto dto, IFormFile? attachmentFile)
        {
            var verification = await VerifyAbsenceSubmissionAsync(dto);
            if (!verification.IsValid)
            {
                return Json(new { success = false, message = verification.ErrorMessage });
            }

            Stream? preparedImageStream = null;
            string? preparedFileName = null;
            string? preparedContentType = null;
            bool isPdfAttachment = false;

            try
            {
                if (attachmentFile != null)
                {
                    if (attachmentFile.Length == 0 && !string.IsNullOrWhiteSpace(attachmentFile.FileName))
                    {
                        return Json(new { success = false, message = "Tệp đính kèm trống (0 byte). Vui lòng chọn lại tệp hợp lệ." });
                    }

                    if (attachmentFile.Length > 0)
                    {
                        var maxBytes = _driveOptions.MaxDocumentBytes > 0
                            ? _driveOptions.MaxDocumentBytes
                            : 10 * 1024 * 1024;

                        if (attachmentFile.Length > maxBytes)
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"Tệp đính kèm vượt quá dung lượng tối đa {maxBytes / (1024 * 1024)} MB."
                            });
                        }

                        var safeOriginalName = Path.GetFileName(attachmentFile.FileName);
                        var ext = Path.GetExtension(safeOriginalName).ToLowerInvariant();
                        if (!AllowedAttachmentExtensions.Contains(ext))
                        {
                            return Json(new
                            {
                                success = false,
                                message = "Chỉ chấp nhận tệp đính kèm định dạng .pdf, .jpg, .jpeg, .png hoặc .webp."
                            });
                        }

                        if (ext == ".pdf")
                        {
                            if (!await HasValidPdfMagicBytesAsync(attachmentFile))
                            {
                                return Json(new { success = false, message = "Tệp PDF đính kèm không hợp lệ hoặc đã bị hỏng." });
                            }

                            isPdfAttachment = true;
                            preparedFileName = string.IsNullOrWhiteSpace(safeOriginalName) ? "minh_chung.pdf" : safeOriginalName;
                            preparedContentType = "application/pdf";
                        }
                        else
                        {
                            try
                            {
                                await using var rawImageStream = attachmentFile.OpenReadStream();
                                var optimized = await _imageOptimizationService.OptimizeToWebpAsync(
                                    rawImageStream,
                                    maxWidth: 1600,
                                    maxHeight: 1600,
                                    quality: 78);

                                preparedImageStream = optimized.OptimizedStream;
                                preparedFileName = Path.ChangeExtension(
                                    string.IsNullOrWhiteSpace(safeOriginalName) ? "minh_chung.webp" : safeOriginalName,
                                    optimized.FileExtension);
                                preparedContentType = optimized.ContentType;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Từ chối hình ảnh đính kèm đơn xin nghỉ không hợp lệ.");
                                return Json(new { success = false, message = "Hình ảnh đính kèm không hợp lệ hoặc đã bị hỏng." });
                            }
                        }
                    }
                }

                var newRequest = BuildAbsenceRequestEntity(dto, verification.Enrollment!.Id);
                _context.AbsenceRequests.Add(newRequest);
                await _context.SaveChangesAsync();

                bool hasAttachment = isPdfAttachment || preparedImageStream != null;
                if (hasAttachment && attachmentFile != null)
                {
                    var currentYearName = !string.IsNullOrWhiteSpace(verification.CurrentYear?.Name)
                        ? verification.CurrentYear.Name.Trim()
                        : DateTime.UtcNow.Year.ToString();

                    try
                    {
                        await using var pdfStream = isPdfAttachment ? attachmentFile.OpenReadStream() : null;
                        var uploadStream = pdfStream ?? preparedImageStream!;

                        await _storageService.UploadAsync(new CloudUploadRequest
                        {
                            Content = uploadStream,
                            OriginalFileName = preparedFileName!,
                            ContentType = preparedContentType!,
                            Category = CloudMediaCategory.AbsenceDoc,
                            EntityName = nameof(AbsenceRequest),
                            EntityId = newRequest.Id,
                            FolderSegments = new[] { currentYearName, "AbsenceRequests" },
                            UploadedBy = $"Parent:{verification.Student!.StudentCode}",
                            AllowLocalFallback = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Đã lưu AbsenceRequest Id={RequestId} nhưng tải tệp đính kèm lên storage thất bại.",
                            newRequest.Id);

                        return Json(new
                        {
                            success = true,
                            message = "Đã ghi nhận đơn xin phép nghỉ, tuy nhiên tệp đính kèm gặp sự cố khi lưu trữ."
                        });
                    }
                }

                return Json(new
                {
                    success = true,
                    message = hasAttachment
                        ? "Gửi đơn xin phép (kèm tệp minh chứng) thành công! Giáo lý viên chủ nhiệm sẽ sớm xem và xác nhận."
                        : "Gửi đơn xin phép thành công! Giáo lý viên chủ nhiệm sẽ sớm xem và xác nhận."
                });
            }
            finally
            {
                if (preparedImageStream != null)
                {
                    await preparedImageStream.DisposeAsync();
                }
            }
        }

        private async Task<(bool IsValid, string? ErrorMessage, Student? Student, Enrollment? Enrollment, AcademicYear? CurrentYear)> VerifyAbsenceSubmissionAsync(
            SubmitAbsenceRequestDto? dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.StudentCode) || string.IsNullOrWhiteSpace(dto.Reason) || string.IsNullOrWhiteSpace(dto.ParentPhone))
            {
                return (false, "Vui lòng nhập đầy đủ Số điện thoại Phụ huynh và Lý do xin phép nghỉ.", null, null, null);
            }

            if (dto.DateOfBirth == default || dto.AbsenceDate == default)
            {
                return (false, "Thông tin Ngày sinh hoặc Ngày xin nghỉ không hợp lệ.", null, null, null);
            }

            if (dto.Reason.Trim().Length > 500)
            {
                return (false, "Lý do xin phép nghỉ không được vượt quá 500 ký tự.", null, null, null);
            }

            var cleanCode = dto.StudentCode.Trim().ToUpperInvariant();
            var startDob = DateTime.SpecifyKind(dto.DateOfBirth.Date, DateTimeKind.Utc);
            var endDob = startDob.AddDays(1);

            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentCode.ToUpper() == cleanCode &&
                                          s.DateOfBirth >= startDob && s.DateOfBirth < endDob &&
                                          s.IsActive);

            // Kiểm tra đối chiếu SĐT Phụ huynh gửi lên với SĐT trong hồ sơ
            var inputPhoneDigits = new string(dto.ParentPhone.Where(char.IsDigit).ToArray());
            var recordPhoneDigits = new string((student?.ParentPhone ?? "").Where(char.IsDigit).ToArray());

            if (student == null || string.IsNullOrEmpty(recordPhoneDigits) || inputPhoneDigits != recordPhoneDigits)
            {
                return (false, "Số điện thoại Phụ huynh không khớp với hồ sơ đã đăng ký của thiếu nhi!", null, null, null);
            }

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.StudentId == student.Id && (currentYear == null || e.ClassRoom.AcademicYearId == currentYear.Id));

            if (enrollment == null)
            {
                return (false, "Thiếu nhi chưa được xếp vào lớp học trong năm nay.", null, null, null);
            }

            return (true, null, student, enrollment, currentYear);
        }

        private static AbsenceRequest BuildAbsenceRequestEntity(SubmitAbsenceRequestDto dto, long enrollmentId)
        {
            var normalizedSession = string.IsNullOrWhiteSpace(dto.SessionType)
                ? "ALL"
                : dto.SessionType.Trim().ToUpperInvariant();

            if (normalizedSession is not ("ALL" or "MASS" or "CLASS"))
            {
                normalizedSession = "ALL";
            }

            return new AbsenceRequest
            {
                EnrollmentId = enrollmentId,
                AbsenceDate = dto.AbsenceDate.Date,
                SessionType = normalizedSession,
                Reason = dto.Reason.Trim(),
                ParentPhone = dto.ParentPhone?.Trim(),
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };
        }

        private static async Task<bool> HasValidPdfMagicBytesAsync(IFormFile file)
        {
            if (file.Length < 4)
            {
                return false;
            }

            byte[] header = new byte[4];
            await using var stream = file.OpenReadStream();
            int bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));
            return bytesRead >= 4
                && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;
        }

        /// <summary>
        /// 2. GLV / BĐH Xem danh sách đơn xin phép theo lớp
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        public async Task<IActionResult> Manage(int? classId)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            // SEC-04 FIX 3: Chỉ lấy danh sách lớp nằm trong phạm vi quyền hạn
            var authorizedClasses = await GetAuthorizedClassesAsync(yearId);
            ViewBag.Classes = authorizedClasses;

            if (!authorizedClasses.Any())
            {
                ViewBag.SelectedClassId = 0;
                ViewBag.AbsenceAttachments = new Dictionary<long, List<CloudMediaSummaryDto>>();
                return View(new List<AbsenceRequest>());
            }

            // Chặn xem chéo lớp nếu tự truyền classId qua URL
            if (classId.HasValue && !authorizedClasses.Any(c => c.Id == classId.Value))
            {
                return Forbid();
            }

            var selectedClassId = classId ?? authorizedClasses.First().Id;
            ViewBag.SelectedClassId = selectedClassId;

            var requests = await _context.AbsenceRequests
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.Student)
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.ClassRoom)
                .Where(r => r.Enrollment.ClassRoomId == selectedClassId)
                .OrderByDescending(r => r.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var requestIds = requests.Select(r => r.Id).ToList();
            ViewBag.AbsenceAttachments = requestIds.Count > 0
                ? await _storageService.GetMediaMapForEntitiesAsync(
                    nameof(AbsenceRequest),
                    requestIds,
                    CloudMediaCategory.AbsenceDoc)
                : new Dictionary<long, List<CloudMediaSummaryDto>>();

            return View(requests);
        }

        /// <summary>
        /// 3. GLV / BĐH Duyệt đơn & Tự động đồng bộ sang bảng Điểm Danh (Attendances)
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review([FromBody] ReviewAbsenceRequestDto dto)
        {
            var req = await _context.AbsenceRequests
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.ClassRoom)
                .FirstOrDefaultAsync(r => r.Id == dto.RequestId);

            if (req == null)
                return Json(new { success = false, message = "Không tìm thấy đơn." });

            // SEC-04 FIX 4: Kiểm tra người duyệt có quyền trên lớp của học sinh này không
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var authorizedClasses = await GetAuthorizedClassesAsync(currentYear?.Id ?? 0);
            if (!authorizedClasses.Any(c => c.Id == req.Enrollment.ClassRoomId))
            {
                return Json(new { success = false, message = "Bạn không có thẩm quyền duyệt đơn xin phép của lớp này!" });
            }

            var fullName = User.FindFirst("FullName")?.Value;
            var username = User.Identity?.Name;
            var operatorName = (!string.IsNullOrWhiteSpace(fullName) && !string.IsNullOrWhiteSpace(username))
                ? $"{fullName} ({username})"
                : (!string.IsNullOrWhiteSpace(fullName) ? fullName : (!string.IsNullOrWhiteSpace(username) ? username : "GLV"));

            req.Status = dto.IsApproved ? "APPROVED" : "REJECTED";
            req.ReviewNote = dto.Note;
            req.ReviewedBy = operatorName;
            req.ReviewedAt = DateTime.UtcNow;

            // Nếu DUYỆT: Tự động cập nhật Attendance sang vắng có phép + Ghi Audit Log
            if (dto.IsApproved)
            {
                var attDate = req.AbsenceDate.Date;
                var att = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.EnrollmentId == req.EnrollmentId && a.AttendanceDate == attDate);

                bool isNew = att == null;
                bool oldMassAtt = att?.AttendedMass ?? false;
                string oldMassSt = att?.MassStatus ?? AttendanceStatus.AbsentUnpermitted;
                bool oldClassAtt = att?.ClassAttended ?? false;
                string oldClassSt = att?.ClassStatus ?? AttendanceStatus.AbsentUnpermitted;
                string? oldNote = att?.Note;

                if (att == null)
                {
                    att = new Attendance
                    {
                        EnrollmentId = req.EnrollmentId,
                        AttendanceDate = attDate,
                        DayOfWeek = attDate.DayOfWeek
                    };
                    _context.Attendances.Add(att);
                }

                if (req.SessionType == "MASS" || req.SessionType == "ALL")
                {
                    att.AttendedMass = false;
                    att.MassStatus = "ABSENT_PERMITTED";
                }

                if (req.SessionType == "CLASS" || req.SessionType == "ALL")
                {
                    att.ClassAttended = false;
                    att.ClassStatus = "ABSENT_PERMITTED";
                }

                await _context.SaveChangesAsync();

                if (isNew || RedisAttendanceService.HasAttendanceStateChanged(
                        oldMassAtt, oldMassSt, att.AttendedMass, att.MassStatus,
                        oldClassAtt, oldClassSt, att.ClassAttended, att.ClassStatus,
                        oldNote, att.Note))
                {
                    _context.AttendanceAuditLogs.Add(new AttendanceAuditLog
                    {
                        AttendanceId = att.Id,
                        ActionType = "EXCUSE_APPROVED",
                        OldValues = RedisAttendanceService.FormatStateSnapshot(oldMassAtt, oldMassSt, oldClassAtt, oldClassSt, oldNote, isNewRecord: isNew),
                        NewValues = RedisAttendanceService.FormatStateSnapshot(att.AttendedMass, att.MassStatus, att.ClassAttended, att.ClassStatus, att.Note),
                        ModifiedBy = RedisAttendanceService.SafeTruncate(operatorName, 100) ?? "GLV",
                        Reason = RedisAttendanceService.SafeTruncate($"Duyệt đơn xin phép ({req.SessionType}): {req.Reason}", 255),
                        IpAddress = RedisAttendanceService.SafeTruncate(HttpContext.Connection.RemoteIpAddress?.ToString(), 45),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            if (dto.IsApproved)
            {
                var attDateUtc = DateTime.SpecifyKind(req.AbsenceDate.Date, DateTimeKind.Utc);
                await _cache.RemoveAsync(RedisAttendanceKeys.ClassSheet(req.Enrollment.ClassRoomId, attDateUtc));
                await _cache.RemoveAsync(RedisAttendanceKeys.ClassSessionInfo(req.Enrollment.ClassRoomId, attDateUtc));
                await _cache.RemoveAsync($"staff_lookup:student_360:{req.Enrollment.StudentId}");
            }

            return Json(new { success = true, message = dto.IsApproved ? "Đã duyệt đơn và ghi nhận vắng có phép!" : "Đã từ chối đơn." });
        }

        /// <summary>
        /// SEC - 04 FIX 2: Hàm phụ trợ lấy danh sách lớp học mà người dùng hiện tại được phép quản lý đơn nghỉ phép
        /// </summary>
        private async Task<List<ClassRoom>> GetAuthorizedClassesAsync(int currentYearId)
        {
            var classesQuery = _context.Classes
                .Where(c => currentYearId == 0 || c.AcademicYearId == currentYearId);

            // Nếu là Trưởng khối: Chỉ lấy các lớp thuộc khối mình phụ trách
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    // SEC-08 FIX: Trưởng khối chưa được phân công khối nào thì không trả về dữ liệu toàn trường
                    classesQuery = classesQuery.Where(_ => false);
                }
                else
                {
                    classesQuery = classesQuery.Where(c => c.GradeLevel == managedGrade);
                }
            }
            // Nếu là Giáo lý viên: Chỉ lấy các lớp được phân công đứng lớp
            else if (User.IsInRole(UserRole.Teacher))
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                long.TryParse(userIdStr, out var currentUserId);

                var assignedClassIds = await _context.ClassTeachers
                    .Where(ct => ct.UserId == currentUserId && (currentYearId == 0 || ct.AcademicYearId == currentYearId))
                    .Select(ct => ct.ClassRoomId)
                    .ToListAsync();

                classesQuery = classesQuery.Where(c => assignedClassIds.Contains(c.Id));
            }

            return await classesQuery
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}