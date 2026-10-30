using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Interface.Services;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    public class StudentPlacementService : IStudentPlacementService
    {
        private readonly ParishDbContext _context;
        private const int DefaultMaxCapacity = 45;

        public StudentPlacementService(ParishDbContext context)
        {
            _context = context;
        }

        public async Task<int?> DetermineBestClassForNewStudentAsync(int academicYearId, string gradeLevel, string gender)
        {
            var classes = await _context.Classes
                .Include(c => c.ClassRoomLocation)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .Where(c => c.AcademicYearId == academicYearId && c.GradeLevel == gradeLevel)
                .AsNoTracking()
                .ToListAsync();

            if (!classes.Any()) return null;

            string normGender = (gender ?? "Nam").Trim();

            var bestClass = classes
                .Select(c => new
                {
                    ClassId = c.Id,
                    MaxCap = c.ClassRoomLocation?.Capacity > 0 ? c.ClassRoomLocation.Capacity : DefaultMaxCapacity,
                    TotalCount = c.Enrollments.Count(e => !e.IsDeleted),
                    SameGenderCount = c.Enrollments.Count(e => !e.IsDeleted &&
                        string.Equals(e.Student.Gender, normGender, StringComparison.OrdinalIgnoreCase))
                })
                .Where(x => x.TotalCount < x.MaxCap)
                .OrderBy(x => x.TotalCount)
                .ThenBy(x => x.SameGenderCount)
                .FirstOrDefault();

            return bestClass?.ClassId ?? classes.First().Id;
        }

        public async Task<Dictionary<long, int>> BatchAssignToTargetClassesAsync(
            List<long> studentIds,
            int targetYearId,
            string targetGradeLevel,
            string? preferredClassPrefix = null)
        {
            var assignments = new Dictionary<long, int>();
            if (studentIds == null || !studentIds.Any()) return assignments;

            var candidateClasses = await _context.Classes
                .Include(c => c.ClassRoomLocation)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .Where(c => c.AcademicYearId == targetYearId && c.GradeLevel == targetGradeLevel)
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            if (!candidateClasses.Any()) return assignments;

            if (!string.IsNullOrWhiteSpace(preferredClassPrefix))
            {
                var prefixMatches = candidateClasses
                    .Where(c => c.Name.StartsWith(preferredClassPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (prefixMatches.Any())
                {
                    candidateClasses = prefixMatches;
                }
            }

            var classBuckets = candidateClasses.Select(c => new ClassBucketState
            {
                ClassId = c.Id,
                ClassName = c.Name,
                MaxCapacity = c.ClassRoomLocation?.Capacity > 0
                    ? c.ClassRoomLocation.Capacity
                    : DefaultMaxCapacity,
                TotalStudents = c.Enrollments.Count(e => !e.IsDeleted),
                MaleCount = c.Enrollments.Count(e => !e.IsDeleted &&
                    string.Equals(e.Student.Gender, "Nam", StringComparison.OrdinalIgnoreCase)),
                FemaleCount = c.Enrollments.Count(e => !e.IsDeleted &&
                    string.Equals(e.Student.Gender, "Nữ", StringComparison.OrdinalIgnoreCase))
            }).ToList();

            var students = await _context.Students
                .Where(s => studentIds.Contains(s.Id))
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .AsNoTracking()
                .ToListAsync();

            var maleStudents = students
                .Where(s => string.Equals(s.Gender, "Nam", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var femaleStudents = students
                .Where(s => !string.Equals(s.Gender, "Nam", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var student in maleStudents)
            {
                var target = classBuckets
                    .Where(b => b.TotalStudents < b.MaxCapacity)
                    .OrderBy(b => b.MaleCount)
                    .ThenBy(b => b.TotalStudents)
                    .ThenBy(b => b.ClassId)
                    .FirstOrDefault()
                    ?? classBuckets.OrderBy(b => b.TotalStudents).First();

                assignments[student.Id] = target.ClassId;
                target.TotalStudents++;
                target.MaleCount++;
            }

            foreach (var student in femaleStudents)
            {
                var target = classBuckets
                    .Where(b => b.TotalStudents < b.MaxCapacity)
                    .OrderBy(b => b.FemaleCount)
                    .ThenBy(b => b.TotalStudents)
                    .ThenBy(b => b.ClassId)
                    .FirstOrDefault()
                    ?? classBuckets.OrderBy(b => b.TotalStudents).First();

                assignments[student.Id] = target.ClassId;
                target.TotalStudents++;
                target.FemaleCount++;
            }

            return assignments;
        }

        private class ClassBucketState
        {
            public int ClassId { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public int MaxCapacity { get; set; }
            public int TotalStudents { get; set; }
            public int MaleCount { get; set; }
            public int FemaleCount { get; set; }
        }
    }
}