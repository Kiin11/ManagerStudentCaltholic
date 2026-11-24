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
            if (!long.TryParse(userId, out var uid) || uid <= 0) return false;

            var userInfo = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == uid && u.IsActive)
                .Select(u => new { u.Role, u.ManagedGradeLevel })
                .FirstOrDefaultAsync();

            if (userInfo == null || string.IsNullOrEmpty(userInfo.Role)) return false;

            var userRole = userInfo.Role;

            if (userRole == UserRole.Admin ||
                userRole == UserRole.SpiritualDirector ||
                userRole == UserRole.ExecutiveBoard)
            {
                return true;
            }

            if (userRole == UserRole.Parent)
            {
                return false;
            }

            if (userRole == UserRole.BranchHead)
            {
                if (string.IsNullOrWhiteSpace(userInfo.ManagedGradeLevel))
                {
                    return false;
                }

                var managedGradeLower = userInfo.ManagedGradeLevel.Trim().ToLowerInvariant();
                return await _db.Classes
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == classId
                        && (string.IsNullOrWhiteSpace(academicYear)
                            ? c.AcademicYear.IsCurrent
                            : (c.AcademicYear.Name == academicYear || c.AcademicYear.IsCurrent))
                        && c.GradeLevel.ToLower() == managedGradeLower);
            }

            var assignedClasses = await _db.ClassTeachers
                .AsNoTracking()
                .Where(ct => ct.UserId == uid &&
                             ct.ClassRoomId == classId &&
                             ct.ClassRoom.AcademicYearId == ct.AcademicYearId &&
                             (string.IsNullOrWhiteSpace(academicYear)
                                 ? ct.AcademicYear.IsCurrent
                                 : (ct.AcademicYear.Name == academicYear || ct.AcademicYear.IsCurrent)))
                .Select(ct => new { ct.ClassRoom.Session, ct.ClassRoom.GradeLevel })
                .ToListAsync();

            if (assignedClasses.Count == 0)
            {
                return false;
            }

            if (userRole == UserRole.ProbationaryTeacher)
            {
                return assignedClasses.Any(c => !GradeLevel.IsAfternoonSession(c.Session, c.GradeLevel));
            }

            return true;
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

            if (!long.TryParse(userId, out var uid) || uid <= 0) return new List<int>();

            var userInfo = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == uid && u.IsActive)
                .Select(u => new { u.Role, u.ManagedGradeLevel })
                .FirstOrDefaultAsync();

            if (userInfo == null || string.IsNullOrEmpty(userInfo.Role)) return new List<int>();

            var userRole = userInfo.Role;

            if (userRole == UserRole.Admin ||
                userRole == UserRole.SpiritualDirector ||
                userRole == UserRole.ExecutiveBoard)
            {
                var allClasses = await _cache.GetOrSetAsync(
                    CacheKeys.ClassesByYear(yearId),
                    async () => await _db.Classes.Where(c => yearId == 0 || c.AcademicYearId == yearId).AsNoTracking().ToListAsync(),
                    TimeSpan.FromHours(4));

                return allClasses.Select(c => c.Id).ToList();
            }

            if (yearId == 0 || userRole == UserRole.Parent)
            {
                return new List<int>();
            }

            if (userRole == UserRole.BranchHead)
            {
                if (string.IsNullOrWhiteSpace(userInfo.ManagedGradeLevel))
                {
                    return new List<int>();
                }

                var managedGradeLower = userInfo.ManagedGradeLevel.Trim().ToLowerInvariant();
                return await _db.Classes
                    .AsNoTracking()
                    .Where(c => c.AcademicYearId == yearId && c.GradeLevel.ToLower() == managedGradeLower)
                    .Select(c => c.Id)
                    .ToListAsync();
            }

            var assignedClasses = await _db.ClassTeachers
                .AsNoTracking()
                .Where(ct => ct.UserId == uid &&
                             ct.AcademicYearId == yearId &&
                             ct.ClassRoom.AcademicYearId == yearId)
                .Select(ct => new
                {
                    ct.ClassRoomId,
                    ct.ClassRoom.Session,
                    ct.ClassRoom.GradeLevel
                })
                .ToListAsync();

            if (userRole == UserRole.ProbationaryTeacher)
            {
                return assignedClasses
                    .Where(c => !GradeLevel.IsAfternoonSession(c.Session, c.GradeLevel))
                    .Select(c => c.ClassRoomId)
                    .Distinct()
                    .ToList();
            }

            return assignedClasses
                .Select(c => c.ClassRoomId)
                .Distinct()
                .ToList();
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