using ManagerStudentCaltholic.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IClassCurriculumService
    {
        /// <summary>
        /// Lấy thông tin kế hoạch bài giảng hiện hành của lớp theo niên khóa
        /// </summary>
        Task<ClassCurriculumDocumentDto?> GetCurriculumByClassAsync(int classRoomId, int academicYearId);

        /// <summary>
        /// Lấy danh sách lịch sử các phiên bản ngày sửa của kế hoạch lớp
        /// </summary>
        Task<List<ClassCurriculumVersionDto>> GetVersionsAsync(long documentId);

        /// <summary>
        /// Lấy chi tiết nội dung tuần học của một phiên bản cụ thể
        /// </summary>
        Task<ClassCurriculumVersionDto?> GetVersionDetailAsync(long versionId);

        /// <summary>
        /// Bộ đọc nạp file kế hoạch (.xlsx, .docx, .csv, .md, .pdf) chuyển thành danh sách tuần học
        /// </summary>
        Task<CurriculumParseResultDto> ParseUploadedCurriculumFileAsync(IFormFile file);

        /// <summary>
        /// Lưu kế hoạch lớp trực tuyến, tự động sinh file Excel đính kèm theo version ngày sửa và đồng bộ ClassLessonPlans
        /// </summary>
        Task<(bool Success, string Message, ClassCurriculumDocumentDto? Data)> SaveCurriculumAsync(SaveCurriculumRequestDto request, long userId, string userName);

        /// <summary>
        /// Khôi phục (Rollback) nội dung kế hoạch về phiên bản ngày sửa cũ
        /// </summary>
        Task<(bool Success, string Message, ClassCurriculumDocumentDto? Data)> RollbackCurriculumAsync(long documentId, long versionId, long userId, string userName);

        /// <summary>
        /// Xuất file Excel bảng kế hoạch lớp của tài liệu hiện hành hoặc phiên bản chỉ định
        /// </summary>
        Task<(byte[] FileBytes, string ContentType, string DownloadFileName)> ExportCurriculumToExcelAsync(long documentId, long? versionId = null);

        /// <summary>
        /// Tải file đính kèm được lưu trữ của một phiên bản
        /// </summary>
        Task<(byte[] FileBytes, string ContentType, string DownloadFileName)?> GetVersionFileAsync(long versionId);
    }
}
