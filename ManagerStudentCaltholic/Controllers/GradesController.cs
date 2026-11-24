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
    [Authorize(Roles = RoleGroups.Staff)]
    public class GradesController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;
        private readonly ILogger<GradesController> _logger;
        private readonly ICacheService _cacheService;
        private readonly IClassEvaluationService _classEvaluationService;

        public GradesController(
            ParishDbContext context,
            IGradeCalculationService gradeService,
            ILogger<GradesController> logger,
            ICacheService cacheService,
            IClassEvaluationService classEvaluationService)
        {
            _context = context;
            _gradeService = gradeService;
            _logger = logger;
            _cacheService = cacheService;
            _classEvaluationService = classEvaluationService;
        }

        private long CurrentUserId
        {
            get
            {
                var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return long.TryParse(str, out var id) ? id : 0;
            }
        }

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        private async Task<bool> HasAccessToClassAsync(ClassRoom targetClass)
        {
            if (IsLeadership) return true;

            // TASK-02.3: Chặn tuyệt đối GLV Dự Bị truy cập / sửa điểm các lớp Ca Chiều (IDOR)
            if (User.IsInRole(UserRole.ProbationaryTeacher) &&
                GradeLevel.IsAfternoonSession(targetClass.Session, targetClass.GradeLevel))
            {
                return false;
            }

            bool isCurrentYear = targetClass.AcademicYear != null
                ? targetClass.AcademicYear.IsCurrent
                : await _context.AcademicYears
                    .AsNoTracking()
                    .AnyAsync(y => y.Id == targetClass.AcademicYearId && y.IsCurrent);

            if (!isCurrentYear)
                return false;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade))
                    return false;

                return string.Equals(targetClass.GradeLevel.Trim(), managedGrade.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            var uid = CurrentUserId;
            if (uid <= 0) return false;

            return await _context.ClassTeachers
                .AsNoTracking()
                .AnyAsync(ct => ct.ClassRoomId == targetClass.Id
                             && ct.UserId == uid
                             && ct.AcademicYearId == targetClass.AcademicYearId
                             && ct.ClassRoom.AcademicYear.IsCurrent);
        }

        /// <summary>
        /// 1. GET: /Grades/Index?classId=...&semester=1
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? classId, int semester = 1)
        {
            var currentYear = await _context.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);

            if (currentYear == null)
            {
                TempData["ErrorMessage"] = "Chưa có niên khóa nào được kích hoạt.";
                return RedirectToAction("Index", "Home");
            }

            var classesQuery = _context.Classes.Where(c => c.AcademicYearId == currentYear.Id);

            if (IsLeadership)
            {
                // Xem toàn bộ lớp
            }
            else if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;

                // SEC-08 FIX (Fail-Closed): Trưởng khối bắt buộc phải có ManagedGradeLevel hợp lệ
                if (!string.IsNullOrWhiteSpace(managedGrade))
                {
                    var normalizedGrade = managedGrade.Trim();
                    classesQuery = classesQuery.Where(c => c.GradeLevel == normalizedGrade);
                }
                else
                {
                    classesQuery = classesQuery.Where(_ => false);
                }
            }
            else if (User.IsInRole(UserRole.ProbationaryTeacher))
            {
                // TASK-02.3: GLV Dự Bị chỉ được phép truy cập các lớp được phân công thuộc Ca Sáng trong niên khóa hiện hành
                var uid = CurrentUserId;
                classesQuery = classesQuery.Where(c =>
                    c.ClassTeachers.Any(ct => ct.UserId == uid && ct.AcademicYearId == currentYear.Id) &&
                    c.Session != GradeLevel.SessionAfternoon &&
                    c.GradeLevel != GradeLevel.BaoDong &&
                    c.GradeLevel != GradeLevel.DuBi &&
                    c.GradeLevel != GradeLevel.DuBiCode);
            }
            else
            {
                var uid = CurrentUserId;
                classesQuery = classesQuery.Where(c => c.ClassTeachers.Any(ct => ct.UserId == uid && ct.AcademicYearId == currentYear.Id));
            }

            var allowedClasses = await classesQuery
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            if (!IsLeadership && User.IsInRole(UserRole.ProbationaryTeacher))
            {
                allowedClasses = allowedClasses
                    .Where(c => !GradeLevel.IsAfternoonSession(c.Session, c.GradeLevel))
                    .ToList();
            }

            if (!allowedClasses.Any())
            {
                ViewBag.Classes = allowedClasses;
                ViewBag.CurrentSemester = semester;
                ViewBag.NoAccessReason = "Bạn chưa được phân công giảng dạy lớp nào trong niên khóa hiện hành.";
                return View(new ClassGradeMatrixViewModel { CurrentSemester = semester });
            }

            int targetClassId = classId ?? allowedClasses.First().Id;
            var targetClass = allowedClasses.FirstOrDefault(c => c.Id == targetClassId);
            if (targetClass == null)
            {
                return Forbid();
            }

            ViewBag.Classes = allowedClasses;
            ViewBag.SelectedClassId = targetClass.Id;
            ViewBag.CurrentSemester = semester;

            // Lấy toàn bộ cấu hình cột điểm của lớp trong năm học
            var allYearConfigs = await _cacheService.GetOrSetAsync(
                CacheKeys.GradeConfigsByClass(currentYear.Id, targetClass.GradeLevel, targetClass.Id),
                async () => await _context.GradeConfigurations
                    .Where(g => g.AcademicYearId == currentYear.Id &&
                                (g.GradeLevel == "ALL" || g.GradeLevel == targetClass.GradeLevel) &&
                                (g.ClassRoomId == null || g.ClassRoomId == targetClass.Id))
                    .OrderBy(g => g.Semester)
                    .ThenBy(g => g.ColumnIndex)
                    .AsNoTracking()
                    .ToListAsync(),
                TimeSpan.FromHours(3));

            var displayColumns = semester == 0
                ? allYearConfigs
                : allYearConfigs.Where(g => g.Semester == semester).ToList();

            var hk1Configs = allYearConfigs.Where(g => g.Semester == 1).ToList();
            var hk2Configs = allYearConfigs.Where(g => g.Semester == 2).ToList();

            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.GradeRecords)
                .Where(e => e.ClassRoomId == targetClass.Id && e.Student.IsActive)
                .OrderBy(e => e.Student.StudentCode).ThenBy(e => e.Student.LastName)
                .AsNoTracking()
                .ToListAsync();

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .AsNoTracking()
                .ToListAsync();

            var totalSessions = attendances
                .Where(a => a.DayOfWeek == DayOfWeek.Sunday)
                .Select(a => a.AttendanceDate.Date)
                .Distinct()
                .Count();

            var rows = enrollments.Select(e =>
            {
                var scoreDict = e.GradeRecords
                    .Where(r => displayColumns.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var hk1Dict = e.GradeRecords
                    .Where(r => hk1Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var hk2Dict = e.GradeRecords
                    .Where(r => hk2Configs.Select(c => c.Id).Contains(r.GradeConfigurationId))
                    .ToDictionary(r => r.GradeConfigurationId, r => r.Score);

                var attList = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                var presentCount = attList.Count(a =>
                    a.DayOfWeek == DayOfWeek.Sunday &&
                    a.ClassAttended &&
                    (a.ClassStatus == AttendanceStatus.Present ||
                     a.ClassStatus == AttendanceStatus.Late ||
                     a.ClassStatus == AttendanceStatus.LinedUp));
                var attRate = totalSessions > 0
                    ? Math.Min(100.0, Math.Round((presentCount / (double)totalSessions) * 100.0, 1))
                    : 100.0;

                var sem1Avg = _gradeService.CalculateSemesterAverage(hk1Dict, hk1Configs);
                var sem2Avg = _gradeService.CalculateSemesterAverage(hk2Dict, hk2Configs);
                var yearAvg = _gradeService.CalculateYearAverage(sem1Avg, sem2Avg);

                decimal? activeAvg = semester switch
                {
                    1 => sem1Avg,
                    2 => sem2Avg,
                    _ => yearAvg
                };

                var (rank, eligible) = _gradeService.EvaluateStudent(activeAvg, attRate, targetClass.GradeLevel, semester);
                

                return new StudentGradeRowItem
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentCode = e.Student.StudentCode,
                    ChristianName = e.Student.ChristianName,
                    FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                    Scores = scoreDict,
                    Semester1Average = sem1Avg,
                    Semester2Average = sem2Avg,
                    SemesterAverage = activeAvg,
                    YearAverage = yearAvg,
                    AttendanceRate = attRate,
                    AcademicRank = rank,
                    IsEligibleForSacrament = eligible
                };
            }).ToList();

            var viewModel = new ClassGradeMatrixViewModel
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AcademicYearName = currentYear.Name,
                CurrentSemester = semester,
                IsLocked = targetClass.IsGradeLocked,
                Columns = displayColumns,
                Rows = rows,
                EvaluationAction = await _classEvaluationService.GetClassEvaluationHeaderAsync(targetClassId)
            };

            return View(viewModel);
        }

        /// <summary>
        /// 2. POST: /Grades/SaveSingleCell (Lưu Ajax tức thì từng ô & trả về ĐTB mới)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSingleCell([FromBody] SaveGradeItemDto dto)
        {
            if (dto == null || !ModelState.IsValid || dto.EnrollmentId <= 0 || dto.GradeConfigurationId <= 0)
            {
                return BadRequest(new { success = false, message = "Dữ liệu điểm không hợp lệ." });
            }

            if (dto.Score.HasValue && (dto.Score.Value < 0.00m || dto.Score.Value > 10.00m))
            {
                return BadRequest(new { success = false, message = "Điểm số phải từ 0.00 đến 10.00." });
            }

            if (!string.IsNullOrEmpty(dto.Note) && dto.Note.Length > 255)
            {
                return BadRequest(new { success = false, message = "Ghi chú không được vượt quá 255 ký tự." });
            }

            var linkedStudentId = await StaffSelfActionGuard.GetLinkedStudentIdAsync(_context, CurrentUserId);
            var currentUser = User.Identity?.Name ?? "GLV";
            decimal? normalizedScore = dto.Score.HasValue
                ? Math.Round(dto.Score.Value, 2, MidpointRounding.AwayFromZero)
                : null;
            string? normalizedNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();

            var strategy = _context.Database.CreateExecutionStrategy();
            IActionResult? earlyResult = null;
            int savedSemester = 1;
            int classId = 0;
            int academicYearId = 0;
            string gradeLevel = string.Empty;

            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var enrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.Id == dto.EnrollmentId);

                if (enrollment == null)
                {
                    earlyResult = BadRequest(new { success = false, message = "Không tìm thấy học sinh." });
                    return;
                }

                var targetClass = await _context.Classes
                    .Include(c => c.AcademicYear)
                    .FirstOrDefaultAsync(c => c.Id == enrollment.ClassRoomId);

                if (targetClass == null)
                {
                    earlyResult = BadRequest(new { success = false, message = "Không tìm thấy thông tin lớp học." });
                    return;
                }

                if (!await HasAccessToClassAsync(targetClass))
                {
                    earlyResult = StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Bạn không có quyền thao tác sổ điểm lớp này." });
                    return;
                }

                if (StaffSelfActionGuard.IsSelfStudentAction(linkedStudentId, enrollment.StudentId))
                {
                    earlyResult = StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Không được phép tự nhập hoặc sửa điểm cho chính mình." });
                    return;
                }

                if (targetClass.AcademicYear == null || !targetClass.AcademicYear.IsCurrent)
                {
                    earlyResult = BadRequest(new { success = false, message = "Chỉ được phép thao tác sổ điểm thuộc niên khóa hiện hành." });
                    return;
                }

                if (targetClass.IsGradeLocked)
                {
                    earlyResult = StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Sổ điểm lớp này đã bị khóa bởi Ban Điều Hành!" });
                    return;
                }

                var config = await _context.GradeConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == dto.GradeConfigurationId);

                if (config == null)
                {
                    earlyResult = BadRequest(new { success = false, message = "Cột điểm không tồn tại." });
                    return;
                }

                // SEC-NEW-05 FIX: Chặn truyền GradeConfigurationId của lớp khác hoặc niên khóa khác
                if (config.AcademicYearId != targetClass.AcademicYearId ||
                    (config.GradeLevel != "ALL" && config.GradeLevel != targetClass.GradeLevel) ||
                    (config.ClassRoomId != null && config.ClassRoomId != targetClass.Id))
                {
                    earlyResult = BadRequest(new { success = false, message = "Cột điểm không thuộc phạm vi lớp học này." });
                    return;
                }

                var record = await _context.GradeRecords
                    .FirstOrDefaultAsync(r => r.EnrollmentId == dto.EnrollmentId && r.GradeConfigurationId == dto.GradeConfigurationId);

                if (record == null)
                {
                    record = new GradeRecord
                    {
                        EnrollmentId = dto.EnrollmentId,
                        GradeConfigurationId = dto.GradeConfigurationId,
                        Score = normalizedScore,
                        Note = normalizedNote,
                        UpdatedBy = currentUser,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.GradeRecords.Add(record);
                }
                else
                {
                    record.Score = normalizedScore;
                    record.Note = normalizedNote;
                    record.UpdatedBy = currentUser;
                    record.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                savedSemester = config.Semester;
                classId = targetClass.Id;
                academicYearId = targetClass.AcademicYearId;
                gradeLevel = targetClass.GradeLevel;
            });

            if (earlyResult != null)
            {
                return earlyResult;
            }

            // Tính lại Điểm Trung Bình Học kỳ tức thì để cập nhật lên UI
            var semConfigs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == academicYearId &&
                            g.Semester == savedSemester &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == gradeLevel) &&
                            (g.ClassRoomId == null || g.ClassRoomId == classId))
                .AsNoTracking()
                .ToListAsync();

            var semConfigIds = semConfigs.Select(c => c.Id).ToList();
            var studentScores = await _context.GradeRecords
                .AsNoTracking()
                .Where(r => r.EnrollmentId == dto.EnrollmentId && semConfigIds.Contains(r.GradeConfigurationId))
                .ToDictionaryAsync(r => r.GradeConfigurationId, r => r.Score);

            var newAvg = _gradeService.CalculateSemesterAverage(studentScores, semConfigs);

            return Json(new
            {
                success = true,
                score = normalizedScore,
                semesterAverage = newAvg.HasValue ? newAvg.Value.ToString("0.00") : "--"
            });
        }

        /// <summary>
        /// 3. POST: /Grades/ToggleLock (Bật/Tắt khóa sổ điểm - Chỉ Leadership)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int classId)
        {
            var cls = await _context.Classes.FindAsync(classId);
            if (cls == null) return Json(new { success = false, message = "Không tìm thấy lớp học." });

            cls.IsGradeLocked = !cls.IsGradeLocked;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                isLocked = cls.IsGradeLocked,
                message = cls.IsGradeLocked ? "Đã khóa sổ điểm thành công!" : "Đã mở khóa sổ điểm!"
            });
        }

        /// <summary>
        /// 4. POST: /Grades/AddCustomColumn (Thêm cột điểm cho lớp)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCustomColumn([FromBody] CreateGradeColumnDto dto)
        {
            if (dto == null || !ModelState.IsValid || dto.ClassId <= 0 ||
                string.IsNullOrWhiteSpace(dto.DisplayName) ||
                dto.Semester is < 1 or > 2 ||
                dto.WeightFactor is < 1 or > 5)
            {
                return BadRequest(new { success = false, message = "Thông tin cột điểm không hợp lệ." });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            IActionResult? earlyResult = null;
            int invalidatedYearId = 0;
            string invalidatedGrade = string.Empty;
            int invalidatedClassId = 0;

            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var targetClass = await _context.Classes
                    .Include(c => c.AcademicYear)
                    .FirstOrDefaultAsync(c => c.Id == dto.ClassId);

                if (targetClass == null)
                {
                    earlyResult = BadRequest(new { success = false, message = "Lớp không tồn tại." });
                    return;
                }

                if (!await HasAccessToClassAsync(targetClass))
                {
                    earlyResult = StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Bạn không có quyền thao tác sổ điểm lớp này." });
                    return;
                }

                if (targetClass.AcademicYear == null || !targetClass.AcademicYear.IsCurrent)
                {
                    earlyResult = BadRequest(new { success = false, message = "Chỉ được thêm cột điểm cho lớp thuộc niên khóa hiện hành." });
                    return;
                }

                if (targetClass.IsGradeLocked)
                {
                    earlyResult = StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Sổ điểm đã khóa, không thể thêm cột." });
                    return;
                }

                var maxIndex = await _context.GradeConfigurations
                    .Where(g => g.AcademicYearId == targetClass.AcademicYearId &&
                                g.Semester == dto.Semester &&
                                (g.ClassRoomId == targetClass.Id || g.ClassRoomId == null))
                    .MaxAsync(g => (int?)g.ColumnIndex) ?? 0;

                var config = new GradeConfiguration
                {
                    AcademicYearId = targetClass.AcademicYearId,
                    GradeLevel = targetClass.GradeLevel,
                    ClassRoomId = targetClass.Id,
                    Semester = dto.Semester,
                    ScoreType = string.IsNullOrWhiteSpace(dto.ScoreType) ? "CUSTOM" : dto.ScoreType.Trim(),
                    DisplayName = dto.DisplayName.Trim(),
                    WeightFactor = dto.WeightFactor,
                    ColumnIndex = maxIndex + 1,
                    IsSacramentExam = false,
                    IsRequired = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.GradeConfigurations.Add(config);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                invalidatedYearId = targetClass.AcademicYearId;
                invalidatedGrade = targetClass.GradeLevel;
                invalidatedClassId = targetClass.Id;
            });

            if (earlyResult != null)
            {
                return earlyResult;
            }

            await _cacheService.InvalidateGradeConfigsCacheAsync(invalidatedYearId, invalidatedGrade, invalidatedClassId);
            return Json(new { success = true, message = "Thêm cột điểm thành công!" });
        }

        /// <summary>
        /// 5. POST: /Grades/DeleteCustomColumn (Xóa cột điểm riêng của lớp)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomColumn(int columnId)
        {
            if (columnId <= 0)
            {
                return BadRequest(new { success = false, message = "Mã cột điểm không hợp lệ." });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            IActionResult? earlyResult = null;
            int invalidatedYearId = 0;
            string invalidatedGrade = string.Empty;
            int invalidatedClassId = 0;

            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var config = await _context.GradeConfigurations
                    .FirstOrDefaultAsync(c => c.Id == columnId);

                if (config == null || config.ClassRoomId == null)
                {
                    earlyResult = BadRequest(new { success = false, message = "Chỉ được xóa cột điểm tự tạo của riêng lớp." });
                    return;
                }

                var targetClass = await _context.Classes
                    .Include(c => c.AcademicYear)
                    .FirstOrDefaultAsync(c => c.Id == config.ClassRoomId.Value);

                if (targetClass == null)
                {
                    earlyResult = BadRequest(new { success = false, message = "Không tìm thấy lớp học của cột điểm." });
                    return;
                }

                if (!await HasAccessToClassAsync(targetClass))
                {
                    earlyResult = StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Bạn không có quyền thao tác sổ điểm lớp này." });
                    return;
                }

                if (targetClass.AcademicYear == null || !targetClass.AcademicYear.IsCurrent)
                {
                    earlyResult = BadRequest(new { success = false, message = "Chỉ được xóa cột điểm thuộc niên khóa hiện hành." });
                    return;
                }

                if (targetClass.IsGradeLocked)
                {
                    earlyResult = StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Sổ điểm đang khóa, không thể xóa cột điểm." });
                    return;
                }

                _context.GradeConfigurations.Remove(config);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                invalidatedYearId = targetClass.AcademicYearId;
                invalidatedGrade = targetClass.GradeLevel;
                invalidatedClassId = targetClass.Id;
            });

            if (earlyResult != null)
            {
                return earlyResult;
            }

            await _cacheService.InvalidateGradeConfigsCacheAsync(invalidatedYearId, invalidatedGrade, invalidatedClassId);
            return Json(new { success = true, message = "Đã xóa cột điểm." });
        }

        #region FEAT-P4-02: Smart Class Evaluation Endpoints (Xét Lên Lớp / Bí Tích / Tuyên Hứa)

        [HttpGet]
        public async Task<IActionResult> GetClassEvaluationSheet(int classId)
        {
            if (!await _classEvaluationService.CanUserEvaluateClassAsync(User, classId))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Bạn không có quyền xem bảng xét duyệt lớp này." });

            var sheet = await _classEvaluationService.BuildEvaluationSheetAsync(classId);
            return Ok(new { success = true, data = sheet });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClassEvaluation([FromBody] SubmitClassEvaluationRequestDto dto)
        {
            if (dto == null || !ModelState.IsValid || dto.ClassId <= 0 || dto.Items == null)
                return BadRequest(new { success = false, message = "Dữ liệu xét duyệt không hợp lệ." });

            if (!await _classEvaluationService.CanUserEvaluateClassAsync(User, dto.ClassId))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Bạn không có quyền thực hiện xét duyệt trên lớp này." });

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var (success, message, batchId) = await _classEvaluationService.SubmitClassEvaluationAsync(dto, User, ip);

            if (!success)
            {
                if (message.Contains("bị khóa", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("quyền", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("tự xét duyệt", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message });
                }

                return BadRequest(new { success = false, message });
            }

            return Ok(new
            {
                success = true,
                message,
                batchId
            });
        }

        #endregion
    }
}