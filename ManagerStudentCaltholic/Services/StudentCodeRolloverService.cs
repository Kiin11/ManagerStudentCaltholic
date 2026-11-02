using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ManagerStudentCaltholic.Services
{
    public class StudentCodeRolloverService : IStudentCodeRolloverService
    {
        private readonly ParishDbContext _context;
        private readonly IStudentCodeGenerator _codeGenerator;
        private readonly ILogger<StudentCodeRolloverService> _logger;

        public StudentCodeRolloverService(
            ParishDbContext context,
            IStudentCodeGenerator codeGenerator,
            ILogger<StudentCodeRolloverService> logger)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _logger = logger;
        }

        /// <summary>
        /// Rút trích hậu tố 4 chữ số từ tên niên khóa:
        /// "2025-2026" -> "2526" | "2026 - 2027" -> "2627"
        /// </summary>
        public string BuildAcademicYearSuffix(string? academicYearName)
        {
            if (string.IsNullOrWhiteSpace(academicYearName))
            {
                var yr = DateTime.UtcNow.Year;
                return $"{(yr - 1) % 100:D2}{yr % 100:D2}";
            }

            var matches = Regex.Matches(academicYearName, @"\d{4}");
            if (matches.Count >= 2 &&
                int.TryParse(matches[0].Value, out int startYear) &&
                int.TryParse(matches[1].Value, out int endYear))
            {
                return $"{startYear % 100:D2}{endYear % 100:D2}";
            }

            if (matches.Count == 1 && int.TryParse(matches[0].Value, out int singleYear))
            {
                return $"{singleYear % 100:D2}{(singleYear + 1) % 100:D2}";
            }

            var digits = new string(academicYearName.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : $"{DateTime.UtcNow.Year % 100:D2}";
        }

        /// <summary>
        /// Gắn đuôi năm học cũ vào mã thiếu nhi: ("TS3A001", "2025-2026") -> "TS3A001_2526"
        /// Nếu mã đã có đuôi "_2526" thì giữ nguyên không gắn lặp.
        /// </summary>
        public string AppendOldYearSuffix(string currentCode, string? oldAcademicYearName)
        {
            var cleanCode = (currentCode ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(cleanCode))
                return string.Empty;

            var suffix = BuildAcademicYearSuffix(oldAcademicYearName);
            var suffixToken = $"_{suffix}";

            if (cleanCode.EndsWith(suffixToken, StringComparison.OrdinalIgnoreCase))
            {
                return cleanCode;
            }

            // Nếu mã đã có hậu tố năm cũ khác (VD: _2425), cắt hậu tố cũ trước khi gắn hậu tố năm vừa học
            var baseCode = Regex.Replace(cleanCode, @"_\d{4}$", "");
            return $"{baseCode}{suffixToken}";
        }

        public async Task<List<StudentCodeTransitionResultDto>> ProcessBatchRolloverCodesAsync(
            int sourceAcademicYearId,
            int targetAcademicYearId,
            int sourceClassId,
            List<StudentRolloverCodeDecisionItem> decisions,
            bool assignNewClassCodeAfterSuffix,
            string operatorName)
        {
            var results = new List<StudentCodeTransitionResultDto>();
            if (decisions == null || !decisions.Any())
                return results;

            var sourceYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == sourceAcademicYearId);

            var sourceClass = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == sourceClassId);

            var studentIds = decisions.Select(d => d.StudentId).Distinct().ToList();
            var students = await _context.Students
                .IgnoreQueryFilters()
                .Where(s => studentIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

            // Kiểm tra các mã đã tồn tại trong DB để đảm bảo không bao giờ trùng khi gắn hậu tố
            var allExistingCodes = await _context.Students
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(s => s.StudentCode.ToUpper())
                .ToListAsync();

            var usedCodesSet = new HashSet<string>(allExistingCodes, StringComparer.OrdinalIgnoreCase);

            // PHA 1: Gắn đuôi năm học cũ (_YYZZ) cho toàn bộ học sinh được kết chuyển
            // giúp giải phóng hoàn toàn dải mã lớp cũ (VD: TS2A001 -> TS2A001_2526)
            var pendingTransitions = new List<(Student Entity, StudentRolloverCodeDecisionItem Decision, string OriginalCode, string SuffixedCode)>();

            foreach (var dec in decisions)
            {
                if (!students.TryGetValue(dec.StudentId, out var student))
                    continue;

                var originalCode = student.StudentCode.Trim().ToUpperInvariant();
                var candidateSuffixed = AppendOldYearSuffix(originalCode, sourceYear?.Name);

                // Nếu mã gắn đuôi không đổi so với mã hiện tại, vẫn đảm bảo tính duy nhất
                if (!string.Equals(originalCode, candidateSuffixed, StringComparison.OrdinalIgnoreCase))
                {
                    usedCodesSet.Remove(originalCode);
                    int collisionIndex = 1;
                    string uniqueSuffixed = candidateSuffixed;
                    while (usedCodesSet.Contains(uniqueSuffixed))
                    {
                        uniqueSuffixed = $"{candidateSuffixed}_{collisionIndex++}";
                    }
                    candidateSuffixed = uniqueSuffixed;
                    usedCodesSet.Add(candidateSuffixed);
                }

                student.StudentCode = candidateSuffixed;
                pendingTransitions.Add((student, dec, originalCode, candidateSuffixed));
            }

            // Lưu Pha 1 xuống DB trong cùng Transaction để giải phóng các mã gốc
            await _context.SaveChangesAsync();

            // PHA 2: Nếu chọn chế độ cấp mã mới theo tên lớp mới cho các em Lên lớp (PROMOTE) hoặc Ở lại lớp (RETAIN)
            foreach (var item in pendingTransitions)
            {
                var student = item.Entity;
                var dec = item.Decision;
                var outcome = (dec.RolloverOutcome ?? "PROMOTE").Trim().ToUpperInvariant();
                string finalCode = item.SuffixedCode;

                if (assignNewClassCodeAfterSuffix &&
                    (outcome == "PROMOTE" || outcome == "RETAIN") &&
                    !string.IsNullOrWhiteSpace(dec.TargetClassName))
                {
                    var prefix = _codeGenerator.BuildClassPrefix(dec.TargetClassName);
                    var seqRegex = new Regex($"^{Regex.Escape(prefix)}(\\d+)$", RegexOptions.IgnoreCase);

                    int maxSeq = 0;
                    foreach (var code in usedCodesSet)
                    {
                        var m = seqRegex.Match(code);
                        if (m.Success && int.TryParse(m.Groups[1].Value, out int seq) && seq > maxSeq)
                        {
                            maxSeq = seq;
                        }
                    }

                    finalCode = $"{prefix}{(maxSeq + 1):D3}";
                    usedCodesSet.Remove(item.SuffixedCode);
                    usedCodesSet.Add(finalCode);
                    student.StudentCode = finalCode;
                }

                _context.StudentCodeHistories.Add(new StudentCodeHistory
                {
                    StudentId = student.Id,
                    SourceAcademicYearId = sourceAcademicYearId,
                    TargetAcademicYearId = targetAcademicYearId,
                    OriginalCode = item.OriginalCode,
                    ArchivedYearCode = item.SuffixedCode,
                    NewCode = finalCode,
                    RolloverOutcome = outcome,
                    SourceClassName = sourceClass?.Name,
                    TargetClassName = dec.TargetClassName,
                    ChangedBy = operatorName,
                    ChangedAt = DateTime.UtcNow
                });

                results.Add(new StudentCodeTransitionResultDto
                {
                    StudentId = student.Id,
                    FullName = $"{student.ChristianName} {student.FirstName} {student.LastName}".Trim(),
                    OldCode = item.OriginalCode,
                    SuffixedOldCode = item.SuffixedCode,
                    FinalActiveCode = finalCode,
                    RolloverOutcome = outcome,
                    SourceClassName = sourceClass?.Name,
                    TargetClassName = dec.TargetClassName
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "AUDIT CODE_ROLLOVER: Processed {Count} student codes from Year {SrcYear} to {DstYear} (Mode={Mode}) by {Actor}",
                results.Count, sourceAcademicYearId, targetAcademicYearId,
                assignNewClassCodeAfterSuffix ? "SUFFIX_AND_NEW_CLASS_CODE" : "SUFFIX_ONLY",
                operatorName);

            return results;
        }

        public async Task<(Student? Student, bool IsHistoricalCode, string? MatchedOldCode)> ResolveStudentByAnyCodeAsync(string inputCode)
        {
            if (string.IsNullOrWhiteSpace(inputCode))
                return (null, false, null);

            var cleanCode = inputCode.Trim().ToUpperInvariant();

            // 1. Ưu tiên tìm theo mã chính thức hiện hành trên bảng Students
            var activeStudent = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IsActive && s.StudentCode.ToUpper() == cleanCode);

            if (activeStudent != null)
            {
                return (activeStudent, false, null);
            }

            // 2. Nếu không khớp mã hiện hành, tra trong bảng lịch sử StudentCodeHistories (OriginalCode hoặc ArchivedYearCode)
            var historyRecord = await _context.StudentCodeHistories
                .Include(h => h.Student)
                .AsNoTracking()
                .Where(h => h.OriginalCode.ToUpper() == cleanCode || h.ArchivedYearCode.ToUpper() == cleanCode)
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefaultAsync();

            if (historyRecord?.Student != null && historyRecord.Student.IsActive && !historyRecord.Student.IsDeleted)
            {
                return (historyRecord.Student, true, cleanCode);
            }

            return (null, false, null);
        }
    }
}