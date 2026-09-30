using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
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

            var classes = await _context.Classes
                .Where(c => c.AcademicYearId == yearId)
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Classes = classes;
            ViewBag.SelectedDate = targetDate.ToString("yyyy-MM-dd");

            if (!classes.Any())
            {
                TempData["ErrorMessage"] = "Chưa có lớp học nào được tạo trong niên khóa hiện tại.";
                return View(new ClassAttendanceSheetViewModel { AttendanceDate = targetDate });
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
        [ValidateAntiForgeryToken]
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
            var (isMass, isClass, status, timeOfDay) = AttendanceTimeHelper.EvaluateCheckIn(scanTime, enrollment.ClassRoom.GradeLevel);

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
    }
}
