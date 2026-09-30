using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ManagerStudentCaltholic.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ParishDbContext _context;
        private readonly ILogger<TokenService> _logger;

        public TokenService(IConfiguration configuration, ParishDbContext context, ILogger<TokenService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public string GenerateAccessToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "DefaultFallbackSecretKey12345678901234567890";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            var expireMinutes = int.TryParse(jwtSettings["AccessTokenExpirationMinutes"], out int exp) ? exp : 30;

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(securityToken);
        }

        public RefreshToken GenerateRefreshToken(long userId, string? ipAddress)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var expireDays = int.TryParse(jwtSettings["RefreshTokenExpirationDays"], out int days) ? days : 7;

            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            return new RefreshToken
            {
                UserId = userId,
                Token = Convert.ToBase64String(randomBytes),
                ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }

        public async Task<(bool Success, string? AccessToken, RefreshToken? NewRefreshToken, string Message)> RotateRefreshTokenAsync(string token, string? ipAddress)
        {
            var existingToken = await _context.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == token);

            if (existingToken == null)
            {
                return (false, null, null, "Refresh Token không tồn tại.");
            }

            // PHÁT HIỆN TẤN CÔNG THỬ LẠI (Token Replay Attack):
            // Nếu token đã bị thu hồi trước đó mà vẫn gửi lại, lập tức thu hồi toàn bộ token của tài khoản này
            if (existingToken.RevokedAt != null)
            {
                _logger.LogWarning("Phát hiện Token Replay Attack từ User ID {UserId}, IP: {Ip}", existingToken.UserId, ipAddress);

                var compromisedTokens = await _context.RefreshTokens
                    .Where(r => r.UserId == existingToken.UserId && r.RevokedAt == null)
                    .ToListAsync();

                foreach (var t in compromisedTokens)
                {
                    t.RevokedAt = DateTime.UtcNow;
                    t.RevokedByIp = ipAddress;
                    t.ReplacedByToken = "REVOKED_DUE_TO_SUSPICIOUS_REPLAY";
                }

                await _context.SaveChangesAsync();
                return (false, null, null, "Phát hiện phiên truy cập đáng ngờ. Toàn bộ phiên đăng nhập đã bị vô hiệu hóa vì lý do an toàn.");
            }

            if (!existingToken.IsActive)
            {
                return (false, null, null, "Refresh Token đã hết hạn.");
            }

            if (!existingToken.User.IsActive)
            {
                return (false, null, null, "Tài khoản người dùng đã bị vô hiệu hóa.");
            }

            // Tạo cặp token mới
            var newRefreshToken = GenerateRefreshToken(existingToken.UserId, ipAddress);

            // Thu hồi token cũ và liên kết với token mới
            existingToken.RevokedAt = DateTime.UtcNow;
            existingToken.RevokedByIp = ipAddress;
            existingToken.ReplacedByToken = newRefreshToken.Token;

            _context.RefreshTokens.Add(newRefreshToken);
            await _context.SaveChangesAsync();

            var newAccessToken = GenerateAccessToken(existingToken.User);
            return (true, newAccessToken, newRefreshToken, "Làm mới phiên thành công.");
        }

        public async Task<bool> RevokeTokenAsync(string token, string? ipAddress)
        {
            var existingToken = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
            if (existingToken == null || !existingToken.IsActive)
                return false;

            existingToken.RevokedAt = DateTime.UtcNow;
            existingToken.RevokedByIp = ipAddress;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
