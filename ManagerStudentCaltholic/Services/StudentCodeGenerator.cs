using System.Text.RegularExpressions;
using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class StudentCodeGenerator : IStudentCodeGenerator
    {
        private readonly ParishDbContext _context;
        private static readonly Regex ValidManualCodeRegex = new(@"^[A-Z0-9_-]{3,20}$", RegexOptions.Compiled);

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
                return $"TN{DateTime.UtcNow:yy}";
            }

            var clean = VietnameseStringHelper.RemoveDiacritics(className)
                .ToUpperInvariant()
                .Trim();

            var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => Regex.Replace(w, @"[^A-Z0-9]", ""))
                .Where(w => !string.IsNullOrEmpty(w))
                .ToArray();

            if (words.Length == 0)
            {
                return $"TN{DateTime.UtcNow:yy}";
            }

            if (words.Length == 1)
            {
                return words[0];
            }

            // Nếu từ cuối chỉ có 1 chữ cái (VD: "Khai Tâm 1 A") và từ kế cuối là số ("1"), gộp thành "1A"
            if (words.Length >= 3 &&
                words[^1].Length == 1 && char.IsLetter(words[^1][0]) &&
                words[^2].All(char.IsDigit))
            {
                var initialsMerged = string.Concat(words.Take(words.Length - 2).Select(w => w[0]));
                return $"{initialsMerged}{words[^2]}{words[^1]}";
            }

            var initials = string.Concat(words.Take(words.Length - 1).Select(w => w[0]));
            var lastSegment = words[^1];

            return $"{initials}{lastSegment}";
        }

        public async Task<(bool IsUnique, string NormalizedCode, string? ConflictMessage)> ValidateCodeUniquenessAsync(
            string manualCode,
            long? excludeStudentId = null)
        {
            if (string.IsNullOrWhiteSpace(manualCode))
            {
                return (false, string.Empty, "Mã thiếu nhi không được để trống.");
            }

            var normalized = manualCode.Trim().ToUpperInvariant();
            if (!ValidManualCodeRegex.IsMatch(normalized))
            {
                return (false, normalized, "Mã thiếu nhi chỉ được chứa chữ cái, chữ số, dấu gạch ngang hoặc gạch dưới (từ 3 đến 20 ký tự).");
            }

            // Dùng IgnoreQueryFilters() để kiểm tra cả các hồ sơ đã xóa mềm
            var query = _context.Students
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.StudentCode.ToUpper() == normalized);

            if (excludeStudentId.HasValue && excludeStudentId.Value > 0)
            {
                query = query.Where(s => s.Id != excludeStudentId.Value);
            }

            var existingStudent = await query
                .Select(s => new { s.StudentCode, s.ChristianName, s.FirstName, s.LastName, s.IsDeleted })
                .FirstOrDefaultAsync();

            if (existingStudent != null)
            {
                var statusNote = existingStudent.IsDeleted ? " (hồ sơ đã lưu trữ/xóa mềm)" : "";
                return (false, normalized, $"Mã '{normalized}' đã được cấp cho em {existingStudent.ChristianName} {existingStudent.FirstName} {existingStudent.LastName}{statusNote}.");
            }

            return (true, normalized, null);
        }

        public async Task<string> GenerateOrValidateStudentCodeAsync(
            string? className,
            string? manualCode = null,
            long? excludeStudentId = null)
        {
            if (!string.IsNullOrWhiteSpace(manualCode))
            {
                var (isUnique, normalized, conflictMsg) = await ValidateCodeUniquenessAsync(manualCode, excludeStudentId);
                if (!isUnique)
                {
                    throw new InvalidOperationException(conflictMsg ?? $"Mã thiếu nhi '{normalized}' đã tồn tại.");
                }
                return normalized;
            }

            var prefix = BuildClassPrefix(className);
            var existingCodes = await _context.Students
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.StudentCode.ToUpper().StartsWith(prefix))
                .Select(s => s.StudentCode)
                .ToListAsync();

            // Chỉ lấy những mã có phần đuôi sau prefix hoàn toàn là chữ số (tránh nhầm RL2 với RL2A)
            var seqRegex = new Regex($"^{Regex.Escape(prefix)}(\\d+)$", RegexOptions.IgnoreCase);
            int maxSeq = 0;

            foreach (var code in existingCodes)
            {
                var match = seqRegex.Match(code.Trim());
                if (match.Success && int.TryParse(match.Groups[1].Value, out int seq) && seq > maxSeq)
                {
                    maxSeq = seq;
                }
            }

            return $"{prefix}{(maxSeq + 1):D3}";
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
    }
}