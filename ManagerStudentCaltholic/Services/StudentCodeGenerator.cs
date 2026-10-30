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

        public async Task<string> GenerateUniqueCodeAsync()
        {
            var currentYear = DateTime.UtcNow.Year;
            var prefix = $"TN-{currentYear}-";

            // Dùng IgnoreQueryFilters() để tính cả các bản ghi đã xóa mềm, tránh trùng khóa Unique StudentCode
            var lastStudent = await _context.Students
                .IgnoreQueryFilters()
                .Where(s => s.StudentCode.StartsWith(prefix))
                .OrderByDescending(s => s.StudentCode)
                .Select(s => s.StudentCode)
                .FirstOrDefaultAsync();

            int nextSequence = 1;
            if (!string.IsNullOrEmpty(lastStudent) && lastStudent.Length >= prefix.Length + 4)
            {
                var seqPart = lastStudent.Substring(prefix.Length);
                if (int.TryParse(seqPart, out int parsed))
                {
                    nextSequence = parsed + 1;
                }
            }

            return $"{prefix}{nextSequence:D4}";
        }
    }
}