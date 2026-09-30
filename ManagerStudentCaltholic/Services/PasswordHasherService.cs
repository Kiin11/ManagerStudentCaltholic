using ManagerStudentCaltholic.Interface.Services;
using System.Security.Cryptography;

namespace ManagerStudentCaltholic.Services
{
    public class PasswordHasherService : IPasswordHasherService
    {
        private const int SaltSize = 16;      // 128 bit
        private const int KeySize = 32;       // 256 bit
        private const int Iterations = 100000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
        private const char SegmentDelimiter = ':';

        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Mật khẩu không được để trống.", nameof(password));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                Algorithm,
                KeySize
            );

            // Định dạng: {Iterations}:{SaltBase64}:{HashBase64}
            return string.Join(
                SegmentDelimiter,
                Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash)
            );
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashedPassword))
                return false;

            string[] segments = hashedPassword.Split(SegmentDelimiter);
            if (segments.Length != 3)
                return false;

            if (!int.TryParse(segments[0], out int iterations))
                return false;

            byte[] salt;
            byte[] hash;
            try
            {
                salt = Convert.FromBase64String(segments[1]);
                hash = Convert.FromBase64String(segments[2]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] testHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                Algorithm,
                hash.Length
            );

            // So sánh thời gian hằng số để ngăn Timing Attack
            return CryptographicOperations.FixedTimeEquals(hash, testHash);
        }
    }
}