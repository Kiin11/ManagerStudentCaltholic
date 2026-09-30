namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IQrCodeService
    {
        /// <summary>
        /// Sinh mảng byte hình ảnh PNG của mã QR
        /// </summary>
        byte[] GenerateQrCodePng(string text, int pixelsPerModule = 10);

        /// <summary>
        /// Sinh chuỗi data URL base64 để nhúng thẳng vào thẻ <img src="...">
        /// </summary>
        string GenerateQrCodeBase64(string text, int pixelsPerModule = 5);
    }
}
