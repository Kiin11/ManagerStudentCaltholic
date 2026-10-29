using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Controllers
{
    public class StudentPlacementController : Controller
    {
        private readonly ParishDbContext _context;

        public StudentPlacementController(ParishDbContext context)
        {
            _context = context;
        }

        public async Task<ClassRoom?> DetermineBestClassForNewStudentAsync(int academicYearId, string gradeLevel, bool isMale)
        {
            // Lấy danh sách các lớp thuộc khối (Ví dụ: Rước Lễ 1) trong niên khóa
            var candidateClasses = await _context.Classes
                .Where(c => c.AcademicYearId == academicYearId && c.GradeLevel == gradeLevel && !c.IsDeleted)
                .Include(c => c.Enrollments)
                .ThenInclude(e => e.Student)
                .ToListAsync();

            if (!candidateClasses.Any()) return null;

            // Thuật toán Smart Balancing: 
            // Ưu tiên 1: Chọn lớp có tổng sĩ số hiện tại ít nhất
            // Ưu tiên 2: Nếu sĩ số bằng nhau, chọn lớp có số lượng Nam/Nữ cân bằng với học sinh mới
            var bestClass = candidateClasses
                .OrderBy(c => c.Enrollments.Count(e => !e.IsDeleted))
                .ThenBy(c => {
                    int maleCount = c.Enrollments.Count(e => !e.IsDeleted && e.Student.Gender == "Nam");
                    int femaleCount = c.Enrollments.Count(e => !e.IsDeleted && e.Student.Gender == "Nữ");
                    return isMale ? maleCount : femaleCount;
                })
                .FirstOrDefault();

            return bestClass;
        }
    }
}
