using System.Security.Claims;
using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IClassEvaluationService
    {
        ClassEvaluationActionDescriptor ResolveClassEvaluationAction(string? className, string? gradeLevel);

        Task<bool> CanUserEvaluateClassAsync(ClaimsPrincipal user, int classId);

        Task<ClassEvaluationActionDescriptor> GetClassEvaluationHeaderAsync(int classId);

        Task<ClassEvaluationSheetViewModel> BuildEvaluationSheetAsync(int classId);

        Task<(bool Success, string Message, long? BatchId)> SubmitClassEvaluationAsync(
            SubmitClassEvaluationRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress);
    }
}