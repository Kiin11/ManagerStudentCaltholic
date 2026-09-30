using ManagerStudentCaltholic.Interface.Services;
using QRCoder;

namespace ManagerStudentCaltholic.Services
{
    public class QrCodeService : IQrCodeService
    {
        public byte[] GenerateQrCodePng(string text, int pixelsPerModule = 10)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<byte>();

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);

            return qrCode.GetGraphic(pixelsPerModule);
        }

        public string GenerateQrCodeBase64(string text, int pixelsPerModule = 5)
        {
            var bytes = GenerateQrCodePng(text, pixelsPerModule);
            if (bytes.Length == 0) return string.Empty;

            var base64 = Convert.ToBase64String(bytes);
            return $"data:image/png;base64,{base64}";
        }
    }
}
