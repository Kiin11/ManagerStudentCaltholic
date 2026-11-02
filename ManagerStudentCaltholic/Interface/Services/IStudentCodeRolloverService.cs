using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IStudentCodeRolloverService
    {
        /// <summary>
        /// Rút trích hậu tố năm học ngắn gọn: "2025-2026" -> "2526"
        /// </summary>
        string BuildAcademicYearSuffix(string? academicYearName);

        /// <summary>
        /// Gắn đuôi năm học cũ vào StudentCode: ("TS3A001", "2025-2026") -> "TS3A001_2526"
        /// </summary>
        string AppendOldYearSuffix(string currentCode, string? oldAcademicYearName);

        /// <summary>
        /// Thực hiện chuyển đổi StudentCode khi đổi năm học (gắn đuôi năm học cũ, lưu lịch sử và tùy chọn sinh mã lớp mới)
        /// </summary>
        Task<List<StudentCodeTransitionResultDto>> ProcessBatchRolloverCodesAsync(
            int sourceAcademicYearId,
            int targetAcademicYearId,
            int sourceClassId,
            List<StudentRolloverCodeDecisionItem> decisions,
            bool assignNewClassCodeAfterSuffix,
            string operatorName);

        /// <summary>
        /// Tìm hồ sơ thiếu nhi theo mã hiện tại hoặc mã lịch sử năm cũ (phục vụ Quét QR & Tra cứu)
        /// </summary>
        Task<(Student? Student, bool IsHistoricalCode, string? MatchedOldCode)> ResolveStudentByAnyCodeAsync(string inputCode);
    }
}