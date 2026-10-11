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
    [Authorize]
    public class GradesController : Controller
    {
        private readonly ParishDbContext _db;
        private readonly IGradeCalculationService _calcService;
        private readonly IGradePermissionService _permissionService;

        public GradesController(ParishDbContext db, IGradeCalculationService calcService, IGradePermissionService permissionService)
        {
            _db = db;
            _calcService = calcService;
            _permissionService = permissionService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        private bool IsAdmin => User.IsInRole(UserRole.Admin) || User.IsInRole(UserRole.SpiritualDirector) || User.IsInRole(UserRole.ExecutiveBoard);

        [HttpGet]
        public async Task<IActionResult> Index(string academicYear = "2025-2026", int semester = 1, int classId = 1)
        {
            var accessibleClasses = await _permissionService.GetAssignedClassIdsAsync(CurrentUserId, academicYear, IsAdmin);
            if (!accessibleClasses.Any())
            {
                return Forbid("Bạn chưa được phân công phụ trách lớp nào trong niên khóa này.");
            }

            int selectedClassId = accessibleClasses.Contains(classId) ? classId : accessibleClasses.First();
            if (selectedClassId != classId)
            {
                return Forbid("Bạn không có quyền quản lý bảng điểm của lớp này.");
            }

            var columns = await _db.GradeColumns
                .Where(c => c.AcademicYear == academicYear && (c.Semester == semester || semester == 0))
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            // Truy vấn học sinh cùng bảng điểm hiện có
            var students = await _db.Set<StudentGrade>()
                .Include(sg => sg.GradeColumn)
                .Where(sg => sg.GradeColumn.AcademicYear == academicYear && sg.Semester == semester)
                .ToListAsync();

            var vm = new GradeManagementViewModel
            {
                AcademicYear = academicYear,
                Semester = semester,
                ClassId = classId,
                Columns = columns
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveScoreInline([FromBody] SaveScoreDto dto)
        {
            var activeAcademicYear = await _db.AcademicYears.FirstOrDefaultAsync(a => a.IsCurrent);

            // Kiểm tra quyền trên lớp mà học sinh đang theo học
            var canEdit = await _permissionService.CanManageClassAsync(CurrentUserId, dto.ClassId, activeAcademicYear?.Name ?? "2025-2026", IsAdmin);
            if (!canEdit)
            {
                return Forbid();
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
                    UpdatedAt = DateTime.UtcNow
                };
                _db.StudentGrades.Add(grade);
            }
            else
            {
                grade.Score = dto.Score;
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
            var canEdit = await _permissionService.CanManageClassAsync(CurrentUserId, model.ClassId ?? 0, activeAcademicYear?.Name ?? "2025-2026", IsAdmin);
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
            var canEdit = await _permissionService.CanManageClassAsync(CurrentUserId, model.ClassId ?? 0, activeAcademicYear?.Name ?? "2025-2026", IsAdmin);
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
