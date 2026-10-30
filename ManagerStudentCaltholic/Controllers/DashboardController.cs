using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
    public class DashboardController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        private static readonly string[] StandardGrades = { "Khai Tâm", "Rước Lễ", "Thêm Sức", "Bao Đồng" };

        public DashboardController(ParishDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        /// <summary>
        /// 1. GET: /Dashboard/Index (Màn hình Executive Dashboard cho Cha Xứ & BĐH - FEAT-P3-08)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? academicYearId, string? gradeLevel, int? month)
        {
            // Kiểm tra phạm vi nếu là Trưởng khối (BranchHead - Fail-Closed)
            string effectiveGrade = string.IsNullOrWhiteSpace(gradeLevel) ? "ALL" : gradeLevel.Trim();
            bool isGradeLocked = false;

            if (User.IsInRole(UserRole.BranchHead) && !IsLeadership)
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    return Forbid();
                }
                effectiveGrade = managedGrade;
                isGradeLocked = true;
            }

            var vm = await BuildExecutiveDashboardDataAsync(academicYearId, effectiveGrade, month);
            vm.IsGradeLocked = isGradeLocked;

            return View(vm);
        }

        /// <summary>
        /// 2. GET: /Dashboard/GetAnalyticsData (API trả về JSON cho biểu đồ & Heatmap khi lọc bằng Ajax - TASK-P3-08.1)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAnalyticsData(int? academicYearId, string? gradeLevel, int? month)
        {
            try
            {
                string effectiveGrade = string.IsNullOrWhiteSpace(gradeLevel) ? "ALL" : gradeLevel.Trim();
                if (User.IsInRole(UserRole.BranchHead) && !IsLeadership)
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    if (string.IsNullOrWhiteSpace(managedGrade))
                    {
                        return Forbid();
                    }
                    effectiveGrade = managedGrade;
                }

                var data = await BuildExecutiveDashboardDataAsync(academicYearId, effectiveGrade, month);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải dữ liệu phân tích Executive Dashboard");
                return StatusCode(500, new { success = false, message = "Không thể tải dữ liệu thống kê lúc này." });
            }
        }

        private async Task<ExecutiveDashboardViewModel> BuildExecutiveDashboardDataAsync(
            int? academicYearId,
            string selectedGrade,
            int? selectedMonth)
        {
            var academicYears = await _context.AcademicYears
                .OrderByDescending(y => y.StartDate)
                .AsNoTracking()
                .ToListAsync();

            var targetYear = academicYears.FirstOrDefault(y => y.Id == academicYearId)
                             ?? academicYears.FirstOrDefault(y => y.IsCurrent)
                             ?? academicYears.FirstOrDefault();

            int yearId = targetYear?.Id ?? 0;

            var vm = new ExecutiveDashboardViewModel
            {
                SelectedYearId = yearId,
                SelectedYearName = targetYear?.Name ?? "Chưa thiết lập",
                SelectedGrade = selectedGrade,
                SelectedMonth = selectedMonth,
                AcademicYears = academicYears
            };

            if (yearId == 0) return vm;

            // 1. Lấy danh sách lớp và học sinh đã xếp lớp trong niên khóa
            var classesQuery = _context.Classes.Where(c => c.AcademicYearId == yearId);
            if (selectedGrade != "ALL")
            {
                classesQuery = classesQuery.Where(c => c.GradeLevel == selectedGrade);
            }

            var classes = await classesQuery
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            var classIds = classes.Select(c => c.Id).ToList();

            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.ClassRoom)
                .Where(e => classIds.Contains(e.ClassRoomId) && e.Student.IsActive)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            // 2. Lấy toàn bộ bản ghi điểm danh của phạm vi đang chọn
            var attendancesQuery = _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId));

            if (selectedMonth.HasValue && selectedMonth.Value >= 1 && selectedMonth.Value <= 12)
            {
                attendancesQuery = attendancesQuery.Where(a => a.AttendanceDate.Month == selectedMonth.Value);
            }

            var allAttendances = await attendancesQuery
                .AsNoTracking()
                .ToListAsync();

            // 3. Thống kê các chỉ số vận hành toàn xứ
            vm.TotalActiveStudents = await _context.Students.CountAsync(s => s.IsActive);
            vm.TotalEnrolledStudents = enrollments.Count;
            vm.TotalClasses = classes.Count;
            vm.TotalActiveTeachers = await _context.ClassTeachers
                .Where(ct => ct.AcademicYearId == yearId && ct.UserId.HasValue)
                .Select(ct => ct.UserId!.Value)
                .Distinct()
                .CountAsync();

            vm.PendingRegistrationsCount = await _context.StudentRegistrationRequests
                .CountAsync(r => r.Status == "PENDING");

            vm.PendingAbsenceRequestsCount = await _context.AbsenceRequests
                .CountAsync(r => r.Status == "PENDING" && classIds.Contains(r.Enrollment.ClassRoomId));

            vm.PendingRoomIncidentsCount = await _context.RoomIncidentReports
                .CountAsync(r => r.Status != "RESOLVED" && r.Status != "CANCELLED");

            // 4. Cơ cấu trạng thái điểm danh (Doughnut Chart)
            vm.TotalLinedUpCount = allAttendances.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.LinedUp);
            vm.TotalPresentOnTimeCount = allAttendances.Count(a =>
                (a.AttendedMass && a.MassStatus == AttendanceStatus.Present) ||
                (a.ClassAttended && a.ClassStatus == AttendanceStatus.Present));
            vm.TotalLateCount = allAttendances.Count(a =>
                (a.AttendedMass && a.MassStatus == AttendanceStatus.Late) ||
                (a.ClassAttended && a.ClassStatus == AttendanceStatus.Late));
            vm.TotalPermittedAbsentCount = allAttendances.Count(a =>
                a.MassStatus == AttendanceStatus.AbsentPermitted ||
                (a.DayOfWeek == DayOfWeek.Sunday && a.ClassStatus == AttendanceStatus.AbsentPermitted));
            vm.TotalUnpermittedAbsentCount = allAttendances.Count(a =>
                a.MassStatus == AttendanceStatus.AbsentUnpermitted ||
                (a.DayOfWeek == DayOfWeek.Sunday && a.ClassStatus == AttendanceStatus.AbsentUnpermitted));

            // 5. Tính tỷ lệ chuyên cần theo từng Khối (Grouped Bar Chart)
            var gradesToAnalyze = selectedGrade == "ALL"
                ? StandardGrades
                : new[] { selectedGrade };

            foreach (var gName in gradesToAnalyze)
            {
                var gradeClasses = classes.Where(c => c.GradeLevel == gName).ToList();
                var gradeClassIds = gradeClasses.Select(c => c.Id).ToHashSet();
                var gradeEnrollments = enrollments.Where(e => gradeClassIds.Contains(e.ClassRoomId)).ToList();
                var gradeEnrollmentIds = gradeEnrollments.Select(e => e.Id).ToHashSet();
                var gradeAtts = allAttendances.Where(a => gradeEnrollmentIds.Contains(a.EnrollmentId)).ToList();

                var (sunRate, thuRate, clsRate, combinedRate) = CalculateAttendanceRates(gradeAtts);
                int unpermittedCount = gradeAtts.Count(a =>
                    a.MassStatus == AttendanceStatus.AbsentUnpermitted ||
                    (a.DayOfWeek == DayOfWeek.Sunday && a.ClassStatus == AttendanceStatus.AbsentUnpermitted));

                vm.GradeBreakdowns.Add(new GradeAttendanceBreakdownDto
                {
                    GradeLevel = gName,
                    ClassCount = gradeClasses.Count,
                    StudentCount = gradeEnrollments.Count,
                    SundayMassRate = sunRate,
                    ThursdayMassRate = thuRate,
                    ClassAttendanceRate = clsRate,
                    CombinedRate = combinedRate,
                    AbsentUnpermittedTotal = unpermittedCount
                });
            }

            // Tính tỷ lệ tổng hợp toàn xứ / khối
            var (overallSun, overallThu, overallCls, overallComb) = CalculateAttendanceRates(allAttendances);
            vm.OverallSunMassRate = overallSun;
            vm.OverallThuMassRate = overallThu;
            vm.OverallClassAttendanceRate = overallCls;
            vm.CombinedParishAttendanceRate = overallComb;

            // 6. Tính toán chuỗi xu hướng qua các tháng trong niên khóa (Line Chart & Monthly Trend Delta)
            // Luôn lấy dữ liệu xuyên suốt các tháng của niên khóa để vẽ biểu đồ đường
            var yearAttendancesForTrend = selectedMonth.HasValue
                ? await _context.Attendances
                    .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                    .AsNoTracking()
                    .ToListAsync()
                : allAttendances;

            var monthlyGroups = yearAttendancesForTrend
                .GroupBy(a => new { a.AttendanceDate.Year, a.AttendanceDate.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .ToList();

            foreach (var mg in monthlyGroups)
            {
                var monthList = mg.ToList();
                var (mSun, mThu, mCls, _) = CalculateAttendanceRates(monthList);
                int distinctDays = monthList.Select(a => a.AttendanceDate.Date).Distinct().Count();
                int absences = monthList.Count(a => !a.AttendedMass || (a.DayOfWeek == DayOfWeek.Sunday && !a.ClassAttended));

                vm.MonthlyTrends.Add(new MonthlyAttendanceTrendPointDto
                {
                    Year = mg.Key.Year,
                    Month = mg.Key.Month,
                    TotalSessions = distinctDays,
                    SundayMassRate = mSun,
                    ThursdayMassRate = mThu,
                    ClassAttendanceRate = mCls,
                    TotalAbsencesCount = absences
                });
            }

            if (vm.MonthlyTrends.Count >= 2)
            {
                var latest = vm.MonthlyTrends[^1];
                var prev = vm.MonthlyTrends[^2];
                double latestAvg = (latest.SundayMassRate + latest.ClassAttendanceRate) / 2.0;
                double prevAvg = (prev.SundayMassRate + prev.ClassAttendanceRate) / 2.0;
                vm.MonthlyTrendDelta = Math.Round(latestAvg - prevAvg, 1);
                vm.CurrentMonthLabel = latest.Label;
                vm.PreviousMonthLabel = prev.Label;
            }
            else if (vm.MonthlyTrends.Count == 1)
            {
                vm.CurrentMonthLabel = vm.MonthlyTrends[0].Label;
                vm.PreviousMonthLabel = "Tháng đầu niên khóa";
            }

            // 7. Xây dựng Bản đồ nhiệt (Heatmap) Vắng Lễ & Vắng Học theo từng ngày T5 & CN (TASK-P3-08.3)
            var sessionDateGroups = allAttendances
                .GroupBy(a => a.AttendanceDate.Date)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var dayGroup in sessionDateGroups)
            {
                var dayRecords = dayGroup.ToList();
                var dt = dayGroup.Key;
                bool isSun = dt.DayOfWeek == DayOfWeek.Sunday;
                int totalRecords = Math.Max(1, dayRecords.Count);

                int present = dayRecords.Count(a => a.AttendedMass &&
                    (a.MassStatus == AttendanceStatus.Present || a.MassStatus == AttendanceStatus.LinedUp));
                int late = dayRecords.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late);
                int permAbs = dayRecords.Count(a => !a.AttendedMass && a.MassStatus == AttendanceStatus.AbsentPermitted);
                int unpermAbs = dayRecords.Count(a => !a.AttendedMass && a.MassStatus != AttendanceStatus.AbsentPermitted);

                int totalAbs = permAbs + unpermAbs;
                double absRate = Math.Round((totalAbs / (double)totalRecords) * 100.0, 1);
                double attRate = Math.Round(100.0 - absRate, 1);

                int heatLevel = absRate switch
                {
                    < 5.0 => 1,   // Xanh lá đậm: Rất sốt sắng (Vắng < 5%)
                    < 12.0 => 2,  // Xanh nhạt: Bình thường (Vắng 5% - 12%)
                    < 22.0 => 3,  // Vàng cam: Cảnh báo vắng (Vắng 12% - 22%)
                    _ => 4        // Đỏ đậm: Cao điểm vắng lễ (Vắng >= 22%)
                };

                vm.HeatmapCells.Add(new AttendanceHeatmapCellDto
                {
                    Date = dt,
                    DayOfWeek = dt.DayOfWeek,
                    TotalExpectedStudents = totalRecords,
                    PresentCount = present,
                    LateCount = late,
                    PermittedAbsentCount = permAbs,
                    UnpermittedAbsentCount = unpermAbs,
                    AbsenceRatePercent = absRate,
                    AttendanceRatePercent = attRate,
                    HeatLevel = heatLevel
                });
            }

            // 8. Bảng xếp hạng các lớp chuyên cần & tính số lượng đủ điều kiện Bí tích (>= 80% chuyên cần)
            var attByEnrollment = allAttendances
                .GroupBy(a => a.EnrollmentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var cls in classes)
            {
                var clsEnrollments = enrollments.Where(e => e.ClassRoomId == cls.Id).ToList();
                var clsEnrIds = clsEnrollments.Select(e => e.Id).ToHashSet();
                var clsAtts = allAttendances.Where(a => clsEnrIds.Contains(a.EnrollmentId)).ToList();

                var (cSun, _, cClass, cComb) = CalculateAttendanceRates(clsAtts);

                vm.TopPerformingClasses.Add(new ClassRankingSummaryDto
                {
                    ClassId = cls.Id,
                    ClassName = cls.Name,
                    GradeLevel = cls.GradeLevel,
                    StudentCount = clsEnrollments.Count,
                    MassRate = cSun,
                    ClassRate = cClass,
                    CombinedRate = cComb
                });
            }

            vm.TopPerformingClasses = vm.TopPerformingClasses
                .OrderByDescending(c => c.CombinedRate)
                .ThenByDescending(c => c.StudentCount)
                .Take(8)
                .ToList();

            // 9. Lọc danh sách học sinh vắng nhiều cần cảnh báo sớm & đếm số em đạt chuyên cần Bí tích >= 80%
            var atRiskList = new List<AtRiskStudentAlertDto>();
            int eligibleCount = 0;

            int totalSunSessionsInScope = Math.Max(1, allAttendances
                .Where(a => a.DayOfWeek == DayOfWeek.Sunday)
                .Select(a => a.AttendanceDate.Date).Distinct().Count());

            foreach (var enr in enrollments)
            {
                if (!attByEnrollment.TryGetValue(enr.Id, out var stuAtts) || !stuAtts.Any())
                {
                    eligibleCount++;
                    continue;
                }

                var sunRecords = stuAtts.Where(a => a.DayOfWeek == DayOfWeek.Sunday).ToList();
                int sunDenom = Math.Max(1, sunRecords.Count);

                int sunPresent = sunRecords.Count(a => a.AttendedMass && (a.MassStatus == AttendanceStatus.Present || a.MassStatus == AttendanceStatus.LinedUp));
                int sunLate = sunRecords.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late);
                double stuMassRate = Math.Min(100.0, ((sunPresent + sunLate * 0.8) / sunDenom) * 100.0);

                int clsPresent = sunRecords.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Present);
                int clsLate = sunRecords.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Late);
                double stuClassRate = Math.Min(100.0, ((clsPresent + clsLate * 0.8) / sunDenom) * 100.0);

                double stuCombined = Math.Round((stuMassRate + stuClassRate) / 2.0, 1);

                if (stuMassRate >= 80.0 && stuClassRate >= 80.0)
                {
                    eligibleCount++;
                }

                int unpermAbsent = stuAtts.Count(a =>
                    (!a.AttendedMass && a.MassStatus == AttendanceStatus.AbsentUnpermitted) ||
                    (a.DayOfWeek == DayOfWeek.Sunday && !a.ClassAttended && a.ClassStatus == AttendanceStatus.AbsentUnpermitted));

                int totalAbsent = stuAtts.Count(a => !a.AttendedMass || (a.DayOfWeek == DayOfWeek.Sunday && !a.ClassAttended));

                if (unpermAbsent >= 2 || stuCombined < 70.0)
                {
                    var lastPresentDate = stuAtts
                        .Where(a => a.AttendedMass || a.ClassAttended)
                        .OrderByDescending(a => a.AttendanceDate)
                        .Select(a => (DateTime?)a.AttendanceDate)
                        .FirstOrDefault();

                    atRiskList.Add(new AtRiskStudentAlertDto
                    {
                        EnrollmentId = enr.Id,
                        StudentId = enr.StudentId,
                        StudentCode = enr.Student.StudentCode,
                        ChristianName = enr.Student.ChristianName,
                        FullName = $"{enr.Student.FirstName} {enr.Student.LastName}".Trim(),
                        ClassName = enr.ClassRoom.Name,
                        GradeLevel = enr.ClassRoom.GradeLevel,
                        ParentPhone = enr.Student.ParentPhone,
                        UnpermittedAbsentCount = unpermAbsent,
                        TotalAbsentCount = totalAbsent,
                        CombinedAttendanceRate = stuCombined,
                        LastAttendedDate = lastPresentDate
                    });
                }
            }

            vm.SacramentEligibleCount = eligibleCount;
            vm.HighAbsenceStudents = atRiskList
                .OrderByDescending(s => s.UnpermittedAbsentCount)
                .ThenBy(s => s.CombinedAttendanceRate)
                .Take(15)
                .ToList();

            return vm;
        }

        /// <summary>
        /// Tính tỷ lệ % chuyên cần Lễ Chúa Nhật, Lễ Thứ Năm, Giờ Học Giáo Lý và Trung bình kết hợp
        /// </summary>
        private static (double SunMassRate, double ThuMassRate, double ClassRate, double CombinedRate) CalculateAttendanceRates(
            List<Attendance> records)
        {
            if (records == null || !records.Any())
            {
                return (0.0, 0.0, 0.0, 0.0);
            }

            var sunRecords = records.Where(a => a.DayOfWeek == DayOfWeek.Sunday).ToList();
            var thuRecords = records.Where(a => a.DayOfWeek == DayOfWeek.Thursday).ToList();

            double sunMassRate = 0.0;
            double classRate = 0.0;

            if (sunRecords.Any())
            {
                int sunTotal = sunRecords.Count;
                int mPresent = sunRecords.Count(a => a.AttendedMass && (a.MassStatus == AttendanceStatus.Present || a.MassStatus == AttendanceStatus.LinedUp));
                int mLate = sunRecords.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late);
                sunMassRate = Math.Min(100.0, Math.Round(((mPresent + mLate * 0.8) / sunTotal) * 100.0, 1));

                int cPresent = sunRecords.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Present);
                int cLate = sunRecords.Count(a => a.ClassAttended && a.ClassStatus == AttendanceStatus.Late);
                classRate = Math.Min(100.0, Math.Round(((cPresent + cLate * 0.8) / sunTotal) * 100.0, 1));
            }

            double thuMassRate = 0.0;
            if (thuRecords.Any())
            {
                int thuTotal = thuRecords.Count;
                int thuPresent = thuRecords.Count(a => a.AttendedMass && (a.MassStatus == AttendanceStatus.Present || a.MassStatus == AttendanceStatus.LinedUp));
                int thuLate = thuRecords.Count(a => a.AttendedMass && a.MassStatus == AttendanceStatus.Late);
                thuMassRate = Math.Min(100.0, Math.Round(((thuPresent + thuLate * 0.8) / thuTotal) * 100.0, 1));
            }

            double combined = Math.Round((sunMassRate + classRate) / 2.0, 1);
            return (sunMassRate, thuMassRate, classRate, combined);
        }
    }
}