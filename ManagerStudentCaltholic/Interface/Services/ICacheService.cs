namespace ManagerStudentCaltholic.Interface.Services
{
    public static class CacheKeys
    {
        public const string AllAcademicYears = "academic_years:all";
        public const string CurrentAcademicYear = "academic_years:current";

        public static string ClassesByYear(int academicYearId) => $"classes:year:{academicYearId}";
        public static string GradeConfigsByClass(int academicYearId, string gradeLevel, int classId) =>
            $"grade_configs:year:{academicYearId}:grade:{gradeLevel}:class:{classId}";

        public static string EffectiveGradeColumns(string academicYear, int semester, int classId) =>
            $"grade_columns:year:{academicYear}:sem:{semester}:class:{classId}";

        public static string EffectiveGradingFormula(string academicYear, int semester, int classId) =>
            $"grading_formula:year:{academicYear}:sem:{semester}:class:{classId}";
    }

    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        Task<T> GetOrSetAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        Task InvalidateAcademicYearsCacheAsync(CancellationToken cancellationToken = default);

        Task InvalidateClassesCacheAsync(int academicYearId, CancellationToken cancellationToken = default);

        Task InvalidateGradeConfigsCacheAsync(int academicYearId, string gradeLevel, int classId, CancellationToken cancellationToken = default);
    }
}