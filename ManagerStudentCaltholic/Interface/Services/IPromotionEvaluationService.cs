using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IPromotionEvaluationService
    {
        Task<PromotionRuleConfig> GetEffectiveRuleAsync(int academicYearId, string gradeLevel);
        Task<List<StudentPromotionEvalItem>> EvaluateClassEligibilityAsync(int sourceClassId, int targetYearId);
        (string NextGradeLevel, string NextClassPrefix, bool IsGraduating) ResolveProgressionTarget(string currentClassName, string currentGradeLevel, string? configuredNextGrade);
    }
}