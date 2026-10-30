using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
    public class ParentMessagesController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;
        private readonly ILogger<ParentMessagesController> _logger;

        public ParentMessagesController(
            ParishDbContext context,
            IGradeCalculationService gradeService,
            ILogger<ParentMessagesController> logger)
        {
            _context = context;
            _gradeService = gradeService;
            _logger = logger;
        }

        private long CurrentUserId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        private async Task<List<ClassRoom>> GetAuthorizedClassesAsync(int currentYearId)
        {
            var query = _context.Classes.Where(c => currentYearId == 0 || c.AcademicYearId == currentYearId);

            if (IsLeadership)
            {
                return await query.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).AsNoTracking().ToListAsync();
            }

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                query = !string.IsNullOrWhiteSpace(managedGrade)
                    ? query.Where(c => c.GradeLevel == managedGrade || c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId))
                    : query.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
            }
            else
            {
                query = query.Where(c => c.ClassTeachers.Any(ct => ct.UserId == CurrentUserId));
            }

            return await query.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).AsNoTracking().ToListAsync();
        }

        private async Task<bool> CanAccessClassAsync(ClassRoom classRoom)
        {
            if (IsLeadership) return true;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrWhiteSpace(managedGrade) &&
                    string.Equals(classRoom.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return await _context.ClassTeachers.AnyAsync(ct => ct.ClassRoomId == classRoom.Id && ct.UserId == CurrentUserId);
        }

        private async Task EnsureDefaultTemplatesSeededAsync()
        {
            if (await _context.ParentMessageTemplates.AnyAsync()) return;

            var defaults = new List<ParentMessageTemplate>
            {
                new ParentMessageTemplate
                {
                    Title = "Thông báo vắng Thánh Lễ & Học Giáo Lý Chúa Nhật",
                    Category = "CHUYÊN CẦN",
                    ContentTemplate = "Kính chào Quý Phụ huynh em {TenThieuNhi} (Mã TN: {MaThieuNhi} - Lớp {TenLop}). Hôm nay ngày {NgayVang}, Ban Giáo Lý ghi nhận em vắng mặt trong buổi sinh hoạt Giáo lý mà chưa có đơn xin phép. Gia đình vui lòng phản hồi lại cho GLV ({SDTLienHe}). Xin cảm ơn Quý Phụ huynh!",
                    CreatedAt = DateTime.UtcNow
                },
                new ParentMessageTemplate
                {
                    Title = "Nhắc nhở đi trễ Thánh Lễ Chúa Nhật",
                    Category = "CHUYÊN CẦN",
                    ContentTemplate = "Kính gửi Quý Phụ huynh em {TenThieuNhi} (Lớp {TenLop}). Sáng Chúa Nhật ngày {NgayVang}, em đến tham dự Thánh Lễ trễ sau giờ quy định. Kính mong Phụ huynh nhắc nhở em đi lễ đúng giờ (trước 06h35) để giữ sốt sắng. Trân trọng!",
                    CreatedAt = DateTime.UtcNow
                },
                new ParentMessageTemplate
                {
                    Title = "Cảnh báo vắng nhiều buổi & Điều kiện xét Bí tích",
                    Category = "CẢNH BÁO BÍ TÍCH",
                    ContentTemplate = "Kính gửi Quý Phụ huynh em {TenThieuNhi} (Lớp {TenLop}). Tính đến nay em đã vắng tổng cộng {TongSoNgayVang} buổi (Tỷ lệ chuyên cần đạt {TiLeChuyenCan}%). Tình trạng xét Bí tích hiện tại: {CanhBaoBiTich}. Kính mong gia đình đôn đốc em đi Lễ và học Giáo lý đầy đủ, hoặc liên hệ {SDTLienHe} để đăng ký học bù.",
                    CreatedAt = DateTime.UtcNow
                },
                new ParentMessageTemplate
                {
                    Title = "Thông báo kết quả học tập & Điểm trung bình Giáo lý",
                    Category = "HỌC TẬP",
                    ContentTemplate = "Kính gửi Quý Phụ huynh kết quả học tập Giáo lý của em {TenThieuNhi} (Lớp {TenLop}): ĐTB Học kỳ 1: {DiemTB_Ky1} | ĐTB Học kỳ 2: {DiemTB_Ky2} | ĐTB Cả năm: {DiemTB_Nam} (Xếp loại: {XepLoai}) | Chuyên cần: {TiLeChuyenCan}%. Đánh giá Bí tích: {CanhBaoBiTich}. Trân trọng cảm ơn sự đồng hành của Gia đình!",
                    CreatedAt = DateTime.UtcNow
                },
                new ParentMessageTemplate
                {
                    Title = "Thông báo lịch kiểm tra / Khảo hạch Giáo lý",
                    Category = "HỌC TẬP",
                    ContentTemplate = "Thông báo đến Quý Phụ huynh em {TenThieuNhi} (Lớp {TenLop}): Chúa Nhật tuần tới lớp sẽ có bài kiểm tra / khảo hạch Giáo lý định kỳ. Kính nhờ Phụ huynh nhắc nhở em ôn bài đầy đủ tại nhà. Mọi thắc mắc xin liên hệ {SDTLienHe}.",
                    CreatedAt = DateTime.UtcNow
                }
            };

            _context.ParentMessageTemplates.AddRange(defaults);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 1. GET: /ParentMessages (Trung tâm Tương tác Lớp học 2 Cột & Quản lý Mẫu tin - TASK-P3-07.3)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? classId, string? category, bool attentionOnly = false)
        {
            await EnsureDefaultTemplatesSeededAsync();

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            var authorizedClasses = await GetAuthorizedClassesAsync(yearId);
            int selectedClassId = classId ?? authorizedClasses.FirstOrDefault()?.Id ?? 0;
            var targetClass = authorizedClasses.FirstOrDefault(c => c.Id == selectedClassId);

            var tplQuery = _context.ParentMessageTemplates.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(category) && category != "ALL")
            {
                tplQuery = tplQuery.Where(t => t.Category == category);
            }
            var templates = await tplQuery.OrderByDescending(t => t.CreatedAt).ToListAsync();

            var studentRows = new List<StudentInteractionRowDto>();
            if (targetClass != null)
            {
                studentRows = await BuildClassInteractionRowsAsync(targetClass, yearId);
                if (attentionOnly)
                {
                    studentRows = studentRows.Where(s => s.NeedsAttention).ToList();
                }
            }

            var vm = new ParentMessageCenterViewModel
            {
                SelectedClassId = targetClass?.Id ?? 0,
                SelectedClassName = targetClass?.Name ?? "Chưa chọn lớp",
                GradeLevel = targetClass?.GradeLevel ?? "",
                SelectedCategory = category ?? "ALL",
                FilterAttentionOnly = attentionOnly,
                CanManageTemplates = IsLeadership || User.IsInRole(UserRole.BranchHead),
                AuthorizedClasses = authorizedClasses,
                Templates = templates,
                Students = studentRows
            };

            return View(vm);
        }

        /// <summary>
        /// 2. POST: /ParentMessages/Create (Thêm mẫu tin nhắn mới)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> Create(string title, string category, string contentTemplate, int? classId)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(contentTemplate))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ Tiêu đề và Nội dung mẫu tin nhắn.";
                return RedirectToAction(nameof(Index), new { classId });
            }

            var item = new ParentMessageTemplate
            {
                Title = title.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "CHUNG" : category.Trim().ToUpperInvariant(),
                ContentTemplate = contentTemplate.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.ParentMessageTemplates.Add(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm mẫu tin nhắn phụ huynh thành công!";
            return RedirectToAction(nameof(Index), new { classId });
        }

        /// <summary>
        /// 3. POST: /ParentMessages/Delete/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> Delete(long id, int? classId)
        {
            var item = await _context.ParentMessageTemplates.FindAsync(id);
            if (item != null)
            {
                _context.ParentMessageTemplates.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa mẫu tin nhắn.";
            }
            return RedirectToAction(nameof(Index), new { classId });
        }

        /// <summary>
        /// 4. GET: /ParentMessages/GetTemplatesJson (Dùng cho Popup trong Sổ Điểm Danh)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTemplatesJson()
        {
            await EnsureDefaultTemplatesSeededAsync();

            var templates = await _context.ParentMessageTemplates
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    category = t.Category,
                    contentTemplate = t.ContentTemplate
                })
                .AsNoTracking()
                .ToListAsync();

            return Json(new { success = true, templates });
        }

        /// <summary>
        /// 5. GET: /ParentMessages/RenderTemplateByEnrollment (Điền đầy đủ biến động điểm số, chuyên cần & cảnh báo Bí tích - TASK-P3-07.2)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RenderTemplateByEnrollment(long templateId, long enrollmentId, string? customDate = null)
        {
            var template = await _context.ParentMessageTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId);
            if (template == null)
                return NotFound(new { success = false, message = "Không tìm thấy mẫu tin nhắn." });

            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.ClassRoom)
                .Include(e => e.GradeRecords)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment == null || enrollment.ClassRoom == null || enrollment.Student == null)
                return BadRequest(new { success = false, message = "Không tìm thấy thông tin xếp lớp của thiếu nhi." });

            if (!await CanAccessClassAsync(enrollment.ClassRoom))
                return Forbid();

            var compiled = await CompileTemplateForEnrollmentAsync(template.ContentTemplate, enrollment, customDate);

            return Json(new
            {
                success = true,
                studentName = $"{enrollment.Student.ChristianName} {enrollment.Student.FirstName} {enrollment.Student.LastName}".Trim(),
                parentPhone = enrollment.Student.ParentPhone ?? "",
                compiledMessage = compiled
            });
        }

        /// <summary>
        /// 6. GET: /ParentMessages/RenderTemplate (Hỗ trợ truyền templateId, studentId, classId)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RenderTemplate(long templateId, long studentId, int classId)
        {
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ClassRoomId == classId && e.StudentId == studentId);

            if (enrollment == null)
                return BadRequest(new { success = false, message = "Học viên không thuộc lớp học đã chọn." });

            return await RenderTemplateByEnrollment(templateId, enrollment.Id);
        }

        /// <summary>
        /// 7. GET: /ParentMessages/QuickAbsenceMessage (Tạo tin nhắn 1-Click Copy tức thì từ dòng Sổ Điểm Danh - TASK-P3-07.4)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> QuickAbsenceMessage(
            long enrollmentId,
            string? attendanceDate,
            bool attendedMass = false,
            string? massStatus = null,
            bool classAttended = false,
            string? classStatus = null)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.ClassRoom)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment == null || enrollment.ClassRoom == null || enrollment.Student == null)
                return BadRequest(new { success = false, message = "Không tìm thấy hồ sơ học sinh." });

            if (!await CanAccessClassAsync(enrollment.ClassRoom))
                return Forbid();

            string dateDisplay = DateTime.TryParse(attendanceDate, out var parsedDt)
                ? parsedDt.ToString("dd/MM/yyyy")
                : DateTime.Now.ToString("dd/MM/yyyy");

            var studentFullName = $"{enrollment.Student.ChristianName} {enrollment.Student.FirstName} {enrollment.Student.LastName}".Trim();
            var teacherContact = await GetCurrentStaffContactInfoAsync();

            var issues = new List<string>();
            if (!attendedMass)
            {
                issues.Add(massStatus == "ABSENT_PERMITTED" ? "vắng Thánh Lễ (có phép)" : "vắng mặt Thánh Lễ (chưa có phép)");
            }
            else if (massStatus == "LATE")
            {
                issues.Add("đi tham dự Thánh Lễ trễ");
            }

            if (parsedDt.DayOfWeek == DayOfWeek.Sunday)
            {
                if (!classAttended)
                {
                    issues.Add(classStatus == "ABSENT_PERMITTED" ? "vắng giờ học Giáo lý (có phép)" : "vắng giờ học Giáo lý (chưa có phép)");
                }
                else if (classStatus == "LATE")
                {
                    issues.Add("vào lớp Giáo lý trễ");
                }
            }

            string statusSummary = issues.Any()
                ? string.Join(" và ", issues)
                : "vắng mặt trong buổi sinh hoạt Giáo lý";

            string message = $"Kính chào Quý Phụ huynh em {studentFullName} (Mã TN: {enrollment.Student.StudentCode} - Lớp {enrollment.ClassRoom.Name}). Hôm nay ngày {dateDisplay}, Ban Giáo Lý ghi nhận em {statusSummary}. Kính mong Quý Phụ huynh lưu ý nhắc nhở hoặc phản hồi lý do cho GLV phụ trách ({teacherContact}). Xin chân thành cảm ơn Quý Phụ huynh!";

            return Json(new
            {
                success = true,
                studentName = studentFullName,
                parentPhone = enrollment.Student.ParentPhone ?? "",
                compiledMessage = message
            });
        }

        #region Helpers Tính toán Biến động Học lực & Chuyên cần
        private async Task<string> GetCurrentStaffContactInfoAsync()
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return "Ban Giáo Lý Giáo xứ Phú Trung";

            var name = $"{user.ChristianName} {user.FirstName} {user.LastName}".Trim();
            return !string.IsNullOrWhiteSpace(user.PhoneNumber)
                ? $"GLV {name} - SĐT: {user.PhoneNumber}"
                : $"GLV {name}";
        }

        private async Task<string> CompileTemplateForEnrollmentAsync(string rawTemplate, Enrollment enrollment, string? customDate)
        {
            var student = enrollment.Student;
            var cls = enrollment.ClassRoom;

            // 1. Lấy cấu hình cột điểm của lớp
            var configs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == cls.AcademicYearId &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == cls.GradeLevel) &&
                            (g.ClassRoomId == null || g.ClassRoomId == cls.Id))
                .AsNoTracking()
                .ToListAsync();

            var hk1Configs = configs.Where(c => c.Semester == 1).ToList();
            var hk2Configs = configs.Where(c => c.Semester == 2).ToList();

            var hk1Scores = enrollment.GradeRecords
                .Where(r => hk1Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

            var hk2Scores = enrollment.GradeRecords
                .Where(r => hk2Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

            var sem1Avg = _gradeService.CalculateSemesterAverage(hk1Scores, hk1Configs);
            var sem2Avg = _gradeService.CalculateSemesterAverage(hk2Scores, hk2Configs);
            var yearAvg = _gradeService.CalculateYearAverage(sem1Avg, sem2Avg);

            // 2. Tính chuyên cần của học sinh trong lớp
            var classEnrollmentIds = await _context.Enrollments
                .Where(e => e.ClassRoomId == cls.Id)
                .Select(e => e.Id)
                .ToListAsync();

            var classAttendances = await _context.Attendances
                .Where(a => classEnrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            int totalSessions = Math.Max(1, classAttendances.Select(a => a.AttendanceDate.Date).Distinct().Count());
            var studentAtts = classAttendances.Where(a => a.EnrollmentId == enrollment.Id).ToList();

            int massAbsent = studentAtts.Count(a => !a.AttendedMass && (a.MassStatus == AttendanceStatus.AbsentUnpermitted || a.MassStatus == AttendanceStatus.AbsentPermitted));
            int classAbsent = studentAtts.Count(a => a.DayOfWeek == DayOfWeek.Sunday && !a.ClassAttended && (a.ClassStatus == AttendanceStatus.AbsentUnpermitted || a.ClassStatus == AttendanceStatus.AbsentPermitted));
            int totalAbsentDays = studentAtts.Count(a => !a.AttendedMass || (a.DayOfWeek == DayOfWeek.Sunday && !a.ClassAttended));

            int presentClassCount = studentAtts.Count(a => a.ClassAttended && (a.ClassStatus == AttendanceStatus.Present || a.ClassStatus == AttendanceStatus.Late));
            int presentMassCount = studentAtts.Count(a => a.AttendedMass && (a.MassStatus == AttendanceStatus.Present || a.MassStatus == AttendanceStatus.LinedUp || a.MassStatus == AttendanceStatus.Late));

            double combinedRate = classAttendances.Any()
                ? Math.Min(100.0, Math.Round((((presentClassCount + presentMassCount) / 2.0) / totalSessions) * 100.0, 1))
                : 100.0;

            var (rank, eligible) = _gradeService.EvaluateStudent(yearAvg ?? sem1Avg ?? 5.0m, combinedRate, cls.GradeLevel, 0);

            string sacramentStatusText = eligible
                ? "Đủ điều kiện xét lãnh nhận Bí tích / Lên lớp"
                : $"Chưa đủ điều kiện (Cần ĐTB >= 5.0 và Chuyên cần >= 80%, hiện đạt {combinedRate:0.0}%)";

            string dateStr = !string.IsNullOrWhiteSpace(customDate) && DateTime.TryParse(customDate, out var dt)
                ? dt.ToString("dd/MM/yyyy")
                : DateTime.Now.ToString("dd/MM/yyyy");

            string staffContact = await GetCurrentStaffContactInfoAsync();

            return rawTemplate
                .Replace("{TenThieuNhi}", $"{student.ChristianName} {student.FirstName} {student.LastName}".Trim())
                .Replace("{MaThieuNhi}", student.StudentCode)
                .Replace("{TenLop}", cls.Name)
                .Replace("{Khoi}", cls.GradeLevel)
                .Replace("{NgayVang}", dateStr)
                .Replace("{DiemTB_Ky1}", sem1Avg.HasValue ? sem1Avg.Value.ToString("0.00") : "Chưa có")
                .Replace("{DiemTB_Ky2}", sem2Avg.HasValue ? sem2Avg.Value.ToString("0.00") : "Chưa có")
                .Replace("{DiemTB_Nam}", yearAvg.HasValue ? yearAvg.Value.ToString("0.00") : "Chưa có")
                .Replace("{XepLoai}", rank)
                .Replace("{TongSoNgayVang}", totalAbsentDays.ToString())
                .Replace("{SoBuoiVangLe}", massAbsent.ToString())
                .Replace("{SoBuoiVangHoc}", classAbsent.ToString())
                .Replace("{TiLeChuyenCan}", combinedRate.ToString("0.0"))
                .Replace("{CanhBaoBiTich}", sacramentStatusText)
                .Replace("{SDTLienHe}", staffContact);
        }

        private async Task<List<StudentInteractionRowDto>> BuildClassInteractionRowsAsync(ClassRoom targetClass, int yearId)
        {
            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.GradeRecords)
                .Where(e => e.ClassRoomId == targetClass.Id && e.Student.IsActive)
                .OrderBy(e => e.Student.LastName)
                .ThenBy(e => e.Student.FirstName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            var configs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == targetClass.AcademicYearId &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == targetClass.GradeLevel) &&
                            (g.ClassRoomId == null || g.ClassRoomId == targetClass.Id))
                .AsNoTracking()
                .ToListAsync();

            var hk1Configs = configs.Where(c => c.Semester == 1).ToList();
            var hk2Configs = configs.Where(c => c.Semester == 2).ToList();

            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            int totalSessions = Math.Max(1, attendances.Select(a => a.AttendanceDate.Date).Distinct().Count());
            var rows = new List<StudentInteractionRowDto>();

            foreach (var e in enrollments)
            {
                var hk1Dict = e.GradeRecords
                    .Where(r => hk1Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var hk2Dict = e.GradeRecords
                    .Where(r => hk2Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var sem1 = _gradeService.CalculateSemesterAverage(hk1Dict, hk1Configs);
                var sem2 = _gradeService.CalculateSemesterAverage(hk2Dict, hk2Configs);
                var yearAvg = _gradeService.CalculateYearAverage(sem1, sem2);

                var stuAtts = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                int massAbs = stuAtts.Count(a => !a.AttendedMass && (a.MassStatus == AttendanceStatus.AbsentUnpermitted || a.MassStatus == AttendanceStatus.AbsentPermitted));
                int clsAbs = stuAtts.Count(a => a.DayOfWeek == DayOfWeek.Sunday && !a.ClassAttended && (a.ClassStatus == AttendanceStatus.AbsentUnpermitted || a.ClassStatus == AttendanceStatus.AbsentPermitted));
                int totalAbsDays = stuAtts.Count(a => !a.AttendedMass || (a.DayOfWeek == DayOfWeek.Sunday && !a.ClassAttended));

                int presentClass = stuAtts.Count(a => a.ClassAttended && (a.ClassStatus == AttendanceStatus.Present || a.ClassStatus == AttendanceStatus.Late));
                int presentMass = stuAtts.Count(a => a.AttendedMass && (a.MassStatus == AttendanceStatus.Present || a.MassStatus == AttendanceStatus.LinedUp || a.MassStatus == AttendanceStatus.Late));

                double combinedRate = attendances.Any()
                    ? Math.Min(100.0, Math.Round((((presentClass + presentMass) / 2.0) / totalSessions) * 100.0, 1))
                    : 100.0;

                var activeAvg = yearAvg ?? sem1;
                var (rank, eligible) = _gradeService.EvaluateStudent(activeAvg ?? 6.5m, combinedRate, targetClass.GradeLevel, 0);

                var badges = new List<string>();
                if (totalAbsDays >= 2) badges.Add($"Vắng {totalAbsDays} buổi");
                if (combinedRate < 80.0) badges.Add($"CC thấp ({combinedRate:0.0}%)");
                if (activeAvg.HasValue && activeAvg.Value < 5.0m) badges.Add($"ĐTB yếu ({activeAvg.Value:0.0})");

                rows.Add(new StudentInteractionRowDto
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    ParentPhone = e.Student.ParentPhone,
                    ClassName = targetClass.Name,
                    Semester1Average = sem1,
                    Semester2Average = sem2,
                    YearAverage = yearAvg,
                    AcademicRank = rank,
                    TotalAbsentDays = totalAbsDays,
                    MassAbsentCount = massAbs,
                    ClassAbsentCount = clsAbs,
                    CombinedAttendanceRate = combinedRate,
                    IsEligibleForSacrament = eligible,
                    NeedsAttention = badges.Any(),
                    AttentionBadges = badges
                });
            }

            // Ưu tiên đẩy các em cần lưu ý (vắng nhiều / điểm thấp) lên đầu danh sách
            return rows
                .OrderByDescending(r => r.NeedsAttention)
                .ThenByDescending(r => r.TotalAbsentDays)
                .ThenBy(r => r.FullName)
                .ToList();
        }
        #endregion
    }
}