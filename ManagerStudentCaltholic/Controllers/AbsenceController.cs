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
        private const int MaxFailedAttempts = 5;
        private const int MaxPendingRequestsPerEnrollment = 5;
        private const string GenericVerificationError =
            "Thông tin Mã thiếu nhi, Ngày sinh, SĐT Phụ huynh không chính xác hoặc thiếu nhi chưa đủ điều kiện gửi đơn trong năm học hiện tại.";

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

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        /// <summary>
        /// 1. Phụ huynh nộp đơn xin nghỉ trực tuyến (Public không cần đăng nhập)
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)] // SEC-04 FIX 2: Chặn spam & brute-force SĐT
        public async Task<IActionResult> SubmitRequest([FromBody] SubmitAbsenceRequestDto? dto)
        {
            if (dto != null && !string.IsNullOrWhiteSpace(dto.WebsiteUrlHoneypot))
            {
                _logger.LogWarning("SECURITY: Honeypot triggered on Absence SubmitRequest from IP {IP}", GetNormalizedClientIp());
                return BadRequest(new { success = false, message = "Yêu cầu không hợp lệ." });
            }

            var clientIp = GetNormalizedClientIp();
            var cleanCode = (dto?.StudentCode ?? string.Empty).Trim().ToUpperInvariant();
            var (isLockedOut, ipFailed, ipCodeFailed) = await CheckAbsenceLockoutAsync(clientIp, cleanCode);
            if (isLockedOut)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    success = false,
                    message = "Thiết bị này đã nhập sai thông tin quá 5 lần liên tiếp. Vui lòng thử lại sau 15 phút!"
                });
            }

            var verification = await VerifyAbsenceSubmissionAsync(dto);
            if (!verification.IsValid)
            {
                if (verification.CountAsAuthFailure)
                {
                    await RecordAbsenceFailureAsync(clientIp, cleanCode, ipFailed, ipCodeFailed);
                }

                return Json(new { success = false, message = verification.ErrorMessage });
            }

            await ClearAbsenceFailuresAsync(clientIp, cleanCode);

            var newRequest = BuildAbsenceRequestEntity(dto!, verification.Enrollment!.Id);

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
        public async Task<IActionResult> SubmitRequestWithFile([FromForm] SubmitAbsenceRequestDto? dto, IFormFile? attachmentFile)
        {
            if (dto != null && !string.IsNullOrWhiteSpace(dto.WebsiteUrlHoneypot))
            {
                _logger.LogWarning("SECURITY: Honeypot triggered on Absence SubmitRequestWithFile from IP {IP}", GetNormalizedClientIp());
                return BadRequest(new { success = false, message = "Yêu cầu không hợp lệ." });
            }

            var clientIp = GetNormalizedClientIp();
            var cleanCode = (dto?.StudentCode ?? string.Empty).Trim().ToUpperInvariant();
            var (isLockedOut, ipFailed, ipCodeFailed) = await CheckAbsenceLockoutAsync(clientIp, cleanCode);
            if (isLockedOut)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    success = false,
                    message = "Thiết bị này đã nhập sai thông tin quá 5 lần liên tiếp. Vui lòng thử lại sau 15 phút!"
                });
            }

            var verification = await VerifyAbsenceSubmissionAsync(dto);
            if (!verification.IsValid)
            {
                if (verification.CountAsAuthFailure)
                {
                    await RecordAbsenceFailureAsync(clientIp, cleanCode, ipFailed, ipCodeFailed);
                }

                return Json(new { success = false, message = verification.ErrorMessage });
            }

            await ClearAbsenceFailuresAsync(clientIp, cleanCode);

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
                            if (!await FileUploadSecurityHelper.ValidateFormFileMagicBytesAsync(attachmentFile, ext))
                            {
                                return Json(new { success = false, message = "Tệp PDF đính kèm không hợp lệ hoặc đã bị hỏng." });
                            }

                            isPdfAttachment = true;
                            preparedFileName = string.IsNullOrWhiteSpace(safeOriginalName) ? "minh_chung.pdf" : safeOriginalName;
                            preparedContentType = FileUploadSecurityHelper.GetSafeMimeType(ext);
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

                var newRequest = BuildAbsenceRequestEntity(dto!, verification.Enrollment!.Id);
                _context.AbsenceRequests.Add(newRequest);
                await _context.SaveChangesAsync();

                bool hasAttachment = isPdfAttachment || preparedImageStream != null;
                if (hasAttachment && attachmentFile != null)
                {
                    var currentYearName = !string.IsNullOrWhiteSpace(verification.CurrentYear?.Name)
                        ? verification.CurrentYear.Name.Trim()
                        : VietnamTimeHelper.Today.Year.ToString();

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

        private async Task<(bool IsValid, string? ErrorMessage, Student? Student, Enrollment? Enrollment, AcademicYear? CurrentYear, bool CountAsAuthFailure)> VerifyAbsenceSubmissionAsync(
            SubmitAbsenceRequestDto? dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.StudentCode) || string.IsNullOrWhiteSpace(dto.Reason) || string.IsNullOrWhiteSpace(dto.ParentPhone))
            {
                return (false, "Vui lòng nhập đầy đủ Số điện thoại Phụ huynh và Lý do xin phép nghỉ.", null, null, null, false);
            }

            if (dto.DateOfBirth == default || dto.AbsenceDate == default)
            {
                return (false, "Thông tin Ngày sinh hoặc Ngày xin nghỉ không hợp lệ.", null, null, null, false);
            }

            if (dto.Reason.Trim().Length > 500)
            {
                return (false, "Lý do xin phép nghỉ không được vượt quá 500 ký tự.", null, null, null, false);
            }

            var absenceBusinessDate = dto.AbsenceDate.Date;
            var minAllowedDate = VietnamTimeHelper.Today.AddDays(-7);
            var maxAllowedDate = VietnamTimeHelper.Today.AddDays(60);
            if (absenceBusinessDate < minAllowedDate || absenceBusinessDate > maxAllowedDate)
            {
                return (false, "Ngày xin phép nghỉ chỉ được phép trong khoảng từ 7 ngày trước đến 60 ngày tới.", null, null, null, false);
            }

            var cleanCode = dto.StudentCode.Trim().ToUpperInvariant();
            var startDob = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dto.DateOfBirth);
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
                return (false, GenericVerificationError, null, null, null, true);
            }

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.StudentId == student.Id && (currentYear == null || e.ClassRoom.AcademicYearId == currentYear.Id));

            if (enrollment == null)
            {
                return (false, GenericVerificationError, null, null, null, true);
            }

            int pendingCount = await _context.AbsenceRequests
                .AsNoTracking()
                .CountAsync(r => r.EnrollmentId == enrollment.Id && r.Status == "PENDING");
            if (pendingCount >= MaxPendingRequestsPerEnrollment)
            {
                return (false, "Hồ sơ đang có tối đa 5 đơn xin phép chờ duyệt. Vui lòng chờ Giáo lý viên xử lý trước khi gửi thêm.", null, null, null, false);
            }

            var normalizedAbsenceDate = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dto.AbsenceDate);
            var normalizedSession = NormalizeSessionType(dto.SessionType);
            bool duplicatePending = await _context.AbsenceRequests
                .AsNoTracking()
                .AnyAsync(r => r.EnrollmentId == enrollment.Id &&
                               r.AbsenceDate == normalizedAbsenceDate &&
                               r.SessionType == normalizedSession &&
                               r.Status == "PENDING");
            if (duplicatePending)
            {
                return (false, $"Gia đình đã gửi đơn xin phép cho ngày {normalizedAbsenceDate:dd/MM/yyyy} và đang chờ Giáo lý viên xác nhận.", null, null, null, false);
            }

            return (true, null, student, enrollment, currentYear, false);
        }

        private static string NormalizeSessionType(string? rawSessionType)
        {
            var normalizedSession = string.IsNullOrWhiteSpace(rawSessionType)
                ? "ALL"
                : rawSessionType.Trim().ToUpperInvariant();

            return normalizedSession is "ALL" or "MASS" or "CLASS" ? normalizedSession : "ALL";
        }

        private static AbsenceRequest BuildAbsenceRequestEntity(SubmitAbsenceRequestDto dto, long enrollmentId)
        {
            return new AbsenceRequest
            {
                EnrollmentId = enrollmentId,
                AbsenceDate = VietnamTimeHelper.NormalizeBusinessDateUtcKind(dto.AbsenceDate),
                SessionType = NormalizeSessionType(dto.SessionType),
                Reason = dto.Reason.Trim(),
                ParentPhone = dto.ParentPhone?.Trim(),
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };
        }

        private string GetNormalizedClientIp()
        {
            var ip = HttpContext.Connection.RemoteIpAddress;
            if (ip == null) return "unknown_ip";
            return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();
        }

        private static string GetIpLockoutKey(string clientIp) => $"absence:fail:ip:{clientIp}";

        private static string GetIpCodeLockoutKey(string clientIp, string cleanCode) =>
            $"absence:fail:ip_code:{clientIp}:{cleanCode}";

        private async Task<(bool IsLockedOut, int IpFailed, int IpCodeFailed)> CheckAbsenceLockoutAsync(string clientIp, string cleanCode)
        {
            int ipFailed = await _cache.GetAsync<int>(GetIpLockoutKey(clientIp));
            int ipCodeFailed = string.IsNullOrEmpty(cleanCode)
                ? 0
                : await _cache.GetAsync<int>(GetIpCodeLockoutKey(clientIp, cleanCode));
            return (ipFailed >= MaxFailedAttempts || ipCodeFailed >= MaxFailedAttempts, ipFailed, ipCodeFailed);
        }

        private async Task RecordAbsenceFailureAsync(string clientIp, string cleanCode, int currentIpFailed, int currentIpCodeFailed)
        {
            await _cache.SetAsync(GetIpLockoutKey(clientIp), currentIpFailed + 1, TimeSpan.FromMinutes(15));
            if (!string.IsNullOrEmpty(cleanCode))
            {
                await _cache.SetAsync(GetIpCodeLockoutKey(clientIp, cleanCode), currentIpCodeFailed + 1, TimeSpan.FromMinutes(30));
            }
        }

        private async Task ClearAbsenceFailuresAsync(string clientIp, string cleanCode)
        {
            await _cache.RemoveAsync(GetIpLockoutKey(clientIp));
            if (!string.IsNullOrEmpty(cleanCode))
            {
                await _cache.RemoveAsync(GetIpCodeLockoutKey(clientIp, cleanCode));
            }
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
        public async Task<IActionResult> Review([FromBody] ReviewAbsenceRequestDto? dto)
        {
            if (dto == null || dto.RequestId <= 0)
            {
                return BadRequest(new { success = false, message = "Dữ liệu duyệt đơn không hợp lệ." });
            }

            var req = await _context.AbsenceRequests
                .Include(r => r.Enrollment)
                    .ThenInclude(e => e.ClassRoom)
                .FirstOrDefaultAsync(r => r.Id == dto.RequestId);

            if (req == null)
                return Json(new { success = false, message = "Không tìm thấy đơn." });

            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var callerUserId);
            var linkedStudentId = await StaffSelfActionGuard.GetLinkedStudentIdAsync(_context, callerUserId);
            if (StaffSelfActionGuard.IsSelfStudentAction(linkedStudentId, req.Enrollment.StudentId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Bạn không được phép tự duyệt đơn xin phép cho chính hồ sơ thiếu nhi của mình!"
                });
            }

            // SEC-04 FIX 4: Kiểm tra người duyệt có quyền trên lớp của học sinh này không
            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var authorizedClasses = await GetAuthorizedClassesAsync(currentYear?.Id ?? 0);
            if (!authorizedClasses.Any(c => c.Id == req.Enrollment.ClassRoomId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Bạn không có thẩm quyền duyệt đơn xin phép của lớp này!"
                });
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
                var attDate = VietnamTimeHelper.NormalizeBusinessDateUtcKind(req.AbsenceDate);
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
                        IpAddress = RedisAttendanceService.SafeTruncate(GetNormalizedClientIp(), 45),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            if (dto.IsApproved)
            {
                var attDateUtc = VietnamTimeHelper.NormalizeBusinessDateUtcKind(req.AbsenceDate);
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

            if (!IsLeadership)
            {
                // Nếu là Trưởng khối: Chỉ lấy các lớp thuộc khối mình phụ trách (fail-closed nếu trống)
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    if (string.IsNullOrWhiteSpace(managedGrade))
                    {
                        return new List<ClassRoom>();
                    }

                    var trimmedGrade = managedGrade.Trim();
                    classesQuery = classesQuery.Where(c => c.GradeLevel == trimmedGrade);
                }
                // Nếu là Giáo lý viên / GLV Dự Bị: Chỉ lấy các lớp được phân công đứng lớp
                else if (User.IsInRole(UserRole.Teacher) || User.IsInRole(UserRole.ProbationaryTeacher))
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!long.TryParse(userIdStr, out var currentUserId) || currentUserId <= 0)
                    {
                        return new List<ClassRoom>();
                    }

                    var assignedClassIds = await _context.ClassTeachers
                        .AsNoTracking()
                        .Where(ct => ct.UserId == currentUserId && (currentYearId == 0 || ct.AcademicYearId == currentYearId))
                        .Select(ct => ct.ClassRoomId)
                        .ToListAsync();

                    classesQuery = classesQuery.Where(c => assignedClassIds.Contains(c.Id));
                }
                else
                {
                    return new List<ClassRoom>();
                }
            }

            var classes = await classesQuery
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            if (!IsLeadership && User.IsInRole(UserRole.ProbationaryTeacher))
            {
                classes = classes
                    .Where(c => !GradeLevel.IsAfternoonSession(c.Session, c.GradeLevel))
                    .ToList();
            }

            return classes;
        }
    }
}