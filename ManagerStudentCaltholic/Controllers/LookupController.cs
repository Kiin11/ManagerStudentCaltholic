using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QRCoder;

namespace ManagerStudentCaltholic.Controllers
{
    [AllowAnonymous] // Cho phép truy cập công khai không cần đăng nhập
    [Route("TraCuu")]
    public class LookupController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LookupController> _logger;

        public LookupController(
            ParishDbContext context,
            IGradeCalculationService gradeService,
            IMemoryCache cache,
            ILogger<LookupController> logger)
        {
            _context = context;
            _gradeService = gradeService;
            _cache = cache;
            _logger = logger;
        }

        // GET: /TraCuu
        [HttpGet("")]
        public IActionResult Index()
        {
            return View(new StudentLookupRequestDto());
        }

        // POST: /TraCuu/Search
        [HttpPost("Search")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Search([FromForm] StudentLookupRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            // 1. RATE LIMITING: Chống Brute-force quét mã (Tối đa 5 lần sai / 15 phút trên mỗi IP)
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
            var rateLimitKey = $"Lookup_FailedAttempts_{clientIp}";

            if (_cache.TryGetValue(rateLimitKey, out int failedCount) && failedCount >= 5)
            {
                ModelState.AddModelError(string.Empty, "Bạn đã nhập sai thông tin quá 5 lần liên tiếp. Để đảm bảo an toàn, vui lòng thử lại sau 15 phút!");
                return View("Index", model);
            }

            var cleanCode = model.StudentCode.Trim().ToUpperInvariant();
            var targetDob = model.DateOfBirth.Date;

            // 2. Tìm kiếm học sinh khớp Mã HS và Ngày Sinh
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentCode.ToUpper() == cleanCode && s.DateOfBirth.Date == targetDob && s.IsActive);

            if (student == null)
            {
                // Tăng số lần thử sai
                failedCount++;
                _cache.Set(rateLimitKey, failedCount, TimeSpan.FromMinutes(15));

                ModelState.AddModelError(string.Empty, "Không tìm thấy hồ sơ! Vui lòng kiểm tra lại chính xác Mã Thiếu Nhi và Ngày Sinh.");
                return View("Index", model);
            }

            // Reset bộ đếm lỗi khi nhập đúng
            _cache.Remove(rateLimitKey);

            // 3. Lấy niên khóa hiện hành
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
            if (currentYear == null)
            {
                currentYear = await _context.AcademicYears.OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();
            }

            // 4. Lấy lớp học hiện tại của học sinh
            var enrollment = await _context.Enrollments
                .Include(e => e.ClassRoom)
                    .ThenInclude(c => c.ClassTeachers)
                .Include(e => e.GradeRecords)
                    .ThenInclude(g => g.GradeConfiguration)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.StudentId == student.Id &&
                                          (currentYear == null || e.ClassRoom.AcademicYearId == currentYear.Id));

            // 5. Thống kê Chuyên cần Lễ & Học của em
            int totalMass = 0, attendedMass = 0;
            int totalClass = 0, attendedClass = 0;

            if (enrollment != null)
            {
                var attendances = await _context.Attendances
                    .Where(a => a.EnrollmentId == enrollment.Id)
                    .AsNoTracking()
                    .ToListAsync();

                totalMass = attendances.Count(a => a.DayOfWeek == DayOfWeek.Thursday || a.DayOfWeek == DayOfWeek.Sunday);
                attendedMass = attendances.Count(a => a.AttendedMass);

                totalClass = attendances.Count(a => a.DayOfWeek == DayOfWeek.Sunday);
                attendedClass = attendances.Count(a => a.ClassAttended);
            }

            var massRate = totalMass > 0 ? Math.Round((attendedMass / (double)totalMass) * 100, 1) : 100.0;
            var classRate = totalClass > 0 ? Math.Round((attendedClass / (double)totalClass) * 100, 1) : 100.0;
            var combinedAttendanceRate = Math.Round((massRate + classRate) / 2.0, 1);

            // 6. Xử lý Bảng điểm HK1 & HK2
            var hk1Detail = new SemesterGradeDetail { Semester = 1 };
            var hk2Detail = new SemesterGradeDetail { Semester = 2 };

            if (enrollment != null && currentYear != null)
            {
                var gradeConfigs = await _context.GradeConfigurations
                    .Where(g => g.AcademicYearId == currentYear.Id &&
                                (g.GradeLevel == "ALL" || g.GradeLevel == enrollment.ClassRoom.GradeLevel))
                    .OrderBy(g => g.ColumnIndex)
                    .AsNoTracking()
                    .ToListAsync();

                // Lọc điểm HK1
                var hk1Configs = gradeConfigs.Where(g => g.Semester == 1).ToList();
                var hk1Scores = new Dictionary<int, decimal?>();
                foreach (var cfg in hk1Configs)
                {
                    var rec = enrollment.GradeRecords.FirstOrDefault(r => r.GradeConfigurationId == cfg.Id);
                    hk1Detail.Items.Add(new GradeItemView
                    {
                        DisplayName = cfg.DisplayName,
                        ScoreType = cfg.ScoreType,
                        WeightFactor = cfg.WeightFactor,
                        Score = rec?.Score,
                        IsSacramentExam = cfg.IsSacramentExam
                    });
                    hk1Scores[cfg.Id] = rec?.Score;
                }
                hk1Detail.SemesterAverage = _gradeService.CalculateSemesterAverage(hk1Scores, hk1Configs);
                hk1Detail.Rank = _gradeService.EvaluateStudent(hk1Detail.SemesterAverage, combinedAttendanceRate, enrollment.ClassRoom.GradeLevel, 1).AcademicRank;

                // Lọc điểm HK2
                var hk2Configs = gradeConfigs.Where(g => g.Semester == 2).ToList();
                var hk2Scores = new Dictionary<int, decimal?>();
                foreach (var cfg in hk2Configs)
                {
                    var rec = enrollment.GradeRecords.FirstOrDefault(r => r.GradeConfigurationId == cfg.Id);
                    hk2Detail.Items.Add(new GradeItemView
                    {
                        DisplayName = cfg.DisplayName,
                        ScoreType = cfg.ScoreType,
                        WeightFactor = cfg.WeightFactor,
                        Score = rec?.Score,
                        IsSacramentExam = cfg.IsSacramentExam
                    });
                    hk2Scores[cfg.Id] = rec?.Score;
                }
                hk2Detail.SemesterAverage = _gradeService.CalculateSemesterAverage(hk2Scores, hk2Configs);
                hk2Detail.Rank = _gradeService.EvaluateStudent(hk2Detail.SemesterAverage, combinedAttendanceRate, enrollment.ClassRoom.GradeLevel, 2).AcademicRank;
            }

            var yearAverage = _gradeService.CalculateYearAverage(hk1Detail.SemesterAverage, hk2Detail.SemesterAverage);
            var (overallRank, isEligible) = _gradeService.EvaluateStudent(yearAverage, combinedAttendanceRate, enrollment?.ClassRoom?.GradeLevel ?? "ALL", 2);

            // 7. Tạo mã QR Code base64 của học sinh
            string qrBase64 = GenerateQrBase64(student.StudentCode);

            var result = new StudentLookupResultViewModel
            {
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                ChristianName = student.ChristianName,
                FullName = $"{student.FirstName} {student.LastName}".Trim(),
                Gender = student.Gender ?? "Nam",
                DateOfBirth = student.DateOfBirth,
                ParentPhone = student.ParentPhone,

                ClassName = enrollment?.ClassRoom?.Name ?? "Chưa xếp lớp",
                GradeLevel = enrollment?.ClassRoom?.GradeLevel ?? "Chưa có",
                AcademicYearName = currentYear?.Name ?? "",
                RoomNumber = enrollment?.ClassRoom?.RoomName ?? "Chưa xếp phòng",
                Teachers = enrollment?.ClassRoom?.ClassTeachers?.Select(t => t.TeacherName).ToList() ?? new List<string>(),

                TotalMassSessions = totalMass,
                AttendedMassCount = attendedMass,
                MassAttendanceRate = massRate,

                TotalClassSessions = totalClass,
                AttendedClassCount = attendedClass,
                ClassAttendanceRate = classRate,

                HK1 = hk1Detail,
                HK2 = hk2Detail,
                YearAverage = yearAverage,
                OverallRank = overallRank,
                IsEligibleForSacrament = isEligible,
                QrCodeBase64 = qrBase64
            };

            return View("Result", result);
        }

        private string GenerateQrBase64(string studentCode)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(studentCode, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeBytes = qrCode.GetGraphic(20);
                return $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}