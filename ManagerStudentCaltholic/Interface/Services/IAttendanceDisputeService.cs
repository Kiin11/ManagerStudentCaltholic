using System.Security.Claims;
using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IAttendanceDisputeService
    {
        Task<(bool Success, string Message)> SubmitDisputeFromParentAsync(
            SubmitAttendanceDisputeRequestDto dto,
            string ipAddress);

        Task<int> GetPendingDisputeCountForClassAsync(int classId);

        Task<List<object>> GetDisputesForClassAsync(
            int classId,
            string statusFilter,
            ClaimsPrincipal user);

        Task<(bool Success, string Message)> ReviewDisputeByTeacherAsync(
            ReviewAttendanceDisputeRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress);
    }
}