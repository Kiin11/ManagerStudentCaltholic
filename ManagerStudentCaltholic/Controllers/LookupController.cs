using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Extensions;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly IStudentCodeRolloverService _codeRolloverService;
        private readonly IAttendanceDisputeService _disputeService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LookupController> _logger;
        private readonly IDeviceStudentRecognitionService _deviceRecognitionService;

        public LookupController(
            ParishDbContext context,
            IGradeCalculationService gradeService,
            IStudentCodeRolloverService codeRolloverService,
            IAttendanceDisputeService disputeService,
            IMemoryCache cache,
            IDeviceStudentRecognitionService deviceRecognitionService,
            ILogger<LookupController> logger)
        {
            _context = context;
            _gradeService = gradeService;
            _codeRolloverService = codeRolloverService;
            _deviceRecognitionService = deviceRecognitionService;
            _disputeService = disputeService;
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
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
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

            // 2. Tìm học sinh theo mã hiện hành hoặc mã lịch sử năm cũ (FEAT-P4-04)
            var (resolvedStudent, isHistoricalCode, matchedOldCode) =
                await _codeRolloverService.ResolveStudentByAnyCodeAsync(cleanCode);

            Student? student = null;
            if (resolvedStudent != null &&
                resolvedStudent.DateOfBirth >= startDob &&
                resolvedStudent.DateOfBirth < endDob &&
                resolvedStudent.IsActive)
            {
                student = resolvedStudent;
            }

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
                _cache.Set(ipRateKey, ipFailed + 1, TimeSpan.FromMinutes(15));
                _cache.Set(studentRateKey, codeFailed + 1, TimeSpan.FromMinutes(30));

                _logger.LogWarning("Tra cứu thất bại cho mã {Code} từ IP {IP}", cleanCode, clientIp);
                ModelState.AddModelError(string.Empty, "Thông tin Mã Thiếu Nhi, Ngày Sinh hoặc 4 số cuối SĐT Phụ Huynh không chính xác!");
                return View("Index", model);
            }

            _cache.Remove(ipRateKey);
            _cache.Remove(studentRateKey);

            if (isHistoricalCode)
            {
                ViewBag.CodeRolloverNotice = $"Bạn vừa tra cứu bằng mã năm học cũ ({matchedOldCode}). Mã thiếu nhi hiện hành trong niên khóa mới của em là: {student.StudentCode}.";
            }

            // 4. Lấy niên khóa hiện hành & lớp học của thiếu nhi
            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent)
                           ?? await _context.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();

            var enrollment = await _context.Enrollments
                .Include(e => e.ClassRoom)
                    .ThenInclude(c => c.ClassTeachers)
                .Include(e => e.GradeRecords)
                    .ThenInclude(g => g.GradeConfiguration)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.StudentId == student.Id &&
                                          (currentYear == null || e.ClassRoom.AcademicYearId == currentYear.Id));

            if (model.RememberOnDevice)
            {
                _deviceRecognitionService.RememberStudentOnDevice(HttpContext, student, enrollment?.ClassRoom);
            }

            // 5. Thống kê số buổi Đi Lễ Chúa Nhật, Lễ Thứ Năm & Học Giáo Lý + Nhật ký từng ngày (FEAT-P4-05)
            int totalSunMass = 0, attendedSunMass = 0, sunLinedUp = 0, sunPresent = 0, sunLate = 0, sunAbsent = 0;
            int totalThuMass = 0, attendedThuMass = 0, thuAbsent = 0;
            int totalClass = 0, attendedClass = 0, classPresent = 0, classLate = 0, classAbsent = 0;

            var dayLogs = new List<AttendanceDayLogItem>();
            MassCampaignLookupSummary? activeCampaignSummary = null;
            var submittedDisputes = new List<DisputeHistoryItemDto>();

            if (enrollment != null)
            {
                var attendances = await _context.Attendances
                    .Where(a => a.EnrollmentId == enrollment.Id)
                    .OrderByDescending(a => a.AttendanceDate)
                    .AsNoTracking()
                    .ToListAsync();

                var disputes = await _context.AttendanceDisputeRequests
                    .Where(d => d.EnrollmentId == enrollment.Id)
                    .OrderByDescending(d => d.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync();

                submittedDisputes = disputes.Select(d => new DisputeHistoryItemDto
                {
                    Id = d.Id,
                    AttendanceDate = d.AttendanceDate,
                    SessionScope = d.SessionScope,
                    SessionScopeLabel = AttendanceDisputeService.GetScopeDisplayLabel(d.SessionScope),
                    CurrentRecordedStatus = d.CurrentRecordedStatus,
                    RequestedStatus = d.RequestedStatus,
                    Reason = d.Reason,
                    Status = d.Status,
                    TeacherResponseNote = d.TeacherResponseNote,
                    ReviewedBy = d.ReviewedBy,
                    CreatedAt = d.CreatedAt,
                    ReviewedAt = d.ReviewedAt
                }).ToList();

                // 5.1. Thánh Lễ Chúa Nhật
                var sunRecords = attendances.Where(a => a.DayOfWeek == DayOfWeek.Sunday).ToList();
                totalSunMass = sunRecords.Count;
                attendedSunMass = sunRecords.Count(a => a.AttendedMass || a.IsMakeUp);
                sunLinedUp = sunRecords.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.LinedUp);
                sunLate = sunRecords.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late);
                sunPresent = Math.Max(0, attendedSunMass - sunLinedUp - sunLate);
                sunAbsent = Math.Max(0, totalSunMass - attendedSunMass);

                // 5.2. Thánh Lễ Thứ Năm
                var thuRecords = attendances.Where(a => a.DayOfWeek == DayOfWeek.Thursday).ToList();
                totalThuMass = thuRecords.Count;
                attendedThuMass = thuRecords.Count(a => a.AttendedMass || a.IsMakeUp);
                thuAbsent = Math.Max(0, totalThuMass - attendedThuMass);

                // 5.3. Giờ Học Giáo Lý (Chúa Nhật)
                totalClass = sunRecords.Count;
                attendedClass = sunRecords.Count(a => a.ClassAttended || a.IsMakeUp);
                classLate = sunRecords.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Late);
                classPresent = Math.Max(0, attendedClass - classLate);
                classAbsent = Math.Max(0, totalClass - attendedClass);

                // 5.4. Xây dựng Nhật ký chi tiết từng ngày sinh hoạt
                foreach (var att in attendances)
                {
                    var dateOnly = att.AttendanceDate.Date;
                    var massDispute = disputes.FirstOrDefault(d =>
                        d.AttendanceDate.Date == dateOnly &&
                        (d.SessionScope == "REGULAR_MASS_SUN" || d.SessionScope == "REGULAR_MASS_THU"));

                    var classDispute = disputes.FirstOrDefault(d =>
                        d.AttendanceDate.Date == dateOnly &&
                        d.SessionScope == "REGULAR_CLASS");

                    dayLogs.Add(new AttendanceDayLogItem
                    {
                        AttendanceId = att.Id,
                        Date = dateOnly,
                        DayLabel = att.DayOfWeek == DayOfWeek.Sunday
                            ? "Chúa Nhật"
                            : (att.DayOfWeek == DayOfWeek.Thursday ? "Thứ Năm" : GetVnDayShort(att.DayOfWeek)),
                        IsCampaignDay = false,
                        AttendedMass = att.AttendedMass || att.IsMakeUp,
                        MassStatus = att.IsMakeUp ? "MAKE_UP" : att.MassStatus,
                        MassCheckInTime = att.MassCheckInTime.HasValue ? att.MassCheckInTime.Value.ToString(@"hh\:mm") : null,
                        HasClassSession = att.DayOfWeek == DayOfWeek.Sunday,
                        AttendedClass = att.ClassAttended || att.IsMakeUp,
                        ClassStatus = att.IsMakeUp ? "MAKE_UP" : att.ClassStatus,
                        ClassCheckInTime = att.ClassCheckInTime.HasValue ? att.ClassCheckInTime.Value.ToString(@"hh\:mm") : null,
                        IsMakeUp = att.IsMakeUp,
                        Note = att.Note,
                        MassCorrectionStatus = massDispute?.Status,
                        ClassCorrectionStatus = classDispute?.Status,
                        TeacherNote = massDispute?.TeacherResponseNote ?? classDispute?.TeacherResponseNote
                    });
                }

                // 5.5. Nạp Lịch Chiến dịch đi lễ 1 tháng (nếu lớp có chiến dịch)
                if (currentYear != null)
                {
                    var activeCampaign = await _context.MassCampaigns
                        .AsNoTracking()
                        .Where(mc => mc.ClassRoomId == enrollment.ClassRoomId && mc.AcademicYearId == currentYear.Id)
                        .OrderByDescending(mc => mc.Status == "ACTIVE")
                        .ThenByDescending(mc => mc.StartDate)
                        .FirstOrDefaultAsync();

                    if (activeCampaign != null)
                    {
                        var campRecords = await _context.MassCampaignRecords
                            .AsNoTracking()
                            .Where(r => r.CampaignId == activeCampaign.Id && r.EnrollmentId == enrollment.Id)
                            .ToDictionaryAsync(r => r.AttendanceDate.Date.ToString("yyyy-MM-dd"));

                        var allowedDays = activeCampaign.ApplicableDaysCsv
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => int.TryParse(s.Trim(), out int d) ? d : -1)
                            .Where(d => d >= 0 && d <= 6)
                            .ToHashSet();

                        if (!allowedDays.Any())
                        {
                            allowedDays = new HashSet<int> { 0, 1, 2, 3, 4, 5, 6 };
                        }

                        activeCampaignSummary = new MassCampaignLookupSummary
                        {
                            CampaignId = activeCampaign.Id,
                            CampaignName = activeCampaign.Title,
                            Description = activeCampaign.Description,
                            StartDate = activeCampaign.StartDate,
                            EndDate = activeCampaign.EndDate,
                            TargetDaysCount = activeCampaign.TargetDaysCount
                        };

                        var todayVn = DateTime.UtcNow.AddHours(7).Date;
                        int runningStreak = 0, maxStreak = 0, currentStreak = 0;

                        for (var dt = activeCampaign.StartDate.Date; dt <= activeCampaign.EndDate.Date; dt = dt.AddDays(1))
                        {
                            if (!allowedDays.Contains((int)dt.DayOfWeek))
                                continue;

                            var key = dt.ToString("yyyy-MM-dd");
                            campRecords.TryGetValue(key, out var cRec);
                            var campDispute = disputes.FirstOrDefault(d =>
                                d.AttendanceDate.Date == dt && d.SessionScope == "CAMPAIGN_MASS");

                            string st = cRec?.Status ?? (dt > todayVn ? "FUTURE" : "ABSENT");
                            bool attended = st == "PRESENT" || st == "LATE";

                            if (st == "PRESENT") { activeCampaignSummary.AttendedDays++; runningStreak++; }
                            else if (st == "LATE") { activeCampaignSummary.AttendedDays++; activeCampaignSummary.LateDays++; runningStreak++; }
                            else if (st == "EXCUSED") { activeCampaignSummary.ExcusedDays++; }
                            else if (dt <= todayVn) { activeCampaignSummary.AbsentDays++; runningStreak = 0; }

                            if (runningStreak > maxStreak) maxStreak = runningStreak;
                            if (dt <= todayVn) currentStreak = runningStreak;

                            activeCampaignSummary.CampaignDays.Add(new AttendanceDayLogItem
                            {
                                AttendanceId = cRec?.Id,
                                Date = dt,
                                DayLabel = $"{GetVnDayShort(dt.DayOfWeek)}",
                                IsCampaignDay = true,
                                CampaignId = activeCampaign.Id,
                                AttendedMass = attended,
                                MassStatus = st,
                                MassCheckInTime = cRec?.CheckInTime?.AddHours(7).ToString("HH:mm"),
                                HasClassSession = false,
                                Note = cRec?.Note,
                                MassCorrectionStatus = campDispute?.Status,
                                TeacherNote = campDispute?.TeacherResponseNote
                            });
                        }

                        activeCampaignSummary.TotalDays = activeCampaignSummary.CampaignDays.Count;
                        activeCampaignSummary.CurrentStreak = currentStreak;
                        activeCampaignSummary.MaxStreak = maxStreak;
                        activeCampaignSummary.CompletionPercentage = activeCampaign.TargetDaysCount > 0
                            ? Math.Min(100.0, Math.Round(activeCampaignSummary.AttendedDays * 100.0 / activeCampaign.TargetDaysCount, 1))
                            : 0.0;
                    }
                }
            }

            var sunMassRate = totalSunMass > 0
                ? Math.Min(100.0, Math.Round(((sunLinedUp + sunPresent + sunLate * 0.8) / (double)totalSunMass) * 100, 1))
                : 100.0;

            var thuMassRate = totalThuMass > 0
                ? Math.Min(100.0, Math.Round((attendedThuMass / (double)totalThuMass) * 100, 1))
                : 0.0;

            var classRate = totalClass > 0
                ? Math.Min(100.0, Math.Round(((classPresent + classLate * 0.8) / (double)totalClass) * 100, 1))
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
            var (overallRank, isSacramentEligible) = _gradeService.EvaluateStudent(yearAverage, combinedAttendanceRate, enrollment?.ClassRoom?.GradeLevel ?? "ALL", 0);

            var result = new StudentLookupResultViewModel
            {
                StudentId = student.Id,
                EnrollmentId = enrollment?.Id ?? 0,
                ClassRoomId = enrollment?.ClassRoomId ?? 0,
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
                ClassPresentCount = classPresent,
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
                IsEligibleForSacrament = isSacramentEligible,
                QrCodeBase64 = GenerateQrBase64(student.StudentCode),

                // FEAT-P4-05
                ActiveCampaign = activeCampaignSummary,
                AttendanceHistoryLogs = dayLogs,
                SubmittedDisputes = submittedDisputes
            };

            return View("Result", result);
        }

        // POST: /TraCuu/SubmitDispute (FEAT-P4-05)
        [HttpPost("SubmitDispute")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> SubmitDispute([FromBody] SubmitAttendanceDisputeRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                var firstErr = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                               ?? "Vui lòng điền đầy đủ thông tin và 4 số cuối SĐT Phụ huynh.";
                return BadRequest(new { success = false, message = firstErr });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var (success, message) = await _disputeService.SubmitDisputeFromParentAsync(dto, ip);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }

        [HttpGet("QuickView")]
        [EnableRateLimiting(SecurityExtensions.PolicyPublicLookup)]
        public async Task<IActionResult> QuickView(string studentCode)
        {
            var remembered = _deviceRecognitionService.TryGetRememberedStudent(HttpContext, studentCode);
            if (remembered == null)
            {
                TempData["ErrorMessage"] = "Phiên ghi nhớ trên thiết bị đã hết hạn hoặc đã được gỡ. Vui lòng nhập thông tin để tra cứu lại.";
                return RedirectToAction(nameof(Index));
            }

            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == remembered.StudentId && s.IsActive);

            if (student == null || string.IsNullOrWhiteSpace(student.ParentPhone))
            {
                return RedirectToAction(nameof(Index));
            }

            var digits = new string(student.ParentPhone.Where(char.IsDigit).ToArray());
            if (digits.Length < 4)
            {
                return RedirectToAction(nameof(Index));
            }

            return await Search(new StudentLookupRequestDto
            {
                StudentCode = student.StudentCode,
                DateOfBirth = student.DateOfBirth,
                PhoneLast4Digits = digits[^4..],
                RememberOnDevice = true
            });
        }

        private static string GetVnDayShort(DayOfWeek dow) => dow switch
        {
            DayOfWeek.Sunday => "CN",
            DayOfWeek.Monday => "T2",
            DayOfWeek.Tuesday => "T3",
            DayOfWeek.Wednesday => "T4",
            DayOfWeek.Thursday => "T5",
            DayOfWeek.Friday => "T6",
            DayOfWeek.Saturday => "T7",
            _ => ""
        };

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
            return $"{clean[..3]}****{clean[^3..]}";
        }
    }
}