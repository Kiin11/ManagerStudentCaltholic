using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    public class GradesController : Controller
    {
        private readonly ParishDbContext _db;
        private readonly IGradeCalculationService _calcService;

        public GradesController(ParishDbContext db, IGradeCalculationService calcService)
        {
            _db = db;
            _calcService = calcService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string academicYear = "2025-2026", int semester = 1, int classId = 1)
        {
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
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _db.GradeColumns.Add(model);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = model.Id });
        }

        [HttpPost]
        public async Task<IActionResult> SaveFormula([FromBody] GradingFormula model)
        {
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
