using ClosedXML.Excel;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class AttendanceController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<AttendanceController> _logger;

        public AttendanceController(ParishDbContext context, ILogger<AttendanceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /Attendance/Index (TASK-401)
        /// </summary>
        /// <param name="classId"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(int? classId, DateTime? date)
        {
            var inputDate = date ?? DateTime.Today;
            var targetDate = DateTime.SpecifyKind(inputDate.Date, DateTimeKind.Utc);

            // Lấy niên khóa đang chạy
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            // 1. Khởi tạo truy vấn danh sách lớp của niên khóa
            var classesQuery = _context.Classes
                .Where(c => c.AcademicYearId == yearId);

            // BƯỚC 4: BỘ LỌC PHẠM VI DÀNH CHO TRƯỞNG KHỐI (BRANCH HEAD)
            string? managedGrade = null;
            if (User.IsInRole(UserRole.BranchHead))
            {
                managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrEmpty(managedGrade))
                {
                    // Chỉ lấy các lớp thuộc khối được phân công
                    classesQuery = classesQuery.Where(c => c.GradeLevel == managedGrade);
                }
            }

            var classes = await classesQuery
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Classes = classes;
            ViewBag.SelectedDate = targetDate.ToString("yyyy-MM-dd");

            if (!classes.Any())
            {
                TempData["ErrorMessage"] = User.IsInRole(UserRole.BranchHead)
                    ? $"Không tìm thấy lớp học nào thuộc khối {managedGrade} trong niên khóa hiện tại."
                    : "Chưa có lớp học nào được tạo trong niên khóa hiện tại.";
                return View(new ClassAttendanceSheetViewModel { AttendanceDate = targetDate });
            }

            // 2. Chặn truy cập chéo khối nếu người dùng tự ý truyền classId qua Query String
            if (classId.HasValue && User.IsInRole(UserRole.BranchHead) && !string.IsNullOrEmpty(managedGrade))
            {
                var requestedClass = await _context.Classes.FindAsync(classId.Value);
                if (requestedClass != null && !string.Equals(requestedClass.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid(); // Trả về 403 Forbidden nếu cố tình xem sổ lớp khối khác
                }
            }

            var selectedClassId = classId ?? classes.First().Id;
            var targetClass = classes.FirstOrDefault(c => c.Id == selectedClassId) ?? classes.First();
            ViewBag.SelectedClassId = targetClass.Id;

            // Lấy danh sách học sinh thuộc lớp
            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassRoomId == targetClass.Id && e.Student.IsActive)
                .OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            // Nạp thông tin điểm danh trong ngày đã có trong CSDL
            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.AttendanceDate == targetDate)
                .AsNoTracking()
                .ToDictionaryAsync(a => a.EnrollmentId);

            var studentRows = enrollments.Select(e =>
            {
                attendances.TryGetValue(e.Id, out var att);
                return new StudentAttendanceRowItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    Gender = e.Student.Gender,
                    AttendedMass = att?.AttendedMass ?? false,
                    MassStatus = att?.MassStatus ?? AttendanceStatus.AbsentUnpermitted,
                    MassCheckInTime = att?.MassCheckInTime,
                    ClassAttended = att?.ClassAttended ?? false,
                    ClassStatus = att?.ClassStatus ?? AttendanceStatus.AbsentUnpermitted,
                    ClassCheckInTime = att?.ClassCheckInTime,
                    IsMakeUp = att?.IsMakeUp ?? false,
                    Note = att?.Note
                };
            }).ToList();

            var viewModel = new ClassAttendanceSheetViewModel
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AttendanceDate = targetDate,
                Students = studentRows
            };

            return View(viewModel);
        }


        /// <summary>
        /// 2. POST: /Attendance/SaveBatch (TASK-403)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBatch([FromBody] AttendanceBatchSubmitDto request)
        {
            if (request == null || !request.Items.Any())
            {
                return Json(new { success = false, message = "Không có dữ liệu điểm danh nào được gửi lên." });
            }

            var targetDate = DateTime.SpecifyKind(request.AttendanceDate.Date, DateTimeKind.Utc);
            var enrollmentIds = request.Items.Select(i => i.EnrollmentId).ToList();

            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    // Lấy tất cả bản ghi điểm danh hiện có của danh sách học sinh trong ngày
                    var existingRecords = await _context.Attendances
                        .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.AttendanceDate == targetDate)
                        .ToListAsync();

                    var existingDict = existingRecords.ToDictionary(a => a.EnrollmentId);
                    var newRecords = new List<Attendance>();

                    foreach (var item in request.Items)
                    {
                        if (existingDict.TryGetValue(item.EnrollmentId, out var existing))
                        {
                            // Cập nhật lũy kế (Upsert)
                            existing.AttendedMass = item.AttendedMass;
                            existing.MassStatus = item.MassStatus;
                            existing.ClassAttended = item.ClassAttended;
                            existing.ClassStatus = item.ClassStatus;
                            existing.Note = item.Note;
                        }
                        else
                        {
                            // Tạo mới bản ghi điểm danh
                            newRecords.Add(new Attendance
                            {
                                EnrollmentId = item.EnrollmentId,
                                AttendanceDate = targetDate, // targetDate lúc này đã là UTC
                                DayOfWeek = targetDate.DayOfWeek,
                                AttendedMass = item.AttendedMass,
                                MassStatus = item.MassStatus,
                                ClassAttended = item.ClassAttended,
                                ClassStatus = item.ClassStatus,
                                Note = item.Note,
                                CreatedAt = DateTime.UtcNow // Luôn dùng UtcNow
                            });
                        }
                    }

                    if (newRecords.Any())
                    {
                        await _context.Attendances.AddRangeAsync(newRecords);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                _logger.LogInformation("Lưu thành công sổ điểm danh Lớp ID: {ClassId}, Ngày: {Date:dd/MM/yyyy}, Số lượng: {Count}",
                    request.ClassId, targetDate, request.Items.Count);

                return Json(new { success = true, message = $"Đã lưu sổ điểm danh ngày {targetDate:dd/MM/yyyy} thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu điểm danh hàng loạt Lớp ID: {ClassId}", request.ClassId);
                return Json(new { success = false, message = "Lỗi khi lưu dữ liệu điểm danh: " + ex.Message });
            }
        }

        /// <summary>
        /// GET: /Attendance/ScanQr (TASK-406 - Màn hình quét QR)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> ScanQr()
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            ViewBag.AcademicYearName = currentYear?.Name ?? "Chưa kích hoạt niên khóa";
            return View();
        }

        /// <summary>
        /// POST: /Attendance/ScanCheckIn (TASK-407 - API Xử lý quét mã)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ScanCheckIn([FromBody] QrScanRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.StudentCode))
            {
                return Json(new QrScanResponseDto
                {
                    Success = false,
                    Message = "Mã QR không hợp lệ hoặc để trống!"
                });
            }

            var cleanCode = request.StudentCode.Trim().ToUpper();
            var scanTime = request.ScanTimestamp ?? DateTime.Now;
            var today = DateTime.SpecifyKind(scanTime.Date, DateTimeKind.Utc);

            // 1. Tìm thông tin học sinh và lớp trong niên khóa hiện tại
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            if (currentYear == null)
            {
                return Json(new QrScanResponseDto { Success = false, Message = "Chưa có niên khóa nào được kích hoạt!" });
            }

            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.ClassRoom)
                .FirstOrDefaultAsync(e => e.Student.StudentCode.ToUpper() == cleanCode
                                       && e.ClassRoom.AcademicYearId == currentYear.Id
                                       && e.Student.IsActive);

            if (enrollment == null)
            {
                return Json(new QrScanResponseDto
                {
                    Success = false,
                    StudentCode = cleanCode,
                    Message = $"Không tìm thấy thiếu nhi có mã '{cleanCode}' trong niên khóa {currentYear.Name}!"
                });
            }

            // 2. Tự động nhận diện khung giờ và trạng thái đi trễ
            var (isMass, isClass, status, timeOfDay, message) = AttendanceTimeHelper.EvaluateCheckIn(scanTime, enrollment.ClassRoom.GradeLevel);

            if (!isMass && !isClass)
            {
                // Nếu quét vào ngày thường không phải T5 hay CN, mặc định tính là giờ học hoặc sự kiện
                isClass = true;
                status = AttendanceStatus.Present;
            }

            var studentFullName = $"{enrollment.Student.FirstName} {enrollment.Student.LastName}".Trim();
            var isDuplicate = false;
            var activityTitle = isMass ? "THÁNH LỄ" : "GIỜ HỌC GIÁO LÝ";

            // 3. Thực thi Transaction Upsert an toàn
            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    var record = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.EnrollmentId == enrollment.Id && a.AttendanceDate == today);

                    if (record == null)
                    {
                        record = new Attendance
                        {
                            EnrollmentId = enrollment.Id,
                            AttendanceDate = today,
                            DayOfWeek = today.DayOfWeek,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Attendances.Add(record);
                    }

                    if (isMass)
                    {
                        if (record.AttendedMass)
                        {
                            isDuplicate = true; // Đã quét Lễ rồi
                        }
                        else
                        {
                            record.AttendedMass = true;
                            record.MassStatus = status;
                            record.MassCheckInTime = timeOfDay;
                        }
                    }

                    if (isClass)
                    {
                        if (record.ClassAttended)
                        {
                            isDuplicate = true; // Đã điểm danh lớp rồi
                        }
                        else
                        {
                            record.ClassAttended = true;
                            record.ClassStatus = status;
                            record.ClassCheckInTime = timeOfDay;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                _logger.LogInformation("Quét QR thành công: {Code} - {Name} - Lớp: {Class} - {Activity} ({Status})",
                    cleanCode, studentFullName, enrollment.ClassRoom.Name, activityTitle, status);

                return Json(new QrScanResponseDto
                {
                    Success = true,
                    IsDuplicate = isDuplicate,
                    StudentCode = cleanCode,
                    ChristianName = enrollment.Student.ChristianName,
                    FullName = studentFullName,
                    ClassName = enrollment.ClassRoom.Name,
                    GradeLevel = enrollment.ClassRoom.GradeLevel,
                    ActivityType = activityTitle,
                    Status = status,
                    CheckInTimeStr = timeOfDay.ToString(@"hh\:mm\:ss"),
                    Message = isDuplicate
                        ? $"Thiếu nhi đã được điểm danh {activityTitle} trước đó!"
                        : $"Điểm danh thành công: {studentFullName} ({status})"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý quét mã QR cho mã: {Code}", cleanCode);
                return Json(new QrScanResponseDto
                {
                    Success = false,
                    StudentCode = cleanCode,
                    Message = "Lỗi hệ thống khi lưu kết quả: " + ex.Message
                });
            }
        }


        #region diem danh bù (TASK-408 & TASK-410)
        /// <summary>
        /// 1. GET: /Attendance/GetMissedDates?enrollmentId=... (Lấy danh sách các ngày vắng)
        /// </summary>
        /// <param name="enrollmentId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetMissedDates(long enrollmentId)
        {
            var missedRecords = await _context.Attendances
                .Where(a => a.EnrollmentId == enrollmentId && !a.IsMakeUp
                       && (a.MassStatus == AttendanceStatus.AbsentUnpermitted || a.MassStatus == AttendanceStatus.AbsentPermitted
                           || a.ClassStatus == AttendanceStatus.AbsentUnpermitted || a.ClassStatus == AttendanceStatus.AbsentPermitted))
                .OrderByDescending(a => a.AttendanceDate)
                .AsNoTracking()
                .Select(a => new MissedDateItemDto
                {
                    AttendanceId = a.Id,
                    MissedDate = a.AttendanceDate,
                    DayOfWeekName = a.AttendanceDate.ToString("dddd, dd/MM/yyyy"),
                    MissedMass = a.MassStatus.StartsWith("ABSENT"),
                    MissedClass = a.ClassStatus.StartsWith("ABSENT"),
                    Reason = a.Note ?? ""
                })
                .ToListAsync();

            return Json(new { success = true, data = missedRecords });
        }

        /// <summary>
        /// POST: /Attendance/SubmitMakeUp (TASK-408 & TASK-410: Ghi nhận bù & Ghi Audit Log)
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitMakeUp([FromBody] MakeUpAttendanceRequestDto dto)
        {
            if (dto == null || dto.EnrollmentId <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ!" });
            }

            var origDate = DateTime.SpecifyKind(dto.OriginalMissedDate.Date, DateTimeKind.Utc);
            var makeUpDate = DateTime.SpecifyKind(dto.MakeUpDate.Date, DateTimeKind.Utc);

            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.ClassRoom)
                .FirstOrDefaultAsync(e => e.Id == dto.EnrollmentId);

            if (enrollment == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hồ sơ xếp lớp của thiếu nhi!" });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    // Tìm bản ghi của ngày vắng ban đầu
                    var origRecord = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.EnrollmentId == dto.EnrollmentId && a.AttendanceDate == origDate);

                    var oldMassStatus = origRecord?.MassStatus ?? "NONE";
                    var oldClassStatus = origRecord?.ClassStatus ?? "NONE";

                    if (origRecord == null)
                    {
                        origRecord = new Attendance
                        {
                            EnrollmentId = dto.EnrollmentId,
                            AttendanceDate = origDate,
                            DayOfWeek = origDate.DayOfWeek,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Attendances.Add(origRecord);
                    }

                    // Cập nhật trạng thái ngày vắng thành đã bù
                    origRecord.IsMakeUp = true;
                    origRecord.OriginalMissedDate = origDate;
                    origRecord.Note = $"[Điểm danh bù ngày {makeUpDate:dd/MM/yyyy}]: {dto.Reason}";

                    if (dto.MakeUpMass)
                    {
                        origRecord.AttendedMass = true;
                        origRecord.MassStatus = AttendanceStatus.Present;
                    }

                    if (dto.MakeUpClass)
                    {
                        origRecord.ClassAttended = true;
                        origRecord.ClassStatus = AttendanceStatus.Present;
                    }

                    await _context.SaveChangesAsync();

                    // TỰ ĐỘNG GHI AUDIT LOG VÀO BẢNG AttendanceAuditLogs (TASK-410)
                    var auditLog = new AttendanceAuditLog
                    {
                        AttendanceId = origRecord.Id,
                        ActionType = "MAKE_UP",
                        OldValues = $"MassStatus: {oldMassStatus}, ClassStatus: {oldClassStatus}",
                        NewValues = $"MassStatus: {origRecord.MassStatus}, ClassStatus: {origRecord.ClassStatus}, IsMakeUp: true, MakeUpDate: {makeUpDate:yyyy-MM-dd}",
                        ModifiedBy = User.Identity?.Name ?? "GLV_IN_CHARGE",
                        Reason = dto.Reason,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AttendanceAuditLogs.Add(auditLog);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                return Json(new
                {
                    success = true,
                    message = $"Đã ghi nhận điểm danh bù thành công cho em {enrollment.Student.ChristianName} {enrollment.Student.FirstName} {enrollment.Student.LastName}!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý điểm danh bù cho EnrollmentId: {Id}", dto.EnrollmentId);
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        #endregion

        #region Thong ke
        /// <summary>
        /// GET: /Attendance/Statistics?classId=... (TASK-411: Báo cáo Thống kê)
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> Statistics(int? classId)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var classesQuery = _context.Classes.Where(c => c.AcademicYearId == yearId);

            // Lọc theo khối nếu là BranchHead
            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrEmpty(managedGrade))
                {
                    classesQuery = classesQuery.Where(c => c.GradeLevel == managedGrade);
                }
            }
            var classes = await classesQuery.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).AsNoTracking().ToListAsync();
            ViewBag.Classes = classes;

            if (!classes.Any())
            {
                return View(new AttendanceStatisticsViewModel());
            }

            // Chặn xem chéo
            if (classId.HasValue && User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                var requestedClass = await _context.Classes.FindAsync(classId.Value);
                if (requestedClass != null && requestedClass.GradeLevel != managedGrade)
                {
                    return Forbid();
                }
            }

            var targetClassId = classId ?? classes.First().Id;
            var targetClass = classes.FirstOrDefault(c => c.Id == targetClassId) ?? classes.First();
            ViewBag.SelectedClassId = targetClass.Id;

            // Danh sách học sinh trong lớp
            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassRoomId == targetClass.Id && e.Student.IsActive)
                .OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            // Toàn bộ dữ liệu điểm danh của lớp trong niên khóa
            var allAttendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            // Tổng số buổi lễ và buổi học đã diễn ra
            var distinctMassDates = allAttendances.Where(a => a.AttendedMass || a.MassStatus != AttendanceStatus.AbsentUnpermitted).Select(a => a.AttendanceDate).Distinct().Count();
            var distinctClassDates = allAttendances.Where(a => a.ClassAttended || a.ClassStatus != AttendanceStatus.AbsentUnpermitted).Select(a => a.AttendanceDate).Distinct().Count();

            var totalMassSessions = Math.Max(1, distinctMassDates);
            var totalClassSessions = Math.Max(1, distinctClassDates);

            var statRows = enrollments.Select(e =>
            {
                var attList = allAttendances.Where(a => a.EnrollmentId == e.Id).ToList();

                var massPresent = attList.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Present && a.DayOfWeek == DayOfWeek.Sunday);
                var massLinedUp = attList.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.LinedUp && a.DayOfWeek == DayOfWeek.Sunday);
                var massLate = attList.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late && a.DayOfWeek == DayOfWeek.Sunday);
                var massAbsPermitted = attList.Count(a => a.MassStatus == AttendanceStatus.AbsentPermitted && a.DayOfWeek == DayOfWeek.Sunday);
                var massAbsUnpermitted = attList.Count(a => a.MassStatus == AttendanceStatus.AbsentUnpermitted && a.DayOfWeek == DayOfWeek.Sunday);

                var massThuPresent = attList.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Present && a.DayOfWeek == DayOfWeek.Thursday);
                var massThuAbsPermitted = attList.Count(a => a.MassStatus == AttendanceStatus.AbsentPermitted && a.DayOfWeek == DayOfWeek.Thursday);
                var massThuAbsUnpermitted = attList.Count(a => a.MassStatus == AttendanceStatus.AbsentUnpermitted && a.DayOfWeek == DayOfWeek.Thursday);

                var classPresent = attList.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Present && a.DayOfWeek == DayOfWeek.Sunday);
                var classLate = attList.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Late && a.DayOfWeek == DayOfWeek.Sunday);
                var classAbsPermitted = attList.Count(a => a.ClassStatus == AttendanceStatus.AbsentPermitted && a.DayOfWeek == DayOfWeek.Sunday);
                var classAbsUnpermitted = attList.Count(a => a.ClassStatus == AttendanceStatus.AbsentUnpermitted && a.DayOfWeek == DayOfWeek.Sunday );

                var makeUps = attList.Count(a => a.IsMakeUp);

                // Tính % chuyên cần (Có mặt + 0.5 * Đi trễ)
                var massRate = Math.Round(((massPresent + massLinedUp + massLate * 0.8) / (double)totalMassSessions) * 100, 1);
                var massThuRate = Math.Round(((massThuPresent) / (double)totalMassSessions) * 100, 1);
                var classRate = Math.Round(((classPresent + classLate * 0.8) / (double)totalClassSessions) * 100, 1);

                return new StudentAttendanceStatRow
                {
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    MassLinedUpCount = massLinedUp,
                    MassPresentCount = massPresent,
                    MassLateCount = massLate,
                    MassAbsentPermitted = massAbsPermitted,
                    MassAbsentUnpermitted = massAbsUnpermitted,
                    MassAttendanceRate = Math.Min(100, massRate),

                    MassThuPresentCount = massThuPresent,
                    MassThuAbsentPermitted = massThuAbsPermitted,
                    MassThuAbsentUnpermitted = massThuAbsUnpermitted,
                    MassThuAttendanceRate = Math.Min(100, massThuRate),

                    ClassPresentCount = classPresent,
                    ClassLateCount = classLate,
                    ClassAbsentPermitted = classAbsPermitted,
                    ClassAbsentUnpermitted = classAbsUnpermitted,
                    ClassAttendanceRate = Math.Min(100, classRate),

                    TotalMakeUpCount = makeUps,
                    // Quy chuẩn lãnh bí tích: Cả 2 tỷ lệ >= 80%
                    IsEligibleForSacrament = false // (massRate >= 80 && classRate >= 80)
                };
            }).ToList();

            var viewModel = new AttendanceStatisticsViewModel
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AcademicYearName = currentYear?.Name ?? "",
                TotalMassSessions = totalMassSessions,
                TotalClassSessions = totalClassSessions,
                StudentStats = statRows
            };

            return View(viewModel);
        }

        /// <summary>
        /// GET: /Attendance/ExportStatisticsExcel?classId=... (TASK-413)
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> ExportStatisticsExcel(int classId)
        {
            var targetClass = await _context.Classes
                .Include(c => c.AcademicYear)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (targetClass == null) return NotFound("Không tìm thấy thông tin lớp học");

            var enrollments = targetClass.Enrollments
                .Where(e => e.Student.IsActive)
                .OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName)
                .ToList();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            var allAttendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            // Buổi lễ Chúa Nhật & Lễ Thứ 5
            var totalSundayMass = Math.Max(1, allAttendances.Where(a => a.DayOfWeek == DayOfWeek.Sunday && (a.AttendedMass || a.MassStatus != AttendanceStatus.AbsentUnpermitted)).Select(a => a.AttendanceDate).Distinct().Count());
            var totalThuMass = Math.Max(0, allAttendances.Where(a => a.DayOfWeek == DayOfWeek.Thursday && (a.AttendedMass || a.MassStatus != AttendanceStatus.AbsentUnpermitted)).Select(a => a.AttendanceDate).Distinct().Count());
            var totalClassSessions = Math.Max(1, allAttendances.Where(a => a.ClassAttended || a.ClassStatus != AttendanceStatus.AbsentUnpermitted).Select(a => a.AttendanceDate).Distinct().Count());

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("BaoCaoChuyenCan");

            // 1. Tiêu đề chính
            worksheet.Cell("A1").Value = "BÁO CÁO CHUYÊN CẦN & XÉT ĐIỀU KIỆN LÃNH BÍ TÍCH";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 15;
            worksheet.Range("A1:Q1").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A2").Value = $"Lớp: {targetClass.Name} ({targetClass.GradeLevel}) - Niên khóa: {targetClass.AcademicYear.Name} - Sĩ số: {enrollments.Count} em";
            worksheet.Cell("A2").Style.Font.Italic = true;
            worksheet.Range("A2:Q2").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // 2. Dòng Header 1
            int hRow1 = 4;
            int hRow2 = 5;

            worksheet.Cell(hRow1, 1).Value = "STT";
            worksheet.Range(hRow1, 1, hRow2, 1).Merge();

            worksheet.Cell(hRow1, 2).Value = "Mã QR";
            worksheet.Range(hRow1, 2, hRow2, 2).Merge();

            worksheet.Cell(hRow1, 3).Value = "Tên Thánh, Họ và Tên";
            worksheet.Range(hRow1, 3, hRow2, 3).Merge();

            // Khối Lễ CN (Cột 4 -> 7)
            worksheet.Cell(hRow1, 4).Value = $"CHUYÊN CẦN LỄ CHÚA NHẬT ({totalSundayMass} buổi)";
            worksheet.Range(hRow1, 4, hRow1, 7).Merge().Style.Fill.BackgroundColor = XLColor.FromArgb(230, 245, 230);

            // Khối Lễ T5 (Cột 8 -> 11)
            worksheet.Cell(hRow1, 8).Value = $"CHUYÊN CẦN LỄ THỨ NĂM ({totalThuMass} buổi)";
            worksheet.Range(hRow1, 8, hRow1, 11).Merge().Style.Fill.BackgroundColor = XLColor.FromArgb(235, 245, 240);

            // Khối Học Giáo Lý (Cột 12 -> 15)
            worksheet.Cell(hRow1, 12).Value = $"CHUYÊN CẦN HỌC GIÁO LÝ ({totalClassSessions} buổi)";
            worksheet.Range(hRow1, 12, hRow1, 15).Merge().Style.Fill.BackgroundColor = XLColor.FromArgb(225, 238, 255);

            worksheet.Cell(hRow1, 16).Value = "Số buổi bù";
            worksheet.Range(hRow1, 16, hRow2, 16).Merge();

            worksheet.Cell(hRow1, 17).Value = "Xét Bí Tích";
            worksheet.Range(hRow1, 17, hRow2, 17).Merge();

            // Dòng Header 2 (tiêu đề con)
            string[] subHeaders = { "Có mặt", "Trễ", "Vắng", "% Lễ", "Có mặt", "Trễ", "Vắng", "% Lễ", "Có mặt", "Trễ", "Vắng", "% Học" };
            for (int i = 0; i < subHeaders.Length; i++)
            {
                worksheet.Cell(hRow2, 4 + i).Value = subHeaders[i];
            }

            // Format Headers
            var headerRange = worksheet.Range(hRow1, 1, hRow2, 17);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // 3. Đổ dữ liệu học sinh
            int currRow = 6;
            int stt = 1;

            foreach (var e in enrollments)
            {
                var attList = allAttendances.Where(a => a.EnrollmentId == e.Id).ToList();

                // Lễ CN
                var sunAtt = attList.Where(a => a.DayOfWeek == DayOfWeek.Sunday).ToList();
                var sunPresent = sunAtt.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Present);
                var sunLate = sunAtt.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late);
                var sunAbsent = sunAtt.Count(a => a.MassStatus == AttendanceStatus.AbsentPermitted || a.MassStatus == AttendanceStatus.AbsentUnpermitted);
                var sunRate = Math.Min(100.0, Math.Round(((sunPresent + sunLate * 0.8) / (double)totalSundayMass) * 100, 1));

                // Lễ T5
                var thuAtt = attList.Where(a => a.DayOfWeek == DayOfWeek.Thursday).ToList();
                var thuPresent = thuAtt.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Present);
                var thuLate = thuAtt.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late);
                var thuAbsent = thuAtt.Count(a => a.MassStatus == AttendanceStatus.AbsentPermitted || a.MassStatus == AttendanceStatus.AbsentUnpermitted);
                var thuRate = totalThuMass > 0 ? Math.Min(100.0, Math.Round(((thuPresent + thuLate * 0.8) / (double)totalThuMass) * 100, 1)) : 0;

                // Giờ học
                var classPresent = attList.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Present);
                var classLate = attList.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Late);
                var classAbsent = attList.Count(a => a.ClassStatus == AttendanceStatus.AbsentPermitted || a.ClassStatus == AttendanceStatus.AbsentUnpermitted);
                var classRate = Math.Min(100.0, Math.Round(((classPresent + classLate * 0.8) / (double)totalClassSessions) * 100, 1));

                var makeUps = attList.Count(a => a.IsMakeUp);
                var isEligible = (sunRate >= 80.0 && classRate >= 80.0);

                worksheet.Cell(currRow, 1).Value = stt++;
                worksheet.Cell(currRow, 2).Value = e.Student.StudentCode;
                worksheet.Cell(currRow, 3).Value = $"{e.Student.ChristianName} {e.Student.FirstName} {e.Student.LastName}".Trim();

                // Lễ CN
                worksheet.Cell(currRow, 4).Value = sunPresent;
                worksheet.Cell(currRow, 5).Value = sunLate;
                worksheet.Cell(currRow, 6).Value = sunAbsent;
                worksheet.Cell(currRow, 7).Value = $"{sunRate}%";

                // Lễ T5
                worksheet.Cell(currRow, 8).Value = thuPresent;
                worksheet.Cell(currRow, 9).Value = thuLate;
                worksheet.Cell(currRow, 10).Value = thuAbsent;
                worksheet.Cell(currRow, 11).Value = $"{thuRate}%";

                // Giờ Học
                worksheet.Cell(currRow, 12).Value = classPresent;
                worksheet.Cell(currRow, 13).Value = classLate;
                worksheet.Cell(currRow, 14).Value = classAbsent;
                worksheet.Cell(currRow, 15).Value = $"{classRate}%";

                // Khác
                worksheet.Cell(currRow, 16).Value = makeUps;
                worksheet.Cell(currRow, 17).Value = isEligible ? "ĐỦ ĐIỀU KIỆN" : "CHƯA ĐỦ ĐK";

                // Style màu cho cột kết quả
                if (isEligible)
                {
                    worksheet.Cell(currRow, 17).Style.Font.FontColor = XLColor.Green;
                    worksheet.Cell(currRow, 17).Style.Font.Bold = true;
                }
                else
                {
                    worksheet.Cell(currRow, 17).Style.Font.FontColor = XLColor.Red;
                }

                // Căn lề
                for (int c = 1; c <= 17; c++)
                {
                    worksheet.Cell(currRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    if (c != 3)
                    {
                        worksheet.Cell(currRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }

                currRow++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            var fileName = $"BaoCao_ChuyenCan_{targetClass.Name.Replace(" ", "_")}.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="studentId"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetAuditHistory(long studentId, DateTime date)
        {
            var targetDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            // 1. Tìm thông tin học sinh
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hồ sơ thiếu nhi." });
            }

            var studentFullName = $"{student.ChristianName} {student.FirstName} {student.LastName}".Trim();

            // 2. Tìm bản ghi điểm danh trong ngày
            var attendance = await _context.Attendances
                .AsNoTracking()
                .Include(a => a.Enrollment)
                .FirstOrDefaultAsync(a => a.Enrollment.StudentId == studentId && a.AttendanceDate == targetDate);

            var timeline = new List<AttendanceHistoryDto>();

            if (attendance != null)
            {
                // Tra cứu tất cả nhật ký kiểm toán từ bảng AttendanceAuditLogs
                var auditLogs = await _context.AttendanceAuditLogs
                    .AsNoTracking()
                    .Where(log => log.AttendanceId == attendance.Id)
                    .OrderByDescending(log => log.CreatedAt)
                    .ToListAsync();

                if (auditLogs.Any())
                {
                    foreach (var log in auditLogs)
                    {
                        timeline.Add(new AttendanceHistoryDto
                        {
                            StudentId = student.Id,
                            StudentName = studentFullName,
                            ActionType = log.ActionType,
                            Description = !string.IsNullOrEmpty(log.Reason)
                                ? log.Reason
                                : $"Thao tác {log.ActionType}: {log.NewValues}",
                            PerformedBy = string.IsNullOrEmpty(log.ModifiedBy) ? "Hệ thống" : log.ModifiedBy,
                            Timestamp = log.CreatedAt
                        });
                    }
                }
                else
                {
                    // Nếu chưa có audit log, hiển thị mốc tạo ban đầu
                    timeline.Add(new AttendanceHistoryDto
                    {
                        StudentId = student.Id,
                        StudentName = studentFullName,
                        ActionType = attendance.IsMakeUp ? "MakeupApproval" : "CheckIn",
                        Description = attendance.IsMakeUp
                            ? "Duyệt điểm danh bù / Bổ sung chuyên cần"
                            : (attendance.AttendedMass ? "Quét mã QR điểm danh đầu giờ Lễ" : "Khởi tạo trạng thái điểm danh"),
                        PerformedBy = "Hệ thống",
                        Timestamp = attendance.CreatedAt
                    });
                }
            }
            else
            {
                timeline.Add(new AttendanceHistoryDto
                {
                    StudentId = student.Id,
                    StudentName = studentFullName,
                    ActionType = "NoRecord",
                    Description = "Chưa có lượt ghi nhận điểm danh nào trong ngày này.",
                    PerformedBy = "Hệ thống",
                    Timestamp = DateTime.UtcNow
                });
            }

            return Json(new { success = true, data = timeline.OrderByDescending(t => t.Timestamp).ToList() });
        }

        [HttpGet]
        public async Task<IActionResult> MonthlySummary(int? classId, int? month, int? year)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            var classesQuery = _context.Classes.AsQueryable();

            if (currentYear != null)
            {
                classesQuery = classesQuery.Where(c => c.AcademicYearId == currentYear.Id);
            }

            var classes = await classesQuery.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Classes = classes;

            var selectedClass = classId.HasValue
                ? classes.FirstOrDefault(c => c.Id == classId.Value)
                : classes.FirstOrDefault();

            if (selectedClass == null)
            {
                return View(new MonthlyAttendanceReportViewModel());
            }

            var targetMonth = month ?? DateTime.Today.Month;
            var targetYear = year ?? DateTime.Today.Year;

            // 1. Lọc tất cả các ngày Thứ 5 và Chúa Nhật trong tháng được chọn
            var sessionDates = new List<MonthlyAttendanceSessionHeader>();
            var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
            for (int day = 1; day <= daysInMonth; day++)
            {
                var dt = new DateTime(targetYear, targetMonth, day);
                if (dt.DayOfWeek == DayOfWeek.Thursday || dt.DayOfWeek == DayOfWeek.Sunday)
                {
                    sessionDates.Add(new MonthlyAttendanceSessionHeader
                    {
                        Date = dt,
                        DayOfWeek = dt.DayOfWeek
                    });
                }
            }

            // 2. Lấy danh sách học sinh đã xếp vào lớp
            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassRoomId == selectedClass.Id && e.Student.IsActive)
                .OrderBy(e => e.Student.LastName)
                .ThenBy(e => e.Student.FirstName)
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            // 3. Lấy dữ liệu điểm danh trong tháng của lớp
            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = new DateTime(targetYear, targetMonth, daysInMonth);

            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId) &&
                            a.AttendanceDate >= startDate && a.AttendanceDate <= endDate)
                .ToListAsync();

            // 4. Tổ chức dữ liệu theo từng dòng học sinh
            var rows = new List<StudentMonthlyAttendanceRow>();
            foreach (var en in enrollments)
            {
                var row = new StudentMonthlyAttendanceRow
                {
                    StudentId = en.StudentId,
                    EnrollmentId = en.Id,
                    StudentCode = en.Student.StudentCode,
                    ChristianName = en.Student.ChristianName,
                    FullName = $"{en.Student.FirstName} {en.Student.LastName}".Trim()
                };

                var studentAtts = attendances.Where(a => a.EnrollmentId == en.Id).ToList();

                foreach (var sess in sessionDates)
                {
                    var att = studentAtts.FirstOrDefault(a => a.AttendanceDate.Date == sess.Date.Date);
                    var statusDto = new StudentDailyStatusDto
                    {
                        Date = sess.Date,
                        DayOfWeek = sess.DayOfWeek
                    };

                    if (att != null)
                    {
                        if (sess.IsSunday)
                        {
                            statusDto.HasClassRecord = true;
                            statusDto.ClassStatus = att.ClassStatus;
                            statusDto.ClassCheckInTime = att.ClassCheckInTime;
                            statusDto.IsClassMakeUp = att.IsMakeUp;
                        }

                        // Cộng dồn thống kê
                        if (att.MassStatus == "LINED_UP") row.TotalMassSunInLine++;
                        else if (att.MassStatus == "PRESENT") row.TotalMassSunAttended++;
                        else if (att.MassStatus == "LATE") { row.TotalMassSunAttended++; row.TotalMassSunLate++; }
                        else row.TotalMassSunAbsent++;

                        if (sess.IsSunday)
                        {
                            if (att.ClassStatus == "PRESENT") row.TotalClassAttended++;
                            else if (att.ClassStatus == "LATE") { row.TotalClassAttended++; row.TotalClassLate++; }
                            else row.TotalClassAbsent++;
                        }
                    }
                    else
                    {
                        // Chưa có bản ghi: mặc định là chưa ghi nhận / vắng không phép
                        //statusDto.MassStatus = "ABSENT_UNPERMITTED";
                        if (sess.IsSunday) statusDto.ClassStatus = "ABSENT_UNPERMITTED";
                        row.TotalMassSunAbsent++;
                        if (sess.IsSunday) row.TotalClassAbsent++;
                    }

                    row.DailyStatuses[sess.Date.Date] = statusDto;
                }

                rows.Add(row);
            }

            var viewModel = new MonthlyAttendanceReportViewModel
            {
                ClassId = selectedClass.Id,
                ClassName = selectedClass.Name,
                GradeLevel = selectedClass.GradeLevel,
                SelectedMonth = targetMonth,
                SelectedYear = targetYear,
                SessionDates = sessionDates,
                Rows = rows
            };

            return View(viewModel);
        }

        #region Attendance Rule Configuration
        /// <summary>
        /// 1. GET: /Attendance/GetClassRule?classId=... (Lấy quy ước tính điểm của lớp)
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetClassRule(int classId)
        {
            var targetClass = await _context.Classes.FindAsync(classId);
            if (targetClass == null) return NotFound(new { success = false, message = "Không tìm thấy lớp" });

            // Kiểm tra quyền: Admin, Cha, BĐH hoặc GLV Chủ nhiệm của lớp
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            long.TryParse(userIdStr, out var currentUserId);

            bool isLeadership = User.IsInRole(UserRole.Admin) ||
                                User.IsInRole(UserRole.SpiritualDirector) ||
                                User.IsInRole(UserRole.ExecutiveBoard);

            bool isHeadTeacher = await _context.ClassTeachers
                .AnyAsync(ct => ct.ClassRoomId == classId && ct.UserId == currentUserId && ct.RoleInClass == "HEAD");

            var rule = await _context.AttendanceRuleConfigs.FirstOrDefaultAsync(r => r.ClassRoomId == classId);
            if (rule == null)
            {
                rule = new AttendanceRuleConfig { ClassRoomId = classId };
            }

            return Json(new
            {
                success = true,
                data = new ClassAttendanceRuleDto
                {
                    ClassId = classId,
                    ClassName = targetClass.Name,
                    ThursdayMassWeightPercent = rule.ThursdayMassWeightPercent,
                    SundayMassWeightPercent = rule.SundayMassWeightPercent,
                    ClassWeightPercent = rule.ClassWeightPercent,
                    LateMultiplier = rule.LateMultiplier,
                    PermittedAbsentMultiplier = rule.PermittedAbsentMultiplier,
                    MakeUpBonusRate = rule.MakeUpBonusRate,
                    MinAttendanceRateForSacrament = rule.MinAttendanceRateForSacrament,
                    CanEdit = isLeadership || isHeadTeacher
                }
            });
        }

        // =========================================================================
        // 2. POST: /Attendance/SaveClassRule (GLV Chủ nhiệm lưu quy ước tính điểm)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveClassRule([FromBody] ClassAttendanceRuleDto dto)
        {
            if (dto == null) return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            if (dto.ThursdayMassWeightPercent + dto.SundayMassWeightPercent + dto.ClassWeightPercent != 100)
            {
                return Json(new { success = false, message = "Tổng tỷ trọng Thánh Lễ và Giờ Học phải bằng 100%!" });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            long.TryParse(userIdStr, out var currentUserId);

            bool isLeadership = User.IsInRole(UserRole.Admin) ||
                                User.IsInRole(UserRole.SpiritualDirector) ||
                                User.IsInRole(UserRole.ExecutiveBoard);

            bool isHeadTeacher = await _context.ClassTeachers
                .AnyAsync(ct => ct.ClassRoomId == dto.ClassId && ct.UserId == currentUserId && ct.RoleInClass == "HEAD");

            if (!isLeadership && !isHeadTeacher)
            {
                return Json(new { success = false, message = "Chỉ có Giáo lý viên Chủ nhiệm hoặc Ban Điều Hành mới được quyền cấu hình quy ước tính điểm của lớp này!" });
            }

            var rule = await _context.AttendanceRuleConfigs.FirstOrDefaultAsync(r => r.ClassRoomId == dto.ClassId);
            if (rule == null)
            {
                rule = new AttendanceRuleConfig { ClassRoomId = dto.ClassId };
                _context.AttendanceRuleConfigs.Add(rule);
            }

            rule.ThursdayMassWeightPercent = dto.ThursdayMassWeightPercent;
            rule.SundayMassWeightPercent = dto.SundayMassWeightPercent;
            rule.ClassWeightPercent = dto.ClassWeightPercent;
            rule.LateMultiplier = dto.LateMultiplier;
            rule.PermittedAbsentMultiplier = dto.PermittedAbsentMultiplier;
            rule.MakeUpBonusRate = dto.MakeUpBonusRate;
            rule.MinAttendanceRateForSacrament = dto.MinAttendanceRateForSacrament;
            rule.UpdatedBy = User.Identity?.Name ?? "GLV Chủ nhiệm";
            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã lưu quy ước tính điểm chuyên cần cho lớp thành công!" });
        }

        #endregion
    }
}
