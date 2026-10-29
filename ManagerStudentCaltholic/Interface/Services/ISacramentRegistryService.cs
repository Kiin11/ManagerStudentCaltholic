using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface ISacramentRegistryService
    {
        /// <summary>
        /// Lấy tiến trình 4 chặng Bí tích (Rửa tội, Rước lễ, Thêm sức, Bao đồng) của một thiếu nhi
        /// </summary>
        Task<List<SacramentRecordItemDto>> GetStudentSacramentsAsync(long studentId);

        /// <summary>
        /// Thêm mới hoặc cập nhật (Upsert) thông tin Sổ bộ Bí tích, đồng bộ ngày Rửa tội / Thêm sức vào bảng Students
        /// </summary>
        Task<StudentSacramentRecord> AddOrUpdateSacramentRecordAsync(SaveSacramentRecordRequestDto dto, string operatorName);

        /// <summary>
        /// Xóa mềm một bản ghi Bí tích và đồng bộ lại cột tương ứng trên bảng Students
        /// </summary>
        Task<bool> DeleteSacramentRecordAsync(long recordId, long studentId, string operatorName);

        /// <summary>
        /// Kiểm tra và trả về danh sách các Bí tích còn thiếu so với độ tuổi hiện tại của thiếu nhi
        /// </summary>
        Task<List<MissingSacramentAlertDto>> GetMissingSacramentsAsync(long studentId, int currentAge);
    }
}