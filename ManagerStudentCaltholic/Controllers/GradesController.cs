using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize]
    public class GradesController : Controller
    {
        private readonly ParishDbContext _db;
        private readonly IGradePermissionService _permService;
        private readonly IGradeCalculationService _calcService;

        public GradesController(
            ParishDbContext db,
            IGradePermissionService permService,
            IGradeCalculationService calcService)
        {
            _db = db;
            _permService = permService;
            _calcService = calcService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        private bool IsAdmin => User.IsInRole(UserRole.Admin) || User.IsInRole(UserRole.SpiritualDirector) || User.IsInRole(UserRole.ExecutiveBoard);

        [HttpGet]
        public async Task<IActionResult> Index(string academicYear = "2025-2026", int semester = 1, int? classId = null)
        {
            var accessibleClasses = await _permService.GetAssignedClassIdsAsync(CurrentUserId, academicYear, IsAdmin);
            if (!accessibleClasses.Any())
            {
                return Forbid("Bạn chưa được phân công phụ trách lớp nào trong niên khóa này.");
            }

            int selectedClassId = classId ?? accessibleClasses.First();
            if (!accessibleClasses.Contains(selectedClassId))
            {
                return Forbid("Bạn không có quyền quản lý bảng điểm của lớp này.");
            }

            var columns = await _permService.GetEffectiveColumnsAsync(selectedClassId, academicYear, semester);
            var formula = await _permService.GetEffectiveFormulaAsync(selectedClassId, academicYear, semester);
            var colIds = columns.Select(c => c.Id).ToList();

            // Lấy điểm hiện có của các cột thuộc lớp & học kỳ
            var grades = await _db.StudentGrades
                .Include(sg => sg.GradeColumn)
                .Where(sg => colIds.Contains(sg.GradeColumnId) && sg.Semester == semester)
                .ToListAsync();

            // Tổng hợp danh sách dòng điểm theo từng học viên
            var rows = grades
                .GroupBy(g => g.StudentId)
                .Select(grp =>
                {
                    var scoresByCode = grp
                        .Where(x => x.Score.HasValue)
                        .ToDictionary(x => x.GradeColumn.ColumnCode, x => x.Score);

                    var nonNullScores = grp
                        .Where(x => x.Score.HasValue)
                        .ToDictionary(x => x.GradeColumn.ColumnCode, x => x.Score!.Value);

                    decimal? avg = null;
                    string classification = "Chưa xếp loại";

                    if (formula != null && nonNullScores.Any())
                    {
                        avg = _calcService.EvaluateGradeFormula(formula.FormulaExpression, nonNullScores);
                        if (avg.HasValue)
                        {
                            classification = _calcService.DetermineClassification(avg.Value, formula.EvaluationRules);
                        }
                    }

                    return new StudentGradeRowViewModel
                    {
                        StudentId = grp.Key,
                        Semester = semester,
                        ScoresByCode = scoresByCode,
                        SemesterAverage = avg,
                        Classification = classification
                    };
                })
                .OrderBy(r => r.StudentId)
                .ToList();

            var vm = new GradeManagementViewModel
            {
                AcademicYear = academicYear,
                Semester = semester,
                ClassId = selectedClassId,
                Columns = columns,
                Rows = rows
            };

            ViewBag.IsAdmin = IsAdmin;
            ViewBag.AssignedClasses = accessibleClasses;
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveScoreInline([FromBody] SaveScoreDto dto)
        {
            if (dto.Score.HasValue && (dto.Score.Value < 0m || dto.Score.Value > 10m))
            {
                return BadRequest(new { message = "Điểm số phải nằm trong khoảng từ 0 đến 10." });
            }

            // 1. Kiểm tra quyền của GLV trên lớp
            var canEdit = await _permService.CanManageClassAsync(CurrentUserId, dto.ClassId, dto.AcademicYear, IsAdmin);
            if (!canEdit)
            {
                return Forbid();
            }

            // 2. Fix IDOR: Kiểm tra cột điểm có thuộc cấu hình hợp lệ của lớp và năm học này không
            var validColumn = await _db.GradeColumns.FirstOrDefaultAsync(c =>
                c.Id == dto.GradeColumnId &&
                c.AcademicYear == dto.AcademicYear &&
                (c.ClassId == dto.ClassId || c.ClassId == null));

            if (validColumn == null)
            {
                return BadRequest(new { message = "Cột điểm không hợp lệ với lớp hiện tại." });
            }

            var grade = await _db.StudentGrades
                .FirstOrDefaultAsync(g => g.StudentId == dto.StudentId
                                       && g.GradeColumnId == dto.GradeColumnId
                                       && g.Semester == dto.Semester);

            if (grade == null)
            {
                grade = new StudentGrade
                {
                    StudentId = dto.StudentId,
                    GradeColumnId = dto.GradeColumnId,
                    Semester = dto.Semester,
                    Score = dto.Score,
                    UpdatedBy = CurrentUserId,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.StudentGrades.Add(grade);
            }
            else
            {
                grade.Score = dto.Score;
                grade.UpdatedBy = CurrentUserId;
                grade.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, score = grade.Score });
        }

        [HttpPost]
        public async Task<IActionResult> AddColumn([FromBody] GradeColumn model)
        {
            var activeAcademicYear = await _db.AcademicYears.FirstOrDefaultAsync(a => a.IsCurrent);

            // Kiểm tra quyền trên lớp mà học sinh đang theo học
            var canEdit = await _permService.CanManageClassAsync(CurrentUserId, model.ClassId ?? 0, activeAcademicYear?.Name ?? "2025-2026", IsAdmin);
            if (!canEdit)
            {
                return Forbid();
            }

            if (!ModelState.IsValid) return BadRequest(ModelState);
            _db.GradeColumns.Add(model);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = model.Id });
        }

        [HttpPost]
        public async Task<IActionResult> SaveFormula([FromBody] GradingFormula model)
        {
            var activeAcademicYear = await _db.AcademicYears.FirstOrDefaultAsync(a => a.IsCurrent);

            // Kiểm tra quyền trên lớp mà học sinh đang theo học
            var canEdit = await _permService.CanManageClassAsync(CurrentUserId, model.ClassId ?? 0, activeAcademicYear?.Name ?? "2025-2026", IsAdmin);
            if (!canEdit)
            {
                return Forbid();
            }

            var exist = await _db.GradingFormulas
                .FirstOrDefaultAsync(f => f.AcademicYear == model.AcademicYear && f.Semester == model.Semester);

            if (exist == null)
            {
                _db.GradingFormulas.Add(model);
            }
            else
            {
                exist.FormulaExpression = model.FormulaExpression;
                exist.EvaluationRules = model.EvaluationRules;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}