using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.ViewModels;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IRedisAttendanceService
    {
        /// <summary>
        /// TASK-P3-12.2: Nạp trước toàn bộ danh sách học sinh, lịch học & bài giảng của tất cả các lớp trong ngày lên Redis
        /// </summary>
        Task<int> PreWarmDailyAttendanceAndTasksAsync(DateTime? targetDate = null);

        /// <summary>
        /// TASK-P3-12.3: Lấy Sổ điểm danh lớp từ Redis (Nếu chưa có sẽ tự động nạp từ DB lên Redis)
        /// </summary>
        Task<ClassAttendanceSheetViewModel> GetOrLoadClassSheetAsync(int classId, DateTime date);

        /// <summary>
        /// TASK-P3-12.3: Lấy thông tin Bài học, Phòng học & Checklist nhiệm vụ trong giờ học từ Redis
        /// </summary>
        Task<ClassSessionContextDto> GetOrLoadSessionContextAsync(int classId, DateTime date);

        /// <summary>
        /// TASK-P3-12.3: Lưu nhanh Sổ điểm danh hàng loạt trực tiếp lên Redis (High-Throughput)
        /// </summary>
        Task<(bool Success, string Message)> SaveBatchToRedisAsync(AttendanceBatchSubmitDto request);

        /// <summary>
        /// TASK-P3-12.3: Xử lý quét mã QR tốc độ cao trực tiếp trên Redis
        /// </summary>
        Task<QrScanResponseDto> ProcessQrCheckInViaRedisAsync(QrScanRequestDto request);

        /// <summary>
        /// TASK-P3-12.4: Đồng bộ hàng loạt (Batch Upsert) toàn bộ dữ liệu điểm danh đã thay đổi trên Redis về PostgreSQL
        /// </summary>
        Task<(int ClassesSynced, int RecordsUpserted)> SyncDirtyAttendancesToDatabaseAsync(DateTime? targetDate = null);
    }
}