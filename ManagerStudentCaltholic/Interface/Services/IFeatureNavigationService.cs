using System.Security.Claims;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IFeatureNavigationService
    {
        Task EnsureDefaultRulesSeededAsync();
        Task<MasterWorkspaceViewModel> GetAuthorizedWorkspaceAsync(ClaimsPrincipal user, string? activeMasterCode = "COMMON");
        Task<List<FeaturePermissionRule>> GetAllRulesForManagementAsync();
        Task<(bool Success, string Message)> UpdatePermissionRulesBatchAsync(
            List<SaveFeaturePermissionItemDto> items,
            string updatedBy,
            string ipAddress);
    }
}