using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IGradeCalculationService
    {
        decimal? CalculateSemesterAverage(Dictionary<int, decimal?> scores, List<GradeConfiguration> configs);

        decimal? CalculateYearAverage(decimal? hk1Average, decimal? hk2Average, int hk1Weight = 1, int hk2Weight = 2);

        // Chuẩn hóa hàm đánh giá học lực & điều kiện Bí tích
        (string AcademicRank, bool IsEligibleForSacrament) EvaluateStudent(
            decimal? averageScore,
            double combinedAttendanceRate,
            string gradeLevel,
            int semester = 0);

        decimal CalculateAttendanceScore(
            int absentMassUnexcused, int absentMassExcused, int lateMass,
            int absentClassUnexcused, int absentClassExcused, int lateClass,
            AttendanceScoreRule rule);

        // Đồng bộ tham số thứ 3 (allColumnCodes = null) để không cần tạo 2 hàm overload
        decimal? EvaluateGradeFormula(
            string formulaExpression,
            Dictionary<string, decimal> scores,
            IEnumerable<string>? allColumnCodes = null);

        string DetermineClassification(decimal average, string evaluationRulesJson);
    }
}
