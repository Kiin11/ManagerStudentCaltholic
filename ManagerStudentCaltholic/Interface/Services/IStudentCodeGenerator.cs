namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IStudentCodeGenerator
    {
        /// <summary>
        /// Tách tiền tố mã từ tên lớp: "Thêm Sức 3A" -> "TS3A"
        /// </summary>
        string BuildClassPrefix(string? className);

        /// <summary>
        /// Sinh mã thiếu nhi mới theo tên lớp (hoặc kiểm tra tính duy nhất nếu có truyền manualCode)
        /// </summary>
        Task<string> GenerateOrValidateStudentCodeAsync(
            string? className,
            string? manualCode = null,
            long? excludeStudentId = null);

        /// <summary>
        /// Sinh mã theo ClassRoomId (tự động tra tên lớp từ DB)
        /// </summary>
        Task<string> GenerateByClassIdAsync(int? classId, string? manualCode = null);

        /// <summary>
        /// Kiểm tra mã thiếu nhi có duy nhất trong toàn hệ thống hay không
        /// </summary>
        Task<(bool IsUnique, string NormalizedCode, string? ConflictMessage)> ValidateCodeUniquenessAsync(
            string manualCode,
            long? excludeStudentId = null);

        /// <summary>
        /// Hàm tương thích ngược với các luồng cũ không truyền tên lớp
        /// </summary>
        Task<string> GenerateUniqueCodeAsync();
    }
}