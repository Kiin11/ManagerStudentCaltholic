using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class GradePermissionService : IGradePermissionService
    {
        private readonly ParishDbContext _db;

        public GradePermissionService(ParishDbContext context)
        {
            _db = context;
        }

        public async Task<bool> CanManageClassAsync(string userId, int classId, string academicYear, bool isAdmin)
        {
            if (isAdmin) return true;
            return await _db.TeacherClasses.AnyAsync(tc =>
                tc.UserId == userId && tc.ClassId == classId && tc.AcademicYear == academicYear && tc.IsActive);
        }

        public async Task<List<int>> GetAssignedClassIdsAsync(string userId, string academicYear, bool isAdmin)
        {
            if (isAdmin)
            {
                // Giả định có bảng Classes
                //return await _db.Database.SqlQueryRaw<int>("SELECT id FROM Classes").ToListAsync();
                return await _db.Classes.Select(c => c.Id).ToListAsync();
            }

            return await _db.TeacherClasses
                .Where(tc => tc.UserId == userId && tc.AcademicYear == academicYear && tc.IsActive)
                .Select(tc => tc.ClassId)
                .ToListAsync();
        }

        // Ưu tiên: Lấy cột cấu hình riêng của Lớp -> Nếu không có thì lấy cột mặc định chung (class_id IS NULL)
        public async Task<List<GradeColumn>> GetEffectiveColumnsAsync(int classId, string academicYear, int semester)
        {
            var classCols = await _db.GradeColumns
                .Where(c => c.AcademicYear == academicYear && (c.Semester == semester || semester == 0) && c.ClassId == classId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            if (classCols.Any()) return classCols;

            return await _db.GradeColumns
                .Where(c => c.AcademicYear == academicYear && (c.Semester == semester || semester == 0) && c.ClassId == null)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
        }

        // Ưu tiên: Lấy công thức riêng của lớp -> nếu không có thì lấy công thức chung
        public async Task<GradingFormula?> GetEffectiveFormulaAsync(int classId, string academicYear, int semester)
        {
            var formula = await _db.GradingFormulas
                .FirstOrDefaultAsync(f => f.AcademicYear == academicYear && f.Semester == semester && f.ClassId == classId);

            return formula ?? await _db.GradingFormulas
                .FirstOrDefaultAsync(f => f.AcademicYear == academicYear && f.Semester == semester && f.ClassId == null);
        }
    }
}
