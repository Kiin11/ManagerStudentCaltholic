using System.Security.Claims;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IMassCampaignService
    {
        (bool IsEligible, string SacramentBadge) EvaluateSacramentClassEligibility(string? className, string? gradeLevel);

        Task<bool> CanUserManageClassCampaignAsync(ClaimsPrincipal user, int classRoomId);

        Task<MassCampaignDashboardViewModel> BuildDashboardViewModelAsync(
            ClaimsPrincipal user,
            int? classId = null,
            long? campaignId = null);

        Task<(bool Success, string Message, MassCampaign? Campaign)> CreateCampaignAsync(
            CreateMassCampaignRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress);

        Task<(bool Success, string Message)> UpdateCampaignConfigAsync(
            UpdateMassCampaignConfigDto dto,
            ClaimsPrincipal user,
            string ipAddress);

        Task<(bool Success, string Message, StudentCampaignRowDto? UpdatedRow)> UpdateSingleCellAsync(
            UpdateCampaignCellRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress);

        Task<(bool Success, string Message, int UpdatedCount)> BulkMarkDayAsync(
            BulkMarkCampaignDayRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress);

        Task<(bool Success, string Message, object? StudentInfo)> RecordQrScanAsync(
            CampaignQrScanRequestDto dto,
            ClaimsPrincipal user,
            string ipAddress);

        Task<byte[]> ExportCampaignMatrixToExcelAsync(long campaignId, ClaimsPrincipal user);
    }
}