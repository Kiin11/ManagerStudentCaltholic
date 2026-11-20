namespace ManagerStudentCaltholic.Models.Entities
{
    /// <summary>
    /// Danh mục Khối Giáo lý chuẩn và quy tắc ánh xạ Ca học (Session) cho toàn hệ thống (TASK-01.2 - Phase 6).
    /// </summary>
    public static class GradeLevel
    {
        public const string KhaiTam = "Khai Tâm";
        public const string RuocLe = "Rước Lễ";
        public const string ThemSuc = "Thêm Sức";
        public const string BaoDong = "Bao Đồng";
        public const string DuBi = "Dự Bị";
        public const string DuBiCode = "DU_BI";

        public const string SessionMorning = "Morning";
        public const string SessionAfternoon = "Afternoon";

        /// <summary>
        /// Danh sách 4 lớp chuẩn cố định của Khối Dự Bị (Ca Chiều)
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultProbationaryClassNames =
            new[] { "Dự Bị 1", "Dự Bị 2", "Dự Bị 3", "Dự Bị 4" };

        /// <summary>
        /// Kiểm tra khối có thuộc Ca Chiều (Bao Đồng, Dự Bị) hay không.
        /// </summary>
        public static bool IsAfternoonGrade(string? gradeLevel)
        {
            if (string.IsNullOrWhiteSpace(gradeLevel)) return false;

            var normalized = gradeLevel.Trim();
            return string.Equals(normalized, BaoDong, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Bao Dong", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, DuBi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Du Bi", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, DuBiCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm tra khối có thuộc Ca Sáng (Khai Tâm, Rước Lễ, Thêm Sức) hay không.
        /// </summary>
        public static bool IsMorningGrade(string? gradeLevel) => !IsAfternoonGrade(gradeLevel);

        /// <summary>
        /// Kiểm tra lớp học có thuộc Ca Chiều dựa trên cả Session và GradeLevel.
        /// </summary>
        public static bool IsAfternoonSession(string? session, string? gradeLevel) =>
            string.Equals(session?.Trim(), SessionAfternoon, StringComparison.OrdinalIgnoreCase)
            || IsAfternoonGrade(gradeLevel);

        /// <summary>
        /// Kiểm tra lớp học có thuộc Ca Sáng dựa trên cả Session và GradeLevel.
        /// </summary>
        public static bool IsMorningSession(string? session, string? gradeLevel) =>
            !IsAfternoonSession(session, gradeLevel);

        /// <summary>
        /// Ánh xạ tự động Khối sang Ca học (Session):
        /// Khai Tâm, Rước Lễ, Thêm Sức => Morning; Bao Đồng, Dự Bị => Afternoon.
        /// </summary>
        public static string ResolveSession(string? gradeLevel) =>
            IsAfternoonGrade(gradeLevel) ? SessionAfternoon : SessionMorning;

        /// <summary>
        /// Kiểm tra khối có phải là Khối Dự Bị hay không.
        /// </summary>
        public static bool IsProbationaryGrade(string? gradeLevel)
        {
            if (string.IsNullOrWhiteSpace(gradeLevel)) return false;

            var normalized = gradeLevel.Trim();
            return string.Equals(normalized, DuBi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Du Bi", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, DuBiCode, StringComparison.OrdinalIgnoreCase);
        }
    }
}
