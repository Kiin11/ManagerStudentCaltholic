using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.SpiritualDirector},{UserRole.ExecutiveBoard},{UserRole.BranchHead},{UserRole.Teacher}")]
    public class PrintReportsController : Controller
    {
        private readonly ParishDbContext _context;
        private readonly IGradeCalculationService _gradeService;

        public PrintReportsController(ParishDbContext context, IGradeCalculationService gradeService)
        {
            _context = context;
            _gradeService = gradeService;
        }

        // =========================================================================
        // In Phiếu Liên Lạc Gia Đình (Khổ A5)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> ReportCards(int classId)
        {
            var model = await BuildBatchPrintData(classId);
            if (model == null) return NotFound();
            return View(model);
        }

        // =========================================================================
        // In Chứng Chỉ Hoàn Tất Bí Tích (Khổ A4 Ngang - RL2, TS3, BD3)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> SacramentCertificates(int classId)
        {
            var model = await BuildBatchPrintData(classId);
            if (model == null) return NotFound();
            // Lọc các em đủ điều kiện
            model.Students = model.Students.Where(s => s.IsEligibleForSacrament).ToList();
            return View(model);
        }

        // =========================================================================
        // In Giấy Khen Học Lực Cuối Năm (Khổ A4 Ngang - Loại Khá, Giỏi, Xuất sắc)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> CertificatesOfMerit(int classId)
        {
            var model = await BuildBatchPrintData(classId);
            if (model == null) return NotFound();
            // Lọc các em đạt danh hiệu Khá trở lên
            model.Students = model.Students
                .Where(s => s.AcademicRank == "Xuất sắc" || s.AcademicRank == "Giỏi" || s.AcademicRank == "Khá")
                .ToList();
            return View(model);
        }

        private async Task<BatchPrintViewModel?> BuildBatchPrintData(int classId)
        {
            var targetClass = await _context.Classes
                .Include(c => c.AcademicYear)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.GradeRecords)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (targetClass == null) return null;

            var configs = await _context.GradeConfigurations
                .Where(g => g.AcademicYearId == targetClass.AcademicYearId &&
                            (g.GradeLevel == "ALL" || g.GradeLevel == targetClass.GradeLevel))
                .ToListAsync();

            var enrollmentIds = targetClass.Enrollments.Select(e => e.Id).ToList();
            var attendances = await _context.Attendances
                .Where(a => enrollmentIds.Contains(a.EnrollmentId))
                .ToListAsync();

            var totalSessions = Math.Max(1, attendances.Select(a => a.AttendanceDate).Distinct().Count());

            var studentItems = targetClass.Enrollments
                .Where(e => e.Student.IsActive)
                .OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName)
                .Select(e =>
                {
                    var attList = attendances.Where(a => a.EnrollmentId == e.Id).ToList();
                    var presentCount = attList.Count(a => a.ClassAttended && (a.ClassStatus == "PRESENT" || a.ClassStatus == "LATE"));
                    var attRate = Math.Round((presentCount / (double)totalSessions) * 100, 1);

                    var hk1Configs = configs.Where(c => c.Semester == 1).ToList();
                    var hk1Dict = e.GradeRecords.Where(r => hk1Configs.Any(c => c.Id == r.GradeConfigurationId))
                                               .ToDictionary(r => r.GradeConfigurationId, r => r.Score);
                    var sem1Avg = _gradeService.CalculateSemesterAverage(hk1Dict, hk1Configs);

                    var hk2Configs = configs.Where(c => c.Semester == 2).ToList();
                    var hk2Dict = e.GradeRecords.Where(r => hk2Configs.Any(c => c.Id == r.GradeConfigurationId))
                                               .ToDictionary(r => r.GradeConfigurationId, r => r.Score);
                    var sem2Avg = _gradeService.CalculateSemesterAverage(hk2Dict, hk2Configs);

                    var yearAvg = _gradeService.CalculateYearAverage(sem1Avg, sem2Avg);
                    var (rank, eligible) = _gradeService.EvaluateStudent(yearAvg, attRate, targetClass.GradeLevel, 2);

                    return new PrintStudentItemViewModel
                    {
                        StudentId = e.StudentId,
                        StudentCode = e.Student.StudentCode,
                        ChristianName = e.Student.ChristianName,
                        FullName = $"{e.Student.FirstName} {e.Student.LastName}".Trim(),
                        DateOfBirth = e.Student.DateOfBirth,
                        Gender = e.Student.Gender ?? "Nam",
                        Semester1Average = sem1Avg,
                        Semester2Average = sem2Avg,
                        YearAverage = yearAvg,
                        AcademicRank = rank,
                        AttendanceRate = attRate,
                        IsEligibleForSacrament = eligible
                    };
                }).ToList();

            return new BatchPrintViewModel
            {
                ClassId = targetClass.Id,
                ClassName = targetClass.Name,
                GradeLevel = targetClass.GradeLevel,
                AcademicYearName = targetClass.AcademicYear.Name,
                Students = studentItems
            };
        }
    }
}