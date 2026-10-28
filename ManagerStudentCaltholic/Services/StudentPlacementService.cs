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

        /// <summary>
        /// Xếp 1 học sinh mới vào lớp có sĩ số thấp nhất và cân bằng giới tính
        /// </summary>
        public async Task<int?> DetermineBestClassForNewStudentAsync(int academicYearId, string gradeLevel, string gender)
        {
            var classes = await _context.Classes
                .Include(c => c.ClassRoomLocations)
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
                    MaxCap = c.ClassRoomLocations.FirstOrDefault()?.Capacity > 0 ? c.ClassRoomLocations.FirstOrDefault().Capacity : DefaultMaxCapacity,
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

        /// <summary>
        /// Thuật toán Smart Class Balancing: Dàn đều danh sách học sinh lên lớp theo giới tính & trần sĩ số phòng học (TASK-P3-01.3)
        /// </summary>
        public async Task<Dictionary<long, int>> BatchAssignToTargetClassesAsync(
            List<long> studentIds,
            int targetYearId,
            string targetGradeLevel,
            string? preferredClassPrefix = null)
        {
            var assignments = new Dictionary<long, int>();
            if (studentIds == null || !studentIds.Any()) return assignments;

            var candidateClasses = await _context.Classes
                .Include(c => c.ClassRoomLocations)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .Where(c => c.AcademicYearId == targetYearId && c.GradeLevel == targetGradeLevel)
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            if (!candidateClasses.Any()) return assignments;

            // Nếu có tiền tố cấp lớp (VD: "Khai Tâm 2"), ưu tiên lọc các lớp "Khai Tâm 2A", "Khai Tâm 2B"...
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

            // Tạo bộ đếm trạng thái sĩ số & giới tính hiện thời của từng lớp đích trong bộ nhớ
            var classBuckets = candidateClasses.Select(c => new ClassBucketState
            {
                ClassId = c.Id,
                ClassName = c.Name,
                MaxCapacity = c.ClassRoomLocations.FirstOrDefault()?.Capacity > 0
                    ? c.ClassRoomLocations.FirstOrDefault().Capacity
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

            // Tách nhóm Nam và Nữ để phân bổ xen kẽ đồng đều
            var maleStudents = students
                .Where(s => string.Equals(s.Gender, "Nam", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var femaleStudents = students
                .Where(s => !string.Equals(s.Gender, "Nam", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 1. Phân bổ nhóm Nam: Ưu tiên lớp còn chỗ trống -> ít học sinh Nam nhất -> tổng sĩ số thấp nhất
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

            // 2. Phân bổ nhóm Nữ: Ưu tiên lớp còn chỗ trống -> ít học sinh Nữ nhất -> tổng sĩ số thấp nhất
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