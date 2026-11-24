using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IStaffStudentLookupService
    {
        Task<List<StaffStudentQuickSearchResultDto>> SearchStudentsQuickAsync(string keyword, int limit = 8);
        Task<List<StaffStudentQuickSearchResultDto>> SearchStudentsQuickAsync(string keyword, ClaimsPrincipal caller, int limit = 8);
        Task<bool> CanCallerAccessStudentAsync(ClaimsPrincipal caller, long studentId);
        Task<StaffStudent360DetailDto?> GetStudent360DetailAsync(long studentId, string selectedOption = "OVERVIEW");
        Task RecordLookupLogAsync(long userId, long studentId, string searchTerm, string selectedOption, string? ipAddress);
        Task<List<RecentLookupItemDto>> GetRecentLookupsAsync(long userId, int limit = 5);
        Task<List<RecentLookupItemDto>> GetRecentLookupsAsync(long userId, ClaimsPrincipal caller, int limit = 5);
        Task<List<LookupViewOption>> GetViewOptionsAsync();
    }
}
