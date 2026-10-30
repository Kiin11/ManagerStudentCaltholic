using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Policy = "RequireStaff")]
    public class SacramentsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly ISacramentRegistryService _sacramentService;
        private readonly ISacramentEvaluationService _evaluationService;
        private readonly ILogger<SacramentsController> _logger;

        public SacramentsController(
            ParishDbContext context,
            ISacramentRegistryService sacramentService,
            ISacramentEvaluationService evaluationService,
            ILogger<SacramentsController> logger)
        {
            _context = context;
            _sacramentService = sacramentService;
            _evaluationService = evaluationService;
            _logger = logger;
        }

        private long CurrentUserId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private bool IsLeadership =>
            User.IsInRole(UserRole.Admin) ||
            User.IsInRole(UserRole.SpiritualDirector) ||
            User.IsInRole(UserRole.ExecutiveBoard);

        private bool CanEditRegistry =>
            IsLeadership || User.IsInRole(UserRole.BranchHead);
        private async Task<bool> CanAccessStudentAsync(long studentId)
        {
            if (IsLeadership) return true;

            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            if (User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade)) return false;

                return await _context.Enrollments
                    .Include(e => e.ClassRoom)
                    .AnyAsync(e => e.StudentId == studentId &&
                                   (yearId == 0 || e.ClassRoom.AcademicYearId == yearId) &&
                                   e.ClassRoom.GradeLevel == managedGrade);
            }

            var assignedClassIds = await _context.ClassTeachers
                .Where(ct => ct.UserId == CurrentUserId && (yearId == 0 || ct.AcademicYearId == yearId))
                .Select(ct => ct.ClassRoomId)
                .ToListAsync();

            return await _context.Enrollments
                .AnyAsync(e => e.StudentId == studentId && assignedClassIds.Contains(e.ClassRoomId));
        }

        private static int CalculateAge(DateTime dob)
        {
            var today = DateTime.UtcNow.Date;
            int age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return Math.Max(0, age);
        }

        /// <summary>
        /// 0. GET: /Sacraments/Index (Trang Tra cứu & Quản lý Sổ Bộ Bí Tích Tập Trung)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? grade, int? classId, int? sacramentFilter, int page = 1)
        {
            const int pageSize = 20;
            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
            var yearId = currentYear?.Id ?? 0;

            // Lọc danh sách lớp theo phạm vi quyền hạn
            var classesQuery = _context.Classes.Where(c => yearId == 0 || c.AcademicYearId == yearId);
            if (!IsLeadership)
            {
                if (User.IsInRole(UserRole.BranchHead))
                {
                    var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                    classesQuery = !string.IsNullOrWhiteSpace(managedGrade)
                        ? classesQuery.Where(c => c.GradeLevel == managedGrade)
                        : classesQuery.Where(_ => false);
                }
                else
                {
                    var assignedIds = await _context.ClassTeachers
                        .Where(ct => ct.UserId == CurrentUserId && (yearId == 0 || ct.AcademicYearId == yearId))
                        .Select(ct => ct.ClassRoomId)
                        .ToListAsync();
                    classesQuery = classesQuery.Where(c => assignedIds.Contains(c.Id));
                }
            }

            var availableClasses = await classesQuery
                .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            var allowedClassIds = availableClasses.Select(c => c.Id).ToList();

            // Lấy danh sách học sinh trong phạm vi
            IQueryable<Student> studentQuery = _context.Students.Where(s => s.IsActive).AsNoTracking();

            if (!IsLeadership)
            {
                var studentIdsInScope = await _context.Enrollments
                    .Where(e => allowedClassIds.Contains(e.ClassRoomId))
                    .Select(e => e.StudentId)
                    .Distinct()
                    .ToListAsync();

                studentQuery = studentQuery.Where(s => studentIdsInScope.Contains(s.Id));
            }

            if (classId.HasValue && classId.Value > 0)
            {
                var studentIdsInClass = await _context.Enrollments
                    .Where(e => e.ClassRoomId == classId.Value)
                    .Select(e => e.StudentId)
                    .ToListAsync();
                studentQuery = studentQuery.Where(s => studentIdsInClass.Contains(s.Id));
            }
            else if (!string.IsNullOrWhiteSpace(grade) && grade != "ALL")
            {
                var studentIdsInGrade = await _context.Enrollments
                    .Include(e => e.ClassRoom)
                    .Where(e => (yearId == 0 || e.ClassRoom.AcademicYearId == yearId) && e.ClassRoom.GradeLevel == grade)
                    .Select(e => e.StudentId)
                    .ToListAsync();
                studentQuery = studentQuery.Where(s => studentIdsInGrade.Contains(s.Id));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim().ToLower();
                var matchedStudentIdsByBook = await _context.StudentSacramentRecords
                    .Where(r => (r.RegistryBookNumber != null && r.RegistryBookNumber.ToLower().Contains(kw)) ||
                                (r.PriestName != null && r.PriestName.ToLower().Contains(kw)) ||
                                (r.GodparentName != null && r.GodparentName.ToLower().Contains(kw)))
                    .Select(r => r.StudentId)
                    .Distinct()
                    .ToListAsync();

                studentQuery = studentQuery.Where(s =>
                    s.StudentCode.ToLower().Contains(kw) ||
                    s.ChristianName.ToLower().Contains(kw) ||
                    s.FirstName.ToLower().Contains(kw) ||
                    s.LastName.ToLower().Contains(kw) ||
                    matchedStudentIdsByBook.Contains(s.Id));
            }

            var allMatchedStudents = await studentQuery
                .OrderBy(s => s.StudentCode)
                .ThenBy(s => s.LastName)
                .ToListAsync();

            var allIds = allMatchedStudents.Select(s => s.Id).ToList();

            var enrollmentsMap = await _context.Enrollments
                .Include(e => e.ClassRoom)
                .Where(e => allIds.Contains(e.StudentId) && (yearId == 0 || e.ClassRoom.AcademicYearId == yearId))
                .AsNoTracking()
                .ToListAsync();

            var sacramentRecords = await _context.StudentSacramentRecords
                .Where(r => allIds.Contains(r.StudentId))
                .AsNoTracking()
                .ToListAsync();

            var rows = new List<StudentSacramentSummaryRow>();

            foreach (var s in allMatchedStudents)
            {
                var enr = enrollmentsMap.FirstOrDefault(e => e.StudentId == s.Id);
                var recs = sacramentRecords.Where(r => r.StudentId == s.Id).ToList();

                var rBaptism = recs.FirstOrDefault(r => r.SacramentType == 1);
                var rCommunion = recs.FirstOrDefault(r => r.SacramentType == 2);
                var rConfirm = recs.FirstOrDefault(r => r.SacramentType == 3);
                var rProf = recs.FirstOrDefault(r => r.SacramentType == 4);

                bool hasBaptism = rBaptism != null || s.BaptismDate.HasValue;
                bool hasCommunion = rCommunion != null;
                bool hasConfirm = rConfirm != null || s.ConfirmationDate.HasValue;
                bool hasProf = rProf != null;

                int age = CalculateAge(s.DateOfBirth);
                bool ageWarning = (age >= 1 && !hasBaptism) ||
                                  (age >= 10 && !hasCommunion) ||
                                  (age >= 13 && !hasConfirm) ||
                                  (age >= 16 && !hasProf);

                // Bộ lọc theo trạng thái Bí tích
                if (sacramentFilter.HasValue)
                {
                    if (sacramentFilter.Value == 1 && !hasBaptism) continue;
                    if (sacramentFilter.Value == 2 && !hasCommunion) continue;
                    if (sacramentFilter.Value == 3 && !hasConfirm) continue;
                    if (sacramentFilter.Value == 4 && !hasProf) continue;
                    if (sacramentFilter.Value == -1 && !ageWarning) continue;
                }

                rows.Add(new StudentSacramentSummaryRow
                {
                    StudentId = s.Id,
                    StudentCode = s.StudentCode,
                    ChristianName = s.ChristianName,
                    FullName = $"{s.FirstName} {s.LastName}".Trim(),
                    DateOfBirth = s.DateOfBirth,
                    Age = age,
                    ClassName = enr?.ClassRoom?.Name ?? "Chưa xếp lớp",
                    GradeLevel = enr?.ClassRoom?.GradeLevel ?? "",
                    HasBaptism = hasBaptism,
                    BaptismDateStr = rBaptism != null ? rBaptism.ReceivedDate.ToString("dd/MM/yyyy") : s.BaptismDate?.ToString("dd/MM/yyyy"),
                    BaptismBookNo = rBaptism?.RegistryBookNumber,
                    HasFirstCommunion = hasCommunion,
                    FirstCommunionDateStr = rCommunion?.ReceivedDate.ToString("dd/MM/yyyy"),
                    FirstCommunionBookNo = rCommunion?.RegistryBookNumber,
                    HasConfirmation = hasConfirm,
                    ConfirmationDateStr = rConfirm != null ? rConfirm.ReceivedDate.ToString("dd/MM/yyyy") : s.ConfirmationDate?.ToString("dd/MM/yyyy"),
                    ConfirmationBookNo = rConfirm?.RegistryBookNumber,
                    HasProfessionOfFaith = hasProf,
                    ProfessionDateStr = rProf?.ReceivedDate.ToString("dd/MM/yyyy"),
                    ProfessionBookNo = rProf?.RegistryBookNumber,
                    HasAgeWarning = ageWarning
                });
            }

            int totalCount = rows.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Max(1, Math.Min(page, totalPages));

            var vm = new SacramentRegistryIndexViewModel
            {
                SearchKeyword = search,
                SelectedGrade = grade ?? "ALL",
                SelectedClassId = classId,
                SacramentFilter = sacramentFilter,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalStudents = totalCount,
                TotalBaptized = rows.Count(r => r.HasBaptism),
                TotalFirstCommunion = rows.Count(r => r.HasFirstCommunion),
                TotalConfirmed = rows.Count(r => r.HasConfirmation),
                TotalProfessionOfFaith = rows.Count(r => r.HasProfessionOfFaith),
                AvailableClasses = availableClasses,
                Rows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            };

            return View(vm);
        }

        /// <summary>
        /// 1. GET: /Sacraments/GetRecords/{id} (Lấy Timeline 4 Bí tích & Cảnh báo thiếu Bí tích)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRecords(long id)
        {
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ thiếu nhi." });
            }

            if (!await CanAccessStudentAsync(id))
            {
                return Forbid();
            }

            int currentAge = CalculateAge(student.DateOfBirth);
            var timeline = await _sacramentService.GetStudentSacramentsAsync(id);
            var missingAlerts = await _sacramentService.GetMissingSacramentsAsync(id, currentAge);

            return Json(new StudentSacramentTimelineResponseDto
            {
                Success = true,
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                ChristianName = student.ChristianName,
                FullName = $"{student.FirstName} {student.LastName}".Trim(),
                DateOfBirthDisplay = student.DateOfBirth.ToString("dd/MM/yyyy"),
                CurrentAge = currentAge,
                CanEdit = CanEditRegistry,
                Timeline = timeline,
                MissingAlerts = missingAlerts
            });
        }

        /// <summary>
        /// 2. POST: /Sacraments/SaveRecord (Thêm mới hoặc cập nhật Sổ bộ Bí tích + Ghi Audit Log)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> SaveRecord([FromBody] SaveSacramentRecordRequestDto dto)
        {
            if (dto == null || !ModelState.IsValid)
            {
                var firstErr = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                               ?? "Dữ liệu Sổ bộ Bí tích không hợp lệ.";
                return BadRequest(new { success = false, message = firstErr });
            }

            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == dto.StudentId);

            if (student == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ thiếu nhi." });
            }

            if (!await CanAccessStudentAsync(dto.StudentId))
            {
                return Forbid();
            }

            if (dto.ReceivedDate.Date < student.DateOfBirth.Date)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Ngày lãnh nhận Bí tích ({dto.ReceivedDate:dd/MM/yyyy}) không được nhỏ hơn Ngày sinh ({student.DateOfBirth:dd/MM/yyyy}) của thiếu nhi!"
                });
            }

            var operatorName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Staff";

            try
            {
                var savedRecord = await _sacramentService.AddOrUpdateSacramentRecordAsync(dto, operatorName);

                _logger.LogInformation(
                    "AUDIT SACRAMENT_UPSERT: User={Operator} saved SacramentType={Type} for StudentId={StudentId} ({StudentCode}), BookNo='{BookNo}', Parish='{Parish}', Priest='{Priest}'",
                    operatorName, dto.SacramentType, student.Id, student.StudentCode,
                    savedRecord.RegistryBookNumber ?? "N/A", savedRecord.ParishName, savedRecord.PriestName ?? "N/A");

                return Json(new
                {
                    success = true,
                    recordId = savedRecord.Id,
                    message = "Đã lưu thông tin Sổ bộ Bí tích thành công!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu Sổ bộ Bí tích cho StudentId={StudentId}", dto.StudentId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi lưu Sổ bộ Bí tích."
                });
            }
        }

        /// <summary>
        /// 3. POST: /Sacraments/DeleteRecord (Xóa mềm bản ghi Bí tích + Ghi Audit Log)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> DeleteRecord([FromBody] DeleteSacramentRecordRequestDto dto)
        {
            if (dto == null || !ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Yêu cầu xóa không hợp lệ." });
            }

            if (!await CanAccessStudentAsync(dto.StudentId))
            {
                return Forbid();
            }

            var operatorName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Staff";

            bool deleted = await _sacramentService.DeleteSacramentRecordAsync(dto.RecordId, dto.StudentId, operatorName);
            if (!deleted)
            {
                return NotFound(new { success = false, message = "Không tìm thấy bản ghi Sổ bộ Bí tích cần xóa." });
            }

            _logger.LogInformation(
                "AUDIT SACRAMENT_DELETE: User={Operator} soft-deleted SacramentRecordId={RecordId} of StudentId={StudentId}",
                operatorName, dto.RecordId, dto.StudentId);

            return Json(new
            {
                success = true,
                message = "Đã xóa bản ghi khỏi Sổ bộ Bí tích."
            });
        }

        /// <summary>
        /// 4. GET: /Sacraments/Evaluation (Dashboard tự động xét duyệt điều kiện Bí tích - TASK-P3-04.3)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> Evaluation(
            int? academicYearId,
            string? grade,
            int? classId,
            int? sacramentType,
            decimal minAvg = 5.0m,
            double minAtt = 80.0,
            bool reqExam = true,
            decimal minExam = 5.0m,
            long? batchId = null)
        {
            var currentYear = await _context.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent)
                              ?? await _context.AcademicYears.OrderByDescending(y => y.StartDate).AsNoTracking().FirstOrDefaultAsync();

            int targetYearId = academicYearId ?? currentYear?.Id ?? 0;
            string selectedGrade = string.IsNullOrWhiteSpace(grade) ? "Rước Lễ" : grade.Trim();

            // Nếu là Trưởng khối (BranchHead): Khóa phạm vi vào đúng khối mình phụ trách
            if (User.IsInRole(UserRole.BranchHead) && !IsLeadership)
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade))
                {
                    return Forbid();
                }
                selectedGrade = managedGrade;
            }

            int targetSacType = sacramentType ?? _evaluationService.InferDefaultSacramentTypeByGrade(selectedGrade);

            var vm = await _evaluationService.EvaluateCandidatesAsync(
                targetYearId,
                selectedGrade,
                classId,
                targetSacType,
                minAvg,
                minAtt,
                reqExam,
                minExam,
                batchId);

            vm.CanApprove = IsLeadership;
            return View(vm);
        }

        /// <summary>
        /// 5. POST: /Sacraments/SaveApprovalBatch (Lưu nháp Tờ trình xét duyệt Bí tích - TASK-P3-04.3)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> SaveApprovalBatch([FromBody] SaveSacramentApprovalBatchRequestDto dto)
        {
            if (dto == null || !ModelState.IsValid)
            {
                var err = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                          ?? "Dữ liệu tờ trình xét duyệt không hợp lệ.";
                return BadRequest(new { success = false, message = err });
            }

            if (User.IsInRole(UserRole.BranchHead) && !IsLeadership)
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade) ||
                    !string.Equals(dto.GradeLevel, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var operatorName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Ban Điều Hành";

            try
            {
                var savedBatch = await _evaluationService.SaveDraftBatchAsync(dto, operatorName);
                return Json(new
                {
                    success = true,
                    batchId = savedBatch.Id,
                    message = $"Đã lưu tờ trình xét duyệt #{savedBatch.Id} ({savedBatch.Title}) thành công!"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu tờ trình xét duyệt Bí tích");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi hệ thống khi lưu tờ trình." });
            }
        }

        /// <summary>
        /// 6. POST: /Sacraments/ApproveBatch (Cha Xứ / BĐH Phê duyệt chính thức & Đẩy vào Sổ bộ Bí tích - TASK-P3-04.4)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard}")]
        public async Task<IActionResult> ApproveBatch([FromBody] ApproveSacramentBatchRequestDto dto)
        {
            if (dto == null || dto.BatchId <= 0)
            {
                return BadRequest(new { success = false, message = "Mã đợt xét duyệt không hợp lệ." });
            }

            var approverName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Ban Điều Hành";
            var (success, syncedCount, message) = await _evaluationService.ApproveBatchAndSyncToRegistryAsync(dto.BatchId, approverName);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Json(new
            {
                success = true,
                syncedCount,
                message
            });
        }

        /// <summary>
        /// 7. GET: /Sacraments/ExportApprovalExcel (Xuất file Excel danh sách xét duyệt trình Cha Xứ - TASK-P3-04.4)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> ExportApprovalExcel(
            int academicYearId,
            string grade = "Rước Lễ",
            int? classId = null,
            int sacramentType = 2,
            decimal minAvg = 5.0m,
            double minAtt = 80.0,
            bool reqExam = true,
            decimal minExam = 5.0m,
            long? batchId = null)
        {
            if (User.IsInRole(UserRole.BranchHead) && !IsLeadership)
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade) ||
                    !string.Equals(grade, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var excelBytes = await _evaluationService.ExportApprovalSheetToExcelAsync(
                academicYearId, grade, classId, sacramentType, minAvg, minAtt, reqExam, minExam, batchId);

            var fileName = $"ToTrinh_XetDuyet_BiTich_{grade.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx";
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// 8. GET: /Sacraments/PrintApprovalSheet (Bản in A4 Biên bản đệ trình Cha Xứ ký duyệt - TASK-P3-04.4)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead}")]
        public async Task<IActionResult> PrintApprovalSheet(
            int academicYearId,
            string grade = "Rước Lễ",
            int? classId = null,
            int sacramentType = 2,
            decimal minAvg = 5.0m,
            double minAtt = 80.0,
            bool reqExam = true,
            decimal minExam = 5.0m,
            long? batchId = null,
            bool onlyApproved = true)
        {
            if (User.IsInRole(UserRole.BranchHead) && !IsLeadership)
            {
                var managedGrade = User.FindFirst("ManagedGradeLevel")?.Value;
                if (string.IsNullOrWhiteSpace(managedGrade) ||
                    !string.Equals(grade, managedGrade, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var vm = await _evaluationService.EvaluateCandidatesAsync(
                academicYearId, grade, classId, sacramentType, minAvg, minAtt, reqExam, minExam, batchId);

            if (onlyApproved)
            {
                vm.Candidates = vm.Candidates.Where(c => c.IsApprovedForSacrament && !c.AlreadyReceivedSacrament).ToList();
            }

            return View(vm);
        }
    }
}