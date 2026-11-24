using Microsoft.AspNetCore.Http;

namespace ManagerStudentCaltholic.Services
{
    /// <summary>
    /// Trợ giúp kiểm tra chữ ký Magic Bytes, ánh xạ MIME an toàn phía Server và chống Path Traversal cho tệp tải lên/xuống.
    /// </summary>
    public static class FileUploadSecurityHelper
    {
        private static readonly IReadOnlyDictionary<string, string> SafeMimeTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".pdf"] = "application/pdf",
                [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                [".doc"] = "application/msword",
                [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                [".xls"] = "application/vnd.ms-excel",
                [".csv"] = "text/csv",
                [".md"] = "text/markdown",
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".gif"] = "image/gif",
                [".webp"] = "image/webp"
            };

        public static bool HasValidPdfSignature(ReadOnlySpan<byte> header)
        {
            return header.Length >= 4
                && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46; // %PDF
        }

        public static bool HasValidZipOoxmlSignature(ReadOnlySpan<byte> header)
        {
            return header.Length >= 4
                && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04; // PK\x03\x04
        }

        public static bool HasValidOle2Signature(ReadOnlySpan<byte> header)
        {
            return header.Length >= 8
                && header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0
                && header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1;
        }

        public static bool HasValidImageSignature(ReadOnlySpan<byte> header, string extension)
        {
            var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => header.Length >= 3
                    && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header.Length >= 8
                    && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                    && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
                ".gif" => header.Length >= 6
                    && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38
                    && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61,
                ".webp" => header.Length >= 12
                    && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
                _ => false
            };
        }

        /// <summary>
        /// Kiểm tra Magic Bytes của Stream và khôi phục Position ban đầu nếu Stream hỗ trợ CanSeek.
        /// </summary>
        public static async Task<bool> ValidateStreamMagicBytesAsync(
            Stream stream,
            string extension,
            CancellationToken cancellationToken = default)
        {
            if (stream == null || !stream.CanRead)
                return false;

            var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                return false;

            long originalPosition = 0;
            bool canSeek = stream.CanSeek;
            if (canSeek)
            {
                originalPosition = stream.Position;
            }

            try
            {
                int bufferSize = ext is ".csv" or ".md" ? 512 : 16;
                byte[] buffer = new byte[bufferSize];
                int totalRead = 0;
                while (totalRead < bufferSize)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(totalRead, bufferSize - totalRead), cancellationToken);
                    if (read == 0) break;
                    totalRead += read;
                }

                if (totalRead == 0)
                    return false;

                return ValidateHeaderBytes(buffer, totalRead, ext);
            }
            finally
            {
                if (canSeek && stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }

        public static bool ValidateHeaderBytes(byte[] buffer, int length, string extension)
        {
            var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
            ReadOnlySpan<byte> span = buffer.AsSpan(0, Math.Min(length, buffer.Length));
            return ext switch
            {
                ".pdf" => HasValidPdfSignature(span),
                ".xlsx" or ".docx" => HasValidZipOoxmlSignature(span),
                ".xls" or ".doc" => HasValidOle2Signature(span),
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => HasValidImageSignature(span, ext),
                ".csv" or ".md" => span.IndexOf((byte)0) < 0,
                _ => false
            };
        }

        public static async Task<bool> ValidateFormFileMagicBytesAsync(
            IFormFile? file,
            string extension,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
                return false;

            await using var stream = file.OpenReadStream();
            return await ValidateStreamMagicBytesAsync(stream, extension, cancellationToken);
        }

        /// <summary>
        /// Trả về MIME type theo allow-list phía Server, không bao giờ tin Content-Type từ client.
        /// </summary>
        public static string GetSafeMimeType(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return "application/octet-stream";

            var ext = extension.Trim();
            if (!ext.StartsWith('.'))
                ext = "." + ext;

            return SafeMimeTypes.TryGetValue(ext, out var mime)
                ? mime
                : "application/octet-stream";
        }

        /// <summary>
        /// Phân giải đường dẫn tệp và xác nhận nằm tuyệt đối bên trong thư mục gốc, chống Path Traversal (`..`, `\0`).
        /// </summary>
        public static bool TryResolveContainedPath(string rootDirectory, string? relativeOrFileName, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(relativeOrFileName))
                return false;

            if (relativeOrFileName.Contains('\0'))
                return false;

            var normalizedRelative = relativeOrFileName.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedRelative))
                return false;

            var fullRoot = Path.GetFullPath(rootDirectory);
            var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;

            var candidate = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelative));
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                return false;

            fullPath = candidate;
            return true;
        }
    }
}
