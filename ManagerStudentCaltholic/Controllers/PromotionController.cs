using System.Security.Claims;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard}")]
    public class PromotionController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IPromotionEvaluationService _evalService;
        private readonly IStudentPlacementService _placementService;
        private readonly ILogger<PromotionController> _logger;

        public PromotionController(
            ParishDbContext context,
            IPromotionEvaluationService evalService,
            IStudentPlacementService placementService,
            ILogger<PromotionController> logger)
        {
            _context = context;
            _evalService = evalService;
            _placementService = placementService;
            _logger = logger;
        }

        /// <summary>
        /// 1. GET: /Promotion/Index (Màn hình điều khiển xét duyệt & kết chuyển niên khóa)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int? sourceYearId, int? classId, int? targetYearId)
        {
            var allYears = await _context.AcademicYears
                .OrderByDescending(y => y.StartDate)
                .ThenByDescending(y => y.Id)
                .AsNoTracking()
                .ToListAsync();

            if (!allYears.Any())
            {
                TempData["ErrorMessage"] = "Chưa có dữ liệu niên khóa. Vui lòng tạo niên khóa trước.";
                return RedirectToAction("Index", "AcademicYears");
            }

            var currentYear = allYears.FirstOrDefault(y => y.IsCurrent) ?? allYears.First();
            int selectedSourceYearId = sourceYearId ?? currentYear.Id;

            var targetYear = allYears.FirstOrDefault(y => y.Id == targetYearId)
                ?? allYears.FirstOrDefault(y => y.Id != selectedSourceYearId && y.StartDate > currentYear.StartDate)
                ?? allYears.FirstOrDefault(y => y.Id != selectedSourceYearId)
                ?? currentYear;

            int selectedTargetYearId = targetYear.Id;

            var sourceClasses = await _context.Classes
                .Where(c => c.AcademicYearId == selectedSourceYearId)
                .OrderBy(c => c.GradeLevel)
                .ThenBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            int selectedClassId = classId ?? sourceClasses.FirstOrDefault()?.Id ?? 0;
            var selectedClass = sourceClasses.FirstOrDefault(c => c.Id == selectedClassId);

            var vm = new PromotionIndexViewModel
            {
                SourceYearId = selectedSourceYearId,
                SourceYearName = allYears.FirstOrDefault(y => y.Id == selectedSourceYearId)?.Name ?? "",
                TargetYearId = selectedTargetYearId,
                TargetYearName = allYears.FirstOrDefault(y => y.Id == selectedTargetYearId)?.Name ?? "",
                SelectedClassId = selectedClass?.Id ?? 0,
                SelectedClassName = selectedClass?.Name ?? "",
                CurrentGradeLevel = selectedClass?.GradeLevel ?? "",
                AcademicYears = allYears,
                SourceClasses = sourceClasses
            };

            if (selectedClass != null)
            {
                vm.AppliedRule = await _evalService.GetEffectiveRuleAsync(selectedSourceYearId, selectedClass.GradeLevel);
                var (nextGrade, _, _) = _evalService.ResolveProgressionTarget(
                    selectedClass.Name, selectedClass.GradeLevel, vm.AppliedRule.NextGradeLevel);
                vm.SuggestedNextGradeLevel = nextGrade;

                var targetClasses = await _context.Classes
                    .Where(c => c.AcademicYearId == selectedTargetYearId)
                    .OrderBy(c => c.GradeLevel)
                    .ThenBy(c => c.Name)
                    .AsNoTracking()
                    .ToListAsync();

                vm.TargetPromoteClasses = targetClasses
                    .Where(c => c.GradeLevel == nextGrade || c.GradeLevel == selectedClass.GradeLevel)
                    .ToList();

                vm.TargetRetainClasses = targetClasses
                    .Where(c => c.GradeLevel == selectedClass.GradeLevel)
                    .ToList();

                vm.Students = await _evalService.EvaluateClassEligibilityAsync(selectedClass.Id, selectedTargetYearId);
            }

            return View(vm);
        }

        /// <summary>
        /// 2. POST: /Promotion/SavePromotionRule (Lưu cấu hình điều kiện lên lớp cho Khối/Toàn xứ)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePromotionRule([FromBody] SavePromotionRuleDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu quy tắc xét duyệt không hợp lệ." });
            }

            var existing = await _context.PromotionRuleConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.AcademicYearId == dto.AcademicYearId && r.GradeLevel == dto.GradeLevel);

            if (existing == null)
            {
                existing = new PromotionRuleConfig
                {
                    AcademicYearId = dto.AcademicYearId,
                    GradeLevel = dto.GradeLevel.Trim(),
                    MinYearAverage = dto.MinYearAverage,
                    MinAttendanceRate = dto.MinAttendanceRate,
                    RequireSacramentPassed = dto.RequireSacramentPassed,
                    MinSacramentScore = dto.MinSacramentScore,
                    NextGradeLevel = string.IsNullOrWhiteSpace(dto.NextGradeLevel) ? null : dto.NextGradeLevel.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                _context.PromotionRuleConfigs.Add(existing);
            }
            else
            {
                existing.MinYearAverage = dto.MinYearAverage;
                existing.MinAttendanceRate = dto.MinAttendanceRate;
                existing.RequireSacramentPassed = dto.RequireSacramentPassed;
                existing.MinSacramentScore = dto.MinSacramentScore;
                existing.NextGradeLevel = string.IsNullOrWhiteSpace(dto.NextGradeLevel) ? null : dto.NextGradeLevel.Trim();
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = false;
                existing.DeletedAt = null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("AUDIT: User {User} updated PromotionRuleConfig for YearId={YearId}, Grade={Grade}",
                User.Identity?.Name, dto.AcademicYearId, dto.GradeLevel);

            return Json(new { success = true, message = "Đã lưu cấu hình quy tắc xét lên lớp thành công!" });
        }

        /// <summary>
        /// 3. POST: /Promotion/CloneClassesToNewYear (Sao chép danh mục lớp từ niên khóa cũ sang niên khóa mới)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloneClassesToNewYear(int sourceYearId, int targetYearId)
        {
            if (sourceYearId == targetYearId)
            {
                return BadRequest(new { success = false, message = "Niên khóa đích phải khác niên khóa nguồn." });
            }

            var sourceClasses = await _context.Classes
                .Where(c => c.AcademicYearId == sourceYearId)
                .AsNoTracking()
                .ToListAsync();

            if (!sourceClasses.Any())
            {
                return BadRequest(new { success = false, message = "Niên khóa nguồn chưa có danh mục lớp nào để sao chép." });
            }

            var existingTargetNames = await _context.Classes
                .Where(c => c.AcademicYearId == targetYearId)
                .Select(c => c.Name.ToLower())
                .ToListAsync();

            int clonedCount = 0;
            foreach (var src in sourceClasses)
            {
                if (!existingTargetNames.Contains(src.Name.ToLower()))
                {
                    _context.Classes.Add(new ClassRoom
                    {
                        Name = src.Name,
                        GradeLevel = src.GradeLevel,
                        AcademicYearId = targetYearId,
                        ClassRoomLocationId = src.ClassRoomLocationId,
                        IsGradeLocked = false
                    });
                    clonedCount++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("AUDIT: User {User} cloned {Count} classes from Year {Src} to Year {Dst}",
                User.Identity?.Name, clonedCount, sourceYearId, targetYearId);

            return Json(new
            {
                success = true,
                clonedCount,
                message = clonedCount > 0
                    ? $"Đã sao chép thành công {clonedCount} lớp học sang niên khóa mới!"
                    : "Tất cả các lớp đã tồn tại ở niên khóa đích."
            });
        }

        /// <summary>
        /// 4. POST: /Promotion/ExecuteRollover (Giao dịch ACID kết chuyển học sinh hàng loạt sang niên khóa mới - TASK-P3-01.4)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteRollover([FromBody] ExecuteRolloverRequestDto request)
        {
            if (request == null || request.Decisions == null || !request.Decisions.Any())
            {
                return BadRequest(new { success = false, message = "Không có dữ liệu học sinh để kết chuyển." });
            }

            if (request.SourceAcademicYearId == request.TargetAcademicYearId)
            {
                return BadRequest(new { success = false, message = "Niên khóa mới phải khác niên khóa xét duyệt hiện tại." });
            }

            // 1. Kiểm tra chống IDOR: Xác thực lớp nguồn thuộc đúng niên khóa nguồn
            var sourceClass = await _context.Classes
                .FirstOrDefaultAsync(c => c.Id == request.SourceClassId && c.AcademicYearId == request.SourceAcademicYearId);

            if (sourceClass == null)
            {
                return BadRequest(new { success = false, message = "Lớp nguồn không hợp lệ hoặc không thuộc niên khóa đã chọn." });
            }

            var targetYear = await _context.AcademicYears.FindAsync(request.TargetAcademicYearId);
            if (targetYear == null)
            {
                return BadRequest(new { success = false, message = "Niên khóa đích không tồn tại." });
            }

            // Lấy toàn bộ lớp hợp lệ thuộc niên khóa đích
            var validTargetClasses = await _context.Classes
                .Where(c => c.AcademicYearId == request.TargetAcademicYearId)
                .ToDictionaryAsync(c => c.Id, c => c);

            // Lấy các Enrollment hợp lệ thuộc lớp nguồn
            var sourceEnrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassRoomId == request.SourceClassId)
                .ToDictionaryAsync(e => e.Id, e => e);

            // Nếu bật Smart Balancing cho các em PROMOTE chưa chọn TargetClassId cụ thể
            var rule = await _evalService.GetEffectiveRuleAsync(sourceClass.AcademicYearId, sourceClass.GradeLevel);
            var (nextGrade, nextPrefix, _) = _evalService.ResolveProgressionTarget(
                sourceClass.Name, sourceClass.GradeLevel, rule.NextGradeLevel);

            var unassignedPromoteStudentIds = request.Decisions
                .Where(d => d.Action == "PROMOTE" && (!d.TargetClassId.HasValue || d.TargetClassId == 0))
                .Select(d => d.StudentId)
                .ToList();

            var autoBalancedMap = new Dictionary<long, int>();
            if (unassignedPromoteStudentIds.Any() && request.UseSmartBalancing)
            {
                autoBalancedMap = await _placementService.BatchAssignToTargetClassesAsync(
                    unassignedPromoteStudentIds, request.TargetAcademicYearId, nextGrade, nextPrefix);
            }

            int promotedCount = 0;
            int retainedCount = 0;
            int skippedCount = 0;
            int graduatedCount = 0;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var decision in request.Decisions)
                {
                    // Chặn IDOR: Đảm bảo EnrollmentId thực sự thuộc lớp nguồn và khớp StudentId
                    if (!sourceEnrollments.TryGetValue(decision.EnrollmentId, out var srcEnrollment) ||
                        srcEnrollment.StudentId != decision.StudentId)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Phát hiện dữ liệu không hợp lệ đối với học sinh ID #{decision.StudentId}."
                        });
                    }

                    string action = (decision.Action ?? "PROMOTE").Trim().ToUpperInvariant();

                    if (action == "SKIP")
                    {
                        // Nghỉ học / Chuyển xứ -> Ngừng kích hoạt hồ sơ học sinh
                        srcEnrollment.Student.IsActive = false;
                        skippedCount++;
                        continue;
                    }

                    if (action == "GRADUATE")
                    {
                        // Hoàn thành chương trình Bao Đồng năm cuối
                        graduatedCount++;
                        continue;
                    }

                    // Xác định lớp đích cho PROMOTE hoặc RETAIN
                    int? targetClassId = decision.TargetClassId;
                    if ((!targetClassId.HasValue || targetClassId == 0) && action == "PROMOTE")
                    {
                        if (autoBalancedMap.TryGetValue(decision.StudentId, out var autoId))
                        {
                            targetClassId = autoId;
                        }
                    }

                    if (!targetClassId.HasValue || !validTargetClasses.ContainsKey(targetClassId.Value))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Chưa chọn lớp đích hợp lệ tại niên khóa mới cho thiếu nhi {srcEnrollment.Student.FirstName} {srcEnrollment.Student.LastName}."
                        });
                    }

                    // Kiểm tra xem học sinh đã có Enrollment ở niên khóa đích chưa (kể cả bản ghi Soft-Deleted)
                    var existingTargetEnrollment = await _context.Enrollments
                        .IgnoreQueryFilters()
                        .Include(e => e.ClassRoom)
                        .FirstOrDefaultAsync(e => e.StudentId == decision.StudentId &&
                                                  e.ClassRoom.AcademicYearId == request.TargetAcademicYearId);

                    if (existingTargetEnrollment != null)
                    {
                        // Nếu bản ghi cũ từng bị xóa mềm (IsDeleted = true), khôi phục và cập nhật lớp đích
                        existingTargetEnrollment.ClassRoomId = targetClassId.Value;
                        existingTargetEnrollment.IsDeleted = false;
                        existingTargetEnrollment.DeletedAt = null;
                    }
                    else
                    {
                        _context.Enrollments.Add(new Enrollment
                        {
                            StudentId = decision.StudentId,
                            ClassRoomId = targetClassId.Value,
                            IsDeleted = false
                        });
                    }

                    if (action == "PROMOTE") promotedCount++;
                    else if (action == "RETAIN") retainedCount++;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var actor = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";
                _logger.LogInformation(
                    "AUDIT ROLLOVER: User={Actor} executed rollover from Class={SourceClassId} (Year={SrcYear}) to Year={TargetYear}. Promoted={Promoted}, Retained={Retained}, Graduated={Graduated}, Skipped={Skipped}",
                    actor, request.SourceClassId, request.SourceAcademicYearId, request.TargetAcademicYearId,
                    promotedCount, retainedCount, graduatedCount, skippedCount);

                return Json(new
                {
                    success = true,
                    promotedCount,
                    retainedCount,
                    graduatedCount,
                    skippedCount,
                    message = $"Kết chuyển hoàn tất! Lên lớp: {promotedCount} em | Ở lại lớp: {retainedCount} em | Tốt nghiệp: {graduatedCount} em | Nghỉ/Chuyển xứ: {skippedCount} em."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi thực thi kết chuyển niên khóa cho lớp {ClassId}", request.SourceClassId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi trong quá trình kết chuyển dữ liệu. Hệ thống đã hoàn tác (Rollback) an toàn."
                });
            }
        }
    }
}