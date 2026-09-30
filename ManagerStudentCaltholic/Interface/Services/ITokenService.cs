using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{

    public interface ITokenService
    {
        string GenerateAccessToken(User user);
        RefreshToken GenerateRefreshToken(long userId, string? ipAddress);
        Task<(bool Success, string? AccessToken, RefreshToken? NewRefreshToken, string Message)> RotateRefreshTokenAsync(string token, string? ipAddress);
        Task<bool> RevokeTokenAsync(string token, string? ipAddress);
    }
}
