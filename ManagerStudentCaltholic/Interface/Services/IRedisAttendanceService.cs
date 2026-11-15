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
        /// TASK-P3-12.3 / P2-LOG-16: Lưu nhanh Sổ điểm danh hàng loạt lên Redis (High-Throughput), kết hợp đồng bộ Write-Through trực tiếp xuống PostgreSQL tại Controller.
        /// </summary>
        Task<(bool Success, string Message)> SaveBatchToRedisAsync(AttendanceBatchSubmitDto request);

        /// <summary>
        /// TASK-P3-12.3: Xử lý quét mã QR tốc độ cao trực tiếp trên Redis
        /// </summary>
        Task<QrScanResponseDto> ProcessQrCheckInViaRedisAsync(QrScanRequestDto request);

        /// <summary>
        /// TASK-P5-04.3: Hoàn tác lượt quét QR trong vòng 5 giây
        /// </summary>
        Task<(bool Success, string Message)> UndoScanCheckInAsync(UndoScanRequestDto dto);

        /// <summary>
        /// TASK-P3-12.4 / P2-LOG-16: Đồng bộ hàng loạt (Batch Upsert) dữ liệu điểm danh từ Redis về PostgreSQL (được gọi Write-Through trực tiếp ở ScanCheckIn/SaveBatch và chạy dự phòng mỗi 10 phút qua Hangfire).
        /// </summary>
        Task<(int ClassesSynced, int RecordsUpserted)> SyncDirtyAttendancesToDatabaseAsync(DateTime? targetDate = null);

        /// <summary>
        /// P2-LOG-17: Tự động tạo bản ghi vắng mặt (ABSENT_UNPERMITTED / ABSENT_PERMITTED) xuống DB
        /// cho các học sinh không quét QR / chưa được điểm danh trong ngày Thứ Năm hoặc Chúa Nhật
        /// </summary>
        Task<int> AutoMarkAbsentUncheckedStudentsAsync(DateTime? targetDate = null);
    }
}