using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IGradeCalculationService
    {
        decimal? CalculateSemesterAverage(Dictionary<int, decimal?> scores, List<GradeConfiguration> configs);
        decimal? CalculateYearAverage(decimal? hk1Average, decimal? hk2Average, int hk1Weight = 1, int hk2Weight = 2);
        (string AcademicRank, bool IsEligibleForSacrament) EvaluateStudent(decimal? averageScore, decimal attendanceScore, string gradeLevel);

        decimal CalculateAttendanceScore(int absentMassUnexcused, int absentMassExcused, int lateMass,
                                         int absentClassUnexcused, int absentClassExcused, int lateClass,
                                         AttendanceScoreRule rule);
        decimal? EvaluateGradeFormula(string formulaExpression, Dictionary<string, decimal> scores);
        string DetermineClassification(decimal average, string evaluationRulesJson);
    }
}
