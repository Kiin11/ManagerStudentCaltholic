using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    /// <summary>
    /// Service tổng hợp hồ sơ lịch sử kép (Học tập + Giảng dạy đa niên khóa) và 5 Cột mốc Đức tin (TASK-04.1).
    /// </summary>
    public interface IProfileHistoryService
    {
        Task<UserProfileViewModel?> BuildUserProfileAsync(long userId);
        Task<UserProfileViewModel?> BuildStudentProfileAsync(long studentId);
    }
}
