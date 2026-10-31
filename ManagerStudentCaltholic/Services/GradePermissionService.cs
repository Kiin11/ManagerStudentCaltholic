using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class GradePermissionService : IGradePermissionService
    {
        private readonly ParishDbContext _db;
        private readonly ICacheService _cache;

        public GradePermissionService(ParishDbContext context, ICacheService cache)
        {
            _db = context;
            _cache = cache;
        }

        public async Task<bool> CanManageClassAsync(string userId, int classId, string academicYear, bool isAdmin)
        {
            if (isAdmin) return true;
            if (!long.TryParse(userId, out var uid)) return false;

            return await _db.ClassTeachers
                .Include(ct => ct.AcademicYear)
                .AnyAsync(ct => ct.UserId == uid &&
                                ct.ClassRoomId == classId &&
                                (ct.AcademicYear.Name == academicYear || ct.AcademicYear.IsCurrent));
        }

        public async Task<List<int>> GetAssignedClassIdsAsync(string userId, string academicYear, bool isAdmin)
        {
            var currentYear = await _cache.GetOrSetAsync(
                CacheKeys.CurrentAcademicYear,
                async () => await _db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.Name == academicYear || y.IsCurrent),
                TimeSpan.FromHours(12));

            var yearId = currentYear?.Id ?? 0;

            if (isAdmin)
            {
                var allClasses = await _cache.GetOrSetAsync(
                    CacheKeys.ClassesByYear(yearId),
                    async () => await _db.Classes.Where(c => yearId == 0 || c.AcademicYearId == yearId).AsNoTracking().ToListAsync(),
                    TimeSpan.FromHours(4));

                return allClasses.Select(c => c.Id).ToList();
            }

            if (!long.TryParse(userId, out var uid)) return new List<int>();

            return await _db.ClassTeachers
                .Where(ct => ct.UserId == uid && (yearId == 0 || ct.AcademicYearId == yearId))
                .Select(ct => ct.ClassRoomId)
                .Distinct()
                .ToListAsync();
        }

        // TASK-P3-09.3: Lấy danh sách cột điểm qua Redis Cache & khử trùng lặp cấu hình mặc định (ClassId IS NULL)
        public async Task<List<GradeColumn>> GetEffectiveColumnsAsync(int classId, string academicYear, int semester)
        {
            string cacheKey = CacheKeys.EffectiveGradeColumns(academicYear, semester, classId);

            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var classCols = await _db.GradeColumns
                    .Where(c => c.AcademicYear == academicYear && (c.Semester == semester || semester == 0) && c.ClassId == classId)
                    .OrderBy(c => c.SortOrder)
                    .AsNoTracking()
                    .ToListAsync();

                if (classCols.Any()) return classCols;

                // Nếu lấy cấu hình mặc định chung (ClassId == null), nhóm theo ColumnCode để tránh trùng lặp
                var defaultCols = await _db.GradeColumns
                    .Where(c => c.AcademicYear == academicYear && (c.Semester == semester || semester == 0) && c.ClassId == null)
                    .OrderBy(c => c.SortOrder)
                    .ThenByDescending(c => c.Id)
                    .AsNoTracking()
                    .ToListAsync();

                return defaultCols
                    .GroupBy(c => new { c.Semester, Code = c.ColumnCode.ToUpperInvariant() })
                    .Select(g => g.First())
                    .OrderBy(c => c.SortOrder)
                    .ToList();
            }, TimeSpan.FromHours(3));
        }

        // TASK-P3-09.3: Lấy công thức tính điểm qua Redis Cache
        public async Task<GradingFormula?> GetEffectiveFormulaAsync(int classId, string academicYear, int semester)
        {
            string cacheKey = CacheKeys.EffectiveGradingFormula(academicYear, semester, classId);

            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var formula = await _db.GradingFormulas
                    .AsNoTracking()
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefaultAsync(f => f.AcademicYear == academicYear && f.Semester == semester && f.ClassId == classId);

                return formula ?? await _db.GradingFormulas
                    .AsNoTracking()
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefaultAsync(f => f.AcademicYear == academicYear && f.Semester == semester && f.ClassId == null);
            }, TimeSpan.FromHours(3));
        }
    }
}