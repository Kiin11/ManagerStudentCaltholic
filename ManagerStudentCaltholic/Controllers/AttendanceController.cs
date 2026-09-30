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
    }
}
