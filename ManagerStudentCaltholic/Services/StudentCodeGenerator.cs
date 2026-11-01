using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class StudentCodeGenerator : IStudentCodeGenerator
    {
        private readonly ParishDbContext _context;

        public StudentCodeGenerator(ParishDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// FEAT-P4-03: Công thức tạo tiền tố mã từ tên lớp:
        /// Lấy chữ cái đầu của các từ đứng trước + cụm từ cuối cùng (đã bỏ dấu tiếng Việt).
        /// Ví dụ:
        /// - "Thêm Sức 3A" -> "T" + "S" + "3A" = "TS3A"
        /// - "Rước Lễ 2B"  -> "R" + "L" + "2B" = "RL2B"
        /// - "Khai Tâm 1"  -> "K" + "T" + "1"  = "KT1"
        /// - "Dự Bị 4A"    -> "D" + "B" + "4A" = "DB4A"
        /// </summary>
        public string BuildClassPrefix(string? className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return $"TN{DateTime.UtcNow.AddHours(7):yy}";
            }

            var ascii = RemoveDiacritics(className).ToUpperInvariant();

            // Loại bỏ từ "LOP" ở đầu nếu người dùng đặt tên lớp là "Lớp Thêm Sức 3A"
            ascii = Regex.Replace(ascii, @"^\s*LOP\s+", "", RegexOptions.IgnoreCase);

            // Tách các cụm chữ-số
            var tokens = Regex.Matches(ascii, @"[A-Z0-9]+")
                .Select(m => m.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (tokens.Count == 0)
                return $"TN{DateTime.UtcNow.AddHours(7):yy}";

            if (tokens.Count == 1)
                return tokens[0];

            var initials = string.Concat(tokens.Take(tokens.Count - 1).Select(w => w[0]));
            var lastSegment = tokens.Last();

            return $"{initials}{lastSegment}";
        }

        public async Task<(bool IsUnique, string NormalizedCode, string? ConflictMessage)> ValidateCodeUniquenessAsync(
            string manualCode,
            long? excludeStudentId = null)
        {
            var normalized = NormalizeManualCode(manualCode);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return (false, string.Empty, "Mã thiếu nhi không được để trống.");
            }

            if (!Regex.IsMatch(normalized, @"^[A-Z0-9\-_]{3,30}$"))
            {
                return (false, normalized, "Mã thiếu nhi chỉ được chứa chữ cái, chữ số, dấu gạch ngang (-) hoặc gạch dưới (_), độ dài 3-30 ký tự.");
            }

            // IgnoreQueryFilters() để kiểm tra cả các hồ sơ đã xóa mềm tránh đụng Unique Index trong PostgreSQL
            var existsInStudents = await _context.Students
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(s => s.StudentCode.ToUpper() == normalized
                            && (!excludeStudentId.HasValue || s.Id != excludeStudentId.Value));

            if (existsInStudents)
            {
                return (false, normalized, $"Mã thiếu nhi '{normalized}' đã tồn tại trong hồ sơ hệ thống.");
            }

            return (true, normalized, null);
        }

        public async Task<string> GenerateOrValidateStudentCodeAsync(
            string? className,
            string? manualCode = null,
            long? excludeStudentId = null)
        {
            // 1. Nếu có truyền mã thủ công -> kiểm tra tính duy nhất rồi trả về
            if (!string.IsNullOrWhiteSpace(manualCode))
            {
                var (isUnique, normalized, errorMsg) = await ValidateCodeUniquenessAsync(manualCode, excludeStudentId);
                if (!isUnique)
                {
                    throw new InvalidOperationException(errorMsg ?? $"Mã thiếu nhi '{normalized}' đã tồn tại.");
                }
                return normalized;
            }

            // 2. Nếu không truyền mã -> sinh tự động theo công thức tên lớp: T + S + 3A + STT
            var prefix = BuildClassPrefix(className);

            var existingCodes = await _context.Students
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.StudentCode.ToUpper().StartsWith(prefix))
                .Select(s => s.StudentCode)
                .ToListAsync();

            int maxSeq = ExtractMaxSequenceForPrefix(existingCodes, prefix);

            string candidate;
            int nextSeq = maxSeq + 1;
            do
            {
                candidate = $"{prefix}{nextSeq:D3}";
                nextSeq++;
            }
            while (existingCodes.Any(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase)));

            return candidate;
        }

        public async Task<string> GenerateByClassIdAsync(int? classId, string? manualCode = null)
        {
            string? className = null;
            if (classId.HasValue && classId.Value > 0)
            {
                className = await _context.Classes
                    .AsNoTracking()
                    .Where(c => c.Id == classId.Value)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();
            }

            return await GenerateOrValidateStudentCodeAsync(className, manualCode);
        }

        public async Task<string> GenerateUniqueCodeAsync()
        {
            return await GenerateOrValidateStudentCodeAsync(null, null);
        }

        public static int ExtractMaxSequenceForPrefix(IEnumerable<string> codes, string prefix)
        {
            int maxSeq = 0;
            foreach (var rawCode in codes)
            {
                if (string.IsNullOrWhiteSpace(rawCode)) continue;
                var code = rawCode.Trim().ToUpperInvariant();
                if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                // Bỏ qua các mã đã gắn đuôi năm học cũ (VD: TS3A001_2425 hoặc TS3A001-2024)
                var remainder = code.Substring(prefix.Length);
                if (int.TryParse(remainder, out int seq) && seq > maxSeq)
                {
                    maxSeq = seq;
                }
            }
            return maxSeq;
        }

        private static string NormalizeManualCode(string input)
        {
            var noDiacritics = RemoveDiacritics(input ?? string.Empty);
            return Regex.Replace(noDiacritics.Trim().ToUpperInvariant(), @"\s+", "");
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }
    }
}