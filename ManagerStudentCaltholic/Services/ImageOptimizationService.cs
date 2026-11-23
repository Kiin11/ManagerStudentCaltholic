using ManagerStudentCaltholic.Interface.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ManagerStudentCaltholic.Services
{
    public class ImageOptimizationService : IImageOptimizationService
    {
        private const int MemoryThresholdBytes = 2 * 1024 * 1024; // 2 MB
        private const int MaxDimensionPixels = 10000;
        private const long MaxTotalPixels = 50_000_000L;

        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ImageOptimizationService> _logger;

        public ImageOptimizationService(IWebHostEnvironment env, ILogger<ImageOptimizationService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public bool IsSupportedImageExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            var normalized = extension.Trim();
            if (!normalized.StartsWith('.'))
            {
                normalized = "." + normalized;
            }

            return SupportedExtensions.Contains(normalized);
        }

        public async Task<(Stream OptimizedStream, string ContentType, string FileExtension, long FileSize)> OptimizeToWebpAsync(
            Stream inputStream,
            int maxWidth = 1600,
            int maxHeight = 1600,
            int quality = 78,
            CancellationToken ct = default)
        {
            if (inputStream == null)
            {
                throw new ArgumentNullException(nameof(inputStream));
            }

            if (maxWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWidth), "Chiều rộng tối đa phải lớn hơn 0.");
            }

            if (maxHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHeight), "Chiều cao tối đa phải lớn hơn 0.");
            }

            Stream workingStream = inputStream;
            bool ownsWorkingStream = false;

            try
            {
                if (!workingStream.CanSeek)
                {
                    var tempInputPath = Path.Combine(ResolveTempDirectory(), $"src_{Guid.NewGuid():N}.tmp");
                    var seekableCopy = new FileStream(
                        tempInputPath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 65536,
                        options: FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                    await inputStream.CopyToAsync(seekableCopy, 65536, ct);
                    await seekableCopy.FlushAsync(ct);
                    seekableCopy.Position = 0;
                    workingStream = seekableCopy;
                    ownsWorkingStream = true;
                }
                else
                {
                    if (workingStream.Length == 0)
                    {
                        throw new InvalidOperationException("Tệp hình ảnh trống.");
                    }

                    workingStream.Position = 0;
                }

                // 1. Kiểm tra magic bytes nghiêm ngặt (chỉ cho phép JPEG, PNG, WebP, GIF)
                await ValidateMagicBytesAsync(workingStream, ct);
                workingStream.Position = 0;

                // 2. Kiểm tra kích thước ảnh qua Image.IdentifyAsync trước khi giải mã toàn bộ pixel vào RAM
                ImageInfo? imageInfo;
                try
                {
                    imageInfo = await Image.IdentifyAsync(workingStream, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or ImageFormatException)
                {
                    _logger.LogWarning(ex, "Từ chối tệp hình ảnh không đọc được metadata.");
                    throw new InvalidOperationException("Tệp không phải là hình ảnh hợp lệ hoặc đã bị hỏng.", ex);
                }

                if (imageInfo == null
                    || imageInfo.Width <= 0
                    || imageInfo.Height <= 0
                    || imageInfo.Width > MaxDimensionPixels
                    || imageInfo.Height > MaxDimensionPixels
                    || (long)imageInfo.Width * imageInfo.Height > MaxTotalPixels)
                {
                    throw new InvalidOperationException(
                        $"Kích thước hình ảnh vượt quá giới hạn cho phép (tối đa {MaxDimensionPixels}x{MaxDimensionPixels} và {MaxTotalPixels:N0} pixels).");
                }

                workingStream.Position = 0;

                Image image;
                try
                {
                    image = await Image.LoadAsync(workingStream, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or ImageFormatException)
                {
                    _logger.LogWarning(ex, "Từ chối tệp hình ảnh không hợp lệ khi tối ưu WebP.");
                    throw new InvalidOperationException("Tệp không phải là hình ảnh hợp lệ hoặc đã bị hỏng.", ex);
                }

                using (image)
                {
                    if (image.Width <= 0 || image.Height <= 0)
                    {
                        throw new InvalidOperationException("Kích thước hình ảnh không hợp lệ.");
                    }

                    image.Mutate(ctx => ctx.AutoOrient());

                    if (image.Width > maxWidth || image.Height > maxHeight)
                    {
                        image.Mutate(ctx => ctx.Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new Size(maxWidth, maxHeight)
                        }));
                    }

                    // Xóa metadata EXIF/IPTC/XMP sau khi AutoOrient để giảm dung lượng và bảo vệ riêng tư
                    image.Metadata.ExifProfile = null;
                    image.Metadata.IptcProfile = null;
                    image.Metadata.XmpProfile = null;

                    var encoder = new WebpEncoder
                    {
                        Quality = Math.Clamp(quality, 1, 100),
                        FileFormat = WebpFileFormatType.Lossy
                    };

                    var tempDir = ResolveTempDirectory();
                    var tempPath = Path.Combine(tempDir, $"img_{Guid.NewGuid():N}.webp.tmp");
                    var tempStream = new FileStream(
                        tempPath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 65536,
                        options: FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                    try
                    {
                        await image.SaveAsWebpAsync(tempStream, encoder, ct);
                        await tempStream.FlushAsync(ct);

                        long fileSize = tempStream.Length;
                        tempStream.Position = 0;

                        if (fileSize <= MemoryThresholdBytes)
                        {
                            var memoryStream = new MemoryStream((int)fileSize);
                            await tempStream.CopyToAsync(memoryStream, 65536, ct);
                            await tempStream.DisposeAsync();
                            memoryStream.Position = 0;
                            return (memoryStream, "image/webp", ".webp", fileSize);
                        }

                        return (tempStream, "image/webp", ".webp", fileSize);
                    }
                    catch
                    {
                        await tempStream.DisposeAsync();
                        throw;
                    }
                }
            }
            finally
            {
                if (ownsWorkingStream)
                {
                    await workingStream.DisposeAsync();
                }
            }
        }

        private static async Task ValidateMagicBytesAsync(Stream stream, CancellationToken ct)
        {
            byte[] header = new byte[12];
            int totalRead = 0;
            while (totalRead < header.Length)
            {
                int read = await stream.ReadAsync(header.AsMemory(totalRead, header.Length - totalRead), ct);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead < 4)
            {
                throw new InvalidOperationException("Định dạng hình ảnh không hợp lệ (dữ liệu quá ngắn).");
            }

            // JPEG: FF D8 FF
            bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            // PNG: 89 50 4E 47
            bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;

            // GIF: 47 49 46 38 ("GIF8")
            bool isGif = header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38;

            // WebP: RIFF....WEBP
            bool isWebp = totalRead >= 12
                && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

            if (!isJpeg && !isPng && !isGif && !isWebp)
            {
                throw new InvalidOperationException("Định dạng hình ảnh không được hỗ trợ. Chỉ chấp nhận JPEG, PNG, WebP hoặc GIF.");
            }
        }

        private string ResolveTempDirectory()
        {
            var preferred = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "PrivateUploads", "cloud_tmp"));
            try
            {
                Directory.CreateDirectory(preferred);
                return preferred;
            }
            catch
            {
                var fallback = Path.GetTempPath();
                Directory.CreateDirectory(fallback);
                return fallback;
            }
        }
    }
}
