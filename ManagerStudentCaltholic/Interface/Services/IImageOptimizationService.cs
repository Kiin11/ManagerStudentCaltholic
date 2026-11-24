namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IImageOptimizationService
    {
        Task<(Stream OptimizedStream, string ContentType, string FileExtension, long FileSize)> OptimizeToWebpAsync(
            Stream inputStream,
            int maxWidth = 1600,
            int maxHeight = 1600,
            int quality = 78,
            CancellationToken ct = default);

        bool IsSupportedImageExtension(string extension);
    }
}
