using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting; // SEC-07 FIX: Thêm RateLimiting
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QRCoder;

namespace ManagerStudentCaltholic.Controllers
{
    [AllowAnonymous]
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
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)] // SEC-07 FIX: Giới hạn tối đa 10 request/phút/IP cho cả tra cứu đúng và sai
        public async Task<IActionResult> Search([FromForm] StudentLookupRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var cleanCode = model.StudentCode.Trim().ToUpperInvariant();
            var cleanLast4 = model.PhoneLast4Digits.Trim();
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";

            // 1. RATE LIMITING KÉP: Khóa theo IP VÀ khóa theo từng Mã Thiếu Nhi
            var ipRateKey = $"Lookup_Failed_IP_{clientIp}";
            var studentRateKey = $"Lookup_Failed_Code_{cleanCode}";

            if ((_cache.TryGetValue(ipRateKey, out int ipFailed) && ipFailed >= 5) ||
                (_cache.TryGetValue(studentRateKey, out int codeFailed) && codeFailed >= 5))
            {
                ModelState.AddModelError(string.Empty, "Hồ sơ hoặc thiết bị này đã nhập sai quá 5 lần liên tiếp. Vui lòng thử lại sau 15 phút!");
                return View("Index", model);
            }

            var startDob = DateTime.SpecifyKind(model.DateOfBirth.Date, DateTimeKind.Utc);
            var endDob = startDob.AddDays(1);

            // 2. Tìm kiếm học sinh khớp Mã HS và Ngày Sinh
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentCode.ToUpper() == cleanCode &&
                                          s.DateOfBirth >= startDob && s.DateOfBirth < endDob &&
                                          s.IsActive);

            // 3. Kiểm tra khớp 4 số cuối SĐT Phụ huynh
            bool isPhoneValid = false;
            if (student != null && !string.IsNullOrWhiteSpace(student.ParentPhone))
            {
                var digitsOnly = new string(student.ParentPhone.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length >= 4 && digitsOnly.EndsWith(cleanLast4))
                {
                    isPhoneValid = true;
                }
            }

            if (student == null || !isPhoneValid)
            {
                // Tăng bộ đếm lỗi trên cả IP và Mã thiếu nhi
                _cache.Set(ipRateKey, ipFailed + 1, TimeSpan.FromMinutes(15));
                _cache.Set(studentRateKey, codeFailed + 1, TimeSpan.FromMinutes(30));

                _logger.LogWarning("Tra cứu thất bại cho mã {Code} từ IP {IP}", cleanCode, clientIp);
                ModelState.AddModelError(string.Empty, "Thông tin Mã Thiếu Nhi, Ngày Sinh hoặc 4 số cuối SĐT Phụ Huynh không chính xác!");
                return View("Index", model);
            }

            // Xóa bộ đếm lỗi khi xác thực thành công
            _cache.Remove(ipRateKey);
            _cache.Remove(studentRateKey);

            // 3. Lấy niên khóa hiện hành
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent)
                           ?? await _context.AcademicYears.OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();

            // 4. Lấy lớp học hiện tại của học sinh
            var enrollment = await _context.Enrollments
                .Include(e => e.ClassRoom)
                    .ThenInclude(c => c.ClassTeachers)
                .Include(e => e.GradeRecords)
                    .ThenInclude(g => g.GradeConfiguration)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.StudentId == student.Id &&
                                          (currentYear == null || e.ClassRoom.AcademicYearId == currentYear.Id));

            // 5. Thống kê số buổi Đi Lễ Chúa Nhật, Lễ Thứ Năm & Học Giáo Lý (Bỏ tính %)
            int totalSunMass = 0, attendedSunMass = 0, sunLinedUp = 0, sunPresent = 0, sunLate = 0, sunAbsent = 0;
            int totalThuMass = 0, attendedThuMass = 0, thuAbsent = 0;
            int totalClass = 0, attendedClass = 0, classPresent = 0, classLate = 0, classAbsent = 0;

            if (enrollment != null)
            {
                var attendances = await _context.Attendances
                    .Where(a => a.EnrollmentId == enrollment.Id)
                    .AsNoTracking()
                    .ToListAsync();

                // 5.1. Thánh Lễ Chúa Nhật
                var sunRecords = attendances.Where(a => a.DayOfWeek == DayOfWeek.Sunday).ToList();
                totalSunMass = sunRecords.Count;
                attendedSunMass = sunRecords.Count(a => a.AttendedMass);
                sunLinedUp = sunRecords.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.LinedUp);
                sunLate = sunRecords.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late);
                // Đảm bảo mọi buổi có AttendedMass = true (ngoài XH và Trễ) đều được tính vào Có mặt
                sunPresent = Math.Max(0, attendedSunMass - sunLinedUp - sunLate);
                sunAbsent = Math.Max(0, totalSunMass - attendedSunMass);

                // 5.2. Thánh Lễ Thứ Năm
                var thuRecords = attendances.Where(a => a.DayOfWeek == DayOfWeek.Thursday).ToList();
                totalThuMass = thuRecords.Count;
                attendedThuMass = thuRecords.Count(a => a.AttendedMass);
                thuAbsent = Math.Max(0, totalThuMass - attendedThuMass);

                // 5.3. Giờ Học Giáo Lý (Chúa Nhật)
                totalClass = sunRecords.Count;
                attendedClass = sunRecords.Count(a => a.ClassAttended);
                classLate = sunRecords.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Late);
                classPresent = Math.Max(0, attendedClass - classLate);
                classAbsent = Math.Max(0, totalClass - attendedClass);
            }

            // Tính tỷ lệ % (Đi trễ tính hệ số 0.8 đồng bộ với AttendanceController.Statistics)
            var sunMassRate = totalSunMass > 0
                ? Math.Min(100.0, Math.Round(((sunLinedUp + sunPresent + sunLate * 0.8) / (double)totalSunMass) * 100, 1))
                : 100.0;

            var thuMassRate = totalThuMass > 0
                ? Math.Min(100.0, Math.Round((attendedThuMass / (double)totalThuMass) * 100, 1))
                : 0.0;

            var classPresentOnTime = Math.Max(0, attendedClass - classLate);
            var classRate = totalClass > 0
                ? Math.Min(100.0, Math.Round(((classPresentOnTime + classLate * 0.8) / (double)totalClass) * 100, 1))
                : 100.0;

            var combinedAttendanceRate = Math.Round((sunMassRate + classRate) / 2.0, 1);

            // 6. Xử lý Bảng điểm HK1 & HK2
            var hk1Detail = new SemesterGradeDetail { Semester = 1 };
            var hk2Detail = new SemesterGradeDetail { Semester = 2 };

            if (enrollment != null && currentYear != null)
            {
                var gradeConfigs = await _context.GradeConfigurations
                    .Where(g => g.AcademicYearId == currentYear.Id &&
                                (g.GradeLevel == "ALL" || g.GradeLevel == enrollment.ClassRoom.GradeLevel) &&
                                (g.ClassRoomId == null || g.ClassRoomId == enrollment.ClassRoomId))
                    .OrderBy(g => g.ColumnIndex)
                    .AsNoTracking()
                    .ToListAsync();

                // Điểm HK1
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

                // Điểm HK2
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
            var (overallRank, _) = _gradeService.EvaluateStudent(yearAverage, combinedAttendanceRate, enrollment?.ClassRoom?.GradeLevel ?? "ALL", 0);

            string qrBase64 = GenerateQrBase64(student.StudentCode);

            var result = new StudentLookupResultViewModel
            {
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                ChristianName = student.ChristianName,
                FullName = $"{student.FirstName} {student.LastName}".Trim(),
                Gender = student.Gender ?? "Nam",
                DateOfBirth = student.DateOfBirth,

                MaskedParentPhone = MaskPhoneNumber(student.ParentPhone),

                ClassName = enrollment?.ClassRoom?.Name ?? "Chưa xếp lớp",
                GradeLevel = enrollment?.ClassRoom?.GradeLevel ?? "Chưa có",
                AcademicYearName = currentYear?.Name ?? "",
                RoomNumber = enrollment?.ClassRoom?.RoomName ?? "Chưa xếp phòng",
                Teachers = enrollment?.ClassRoom?.ClassTeachers?.Select(t => t.TeacherName).ToList() ?? new List<string>(),

                // Lễ Chúa Nhật
                TotalSunMassSessions = totalSunMass,
                AttendedSunMassCount = attendedSunMass,
                SunMassLinedUpCount = sunLinedUp,
                SunMassPresentCount = sunPresent,
                SunMassLateCount = sunLate,
                SunMassAbsentCount = sunAbsent,
                SunMassAttendanceRate = sunMassRate,

                // Lễ Thứ Năm
                TotalThuMassSessions = totalThuMass,
                AttendedThuMassCount = attendedThuMass,
                ThuMassAbsentCount = thuAbsent,
                ThuMassAttendanceRate = thuMassRate,

                // Học Giáo Lý
                TotalClassSessions = totalClass,
                AttendedClassCount = attendedClass,
                ClassLateCount = classLate,
                ClassAbsentCount = classAbsent,
                ClassAttendanceRate = classRate,

                // Tổng hợp chung
                TotalMassSessions = totalSunMass + totalThuMass,
                AttendedMassCount = attendedSunMass + attendedThuMass,
                MassAttendanceRate = sunMassRate,

                HK1 = hk1Detail,
                HK2 = hk2Detail,
                YearAverage = yearAverage,
                OverallRank = overallRank,
                IsEligibleForSacrament = false, // Tạm để false theo yêu cầu
                QrCodeBase64 = qrBase64
            };

            return View("Result", result);
        }

        private static string GenerateQrBase64(string studentCode)
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

        private static string MaskPhoneNumber(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "Chưa cập nhật";
            var clean = phone.Trim();
            if (clean.Length <= 6) return "******";
            return $"{clean[..3]}****{clean[^3..]}"; // Ví dụ: 091****678
        }
    }
}