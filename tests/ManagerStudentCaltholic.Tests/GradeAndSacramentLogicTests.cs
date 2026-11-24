using ManagerStudentCaltholic.Data;
using ManagerStudentCaltholic.Models.Entities;
using ManagerStudentCaltholic.Models.Enum;
using ManagerStudentCaltholic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ManagerStudentCaltholic.Tests;

public class GradeAndSacramentLogicTests
{
    private readonly GradeCalculationService _gradeService = new();

    private static ParishDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ParishDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ParishDbContext(options);
    }

    [Fact]
    public void CalculateSemesterAverage_TreatsExplicitZeroDifferentlyFromNull()
    {
        var configs = new List<GradeConfiguration>
        {
            new() { Id = 1, Semester = 1, WeightFactor = 1, DisplayName = "M15" },
            new() { Id = 2, Semester = 1, WeightFactor = 1, DisplayName = "Thi" }
        };

        var scoresWithZero = new Dictionary<int, decimal?>
        {
            [1] = 0.0m,
            [2] = 8.0m
        };
        var scoresWithNull = new Dictionary<int, decimal?>
        {
            [1] = null,
            [2] = 8.0m
        };
        var scoresAllNull = new Dictionary<int, decimal?>
        {
            [1] = null,
            [2] = null
        };

        Assert.Equal(4.00m, _gradeService.CalculateSemesterAverage(scoresWithZero, configs));
        Assert.Equal(8.00m, _gradeService.CalculateSemesterAverage(scoresWithNull, configs));
        Assert.Null(_gradeService.CalculateSemesterAverage(scoresAllNull, configs));
    }

    [Fact]
    public void CalculateSemesterAverage_NegativeWeight_ReturnsNull()
    {
        var configs = new List<GradeConfiguration>
        {
            new() { Id = 1, Semester = 1, WeightFactor = -1, DisplayName = "Invalid" },
            new() { Id = 2, Semester = 1, WeightFactor = 2, DisplayName = "Valid" }
        };
        var scores = new Dictionary<int, decimal?> { [1] = 8.0m, [2] = 9.0m };

        Assert.Null(_gradeService.CalculateSemesterAverage(scores, configs));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -2)]
    public void CalculateYearAverage_ZeroOrNegativeTotalWeight_ReturnsNull(int hk1Weight, int hk2Weight)
    {
        var result = _gradeService.CalculateYearAverage(8.0m, 9.0m, hk1Weight, hk2Weight);
        Assert.Null(result);
    }

    [Fact]
    public void CalculateYearAverage_ValidWeights_UsesAwayFromZeroRounding()
    {
        // (7.00 * 1 + 8.00 * 2) / 3 = 23 / 3 = 7.6666... -> 7.67
        Assert.Equal(7.67m, _gradeService.CalculateYearAverage(7.00m, 8.00m, 1, 2));
        // Only HK1 present
        Assert.Equal(7.50m, _gradeService.CalculateYearAverage(7.50m, null, 1, 2));
        // Both null
        Assert.Null(_gradeService.CalculateYearAverage(null, null, 1, 2));
    }

    [Fact]
    public void EvaluateGradeFormula_NullableScores_NullOrMissingColumnReturnsNull_WhileZeroEvaluates()
    {
        const string formula = "(M15 + EXAM) / 2";

        var withNull = new Dictionary<string, decimal?>
        {
            ["M15"] = null,
            ["EXAM"] = 8.0m
        };
        var withMissing = new Dictionary<string, decimal?>
        {
            ["EXAM"] = 8.0m
        };
        var withExplicitZero = new Dictionary<string, decimal?>
        {
            ["M15"] = 0.0m,
            ["EXAM"] = 8.0m
        };

        Assert.Null(_gradeService.EvaluateGradeFormula(formula, withNull, new[] { "M15", "EXAM" }));
        Assert.Null(_gradeService.EvaluateGradeFormula(formula, withMissing, new[] { "M15", "EXAM" }));
        Assert.Equal(4.00m, _gradeService.EvaluateGradeFormula(formula, withExplicitZero, new[] { "M15", "EXAM" }));
    }

    [Fact]
    public void EvaluateGradeFormula_DivideByZero_ReturnsNull()
    {
        var scores = new Dictionary<string, decimal?> { ["A"] = 8.0m, ["B"] = 0.0m };
        Assert.Null(_gradeService.EvaluateGradeFormula("A / B", scores));
    }

    [Fact]
    public async Task EvaluateCandidatesAsync_ZeroSundaySessions_ThursdayOnlyAttendance_DoesNotProduceNaNOrDivideByZero()
    {
        await using var db = CreateInMemoryContext(nameof(EvaluateCandidatesAsync_ZeroSundaySessions_ThursdayOnlyAttendance_DoesNotProduceNaNOrDivideByZero));

        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            IsCurrent = true
        };
        var cls = new ClassRoom
        {
            Id = 10,
            Name = "Rước Lễ 2A",
            GradeLevel = GradeLevel.RuocLe,
            AcademicYearId = year.Id
        };
        var student = new Student
        {
            Id = 100,
            StudentCode = "TN-2026-0001",
            ChristianName = "Maria",
            FirstName = "Nguyễn Ngọc",
            LastName = "An",
            DateOfBirth = new DateTime(2017, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };
        var enrollment = new Enrollment
        {
            Id = 1000,
            StudentId = student.Id,
            ClassRoomId = cls.Id,
            AcademicYearId = year.Id
        };
        var configHk1 = new GradeConfiguration
        {
            Id = 1,
            AcademicYearId = year.Id,
            GradeLevel = GradeLevel.RuocLe,
            Semester = 1,
            ScoreType = "SEMESTER_EXAM",
            DisplayName = "Thi HK1",
            WeightFactor = 1,
            IsSacramentExam = false
        };
        var gradeRec = new GradeRecord
        {
            Id = 1,
            EnrollmentId = enrollment.Id,
            GradeConfigurationId = configHk1.Id,
            Score = 8.5m
        };
        // Only Thursday attendance exists; zero Sunday sessions in the whole class
        var thursdayAtt = new Attendance
        {
            Id = 1,
            EnrollmentId = enrollment.Id,
            AttendanceDate = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
            DayOfWeek = DayOfWeek.Thursday,
            AttendedMass = true,
            MassStatus = AttendanceStatus.Present,
            ClassAttended = false,
            ClassStatus = AttendanceStatus.AbsentUnpermitted
        };

        db.AcademicYears.Add(year);
        db.Classes.Add(cls);
        db.Students.Add(student);
        db.Enrollments.Add(enrollment);
        db.GradeConfigurations.Add(configHk1);
        db.GradeRecords.Add(gradeRec);
        db.Attendances.Add(thursdayAtt);
        await db.SaveChangesAsync();

        var service = new SacramentEvaluationService(db, _gradeService, NullLogger<SacramentEvaluationService>.Instance);
        var result = await service.EvaluateCandidatesAsync(
            year.Id,
            GradeLevel.RuocLe,
            cls.Id,
            (int)SacramentTypeEnum.FirstCommunion);

        var candidate = Assert.Single(result.Candidates);
        Assert.False(double.IsNaN(candidate.MassAttendanceRate));
        Assert.False(double.IsInfinity(candidate.MassAttendanceRate));
        Assert.False(double.IsNaN(candidate.ClassAttendanceRate));
        Assert.False(double.IsInfinity(candidate.ClassAttendanceRate));
        Assert.False(double.IsNaN(candidate.CombinedAttendanceRate));
        Assert.False(double.IsInfinity(candidate.CombinedAttendanceRate));
    }

    [Fact]
    public async Task EvaluateCandidatesAsync_ClassHasSundaySessions_StudentWithOnlyThursdayRecord_GetsZeroAndFailsThreshold()
    {
        await using var db = CreateInMemoryContext(nameof(EvaluateCandidatesAsync_ClassHasSundaySessions_StudentWithOnlyThursdayRecord_GetsZeroAndFailsThreshold));

        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            IsCurrent = true
        };
        var cls = new ClassRoom
        {
            Id = 10,
            Name = "Rước Lễ 2A",
            GradeLevel = GradeLevel.RuocLe,
            AcademicYearId = year.Id
        };
        var studentA = new Student
        {
            Id = 100,
            StudentCode = "TN-2026-0001",
            ChristianName = "Phêrô",
            FirstName = "Trần Văn",
            LastName = "Bình",
            DateOfBirth = new DateTime(2017, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };
        var studentB = new Student
        {
            Id = 101,
            StudentCode = "TN-2026-0002",
            ChristianName = "Giuse",
            FirstName = "Lê Văn",
            LastName = "Cường",
            DateOfBirth = new DateTime(2017, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };
        var enrA = new Enrollment { Id = 1000, StudentId = studentA.Id, ClassRoomId = cls.Id, AcademicYearId = year.Id };
        var enrB = new Enrollment { Id = 1001, StudentId = studentB.Id, ClassRoomId = cls.Id, AcademicYearId = year.Id };

        var cfg = new GradeConfiguration
        {
            Id = 1,
            AcademicYearId = year.Id,
            GradeLevel = GradeLevel.RuocLe,
            Semester = 1,
            ScoreType = "SEMESTER_EXAM",
            DisplayName = "Thi HK1",
            WeightFactor = 1,
            IsSacramentExam = false
        };
        db.GradeRecords.AddRange(
            new GradeRecord { Id = 1, EnrollmentId = enrA.Id, GradeConfigurationId = cfg.Id, Score = 9.0m },
            new GradeRecord { Id = 2, EnrollmentId = enrB.Id, GradeConfigurationId = cfg.Id, Score = 9.0m });

        // Student A only attended Thursday (0 Sunday records -> previously caused 0/0 = NaN bypassing 80% threshold!)
        db.Attendances.Add(new Attendance
        {
            Id = 1,
            EnrollmentId = enrA.Id,
            AttendanceDate = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
            DayOfWeek = DayOfWeek.Thursday,
            AttendedMass = true,
            MassStatus = AttendanceStatus.Present
        });
        // Student B attended Sunday Mass & Class (establishes class Sunday session totals)
        db.Attendances.Add(new Attendance
        {
            Id = 2,
            EnrollmentId = enrB.Id,
            AttendanceDate = new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc),
            DayOfWeek = DayOfWeek.Sunday,
            AttendedMass = true,
            MassStatus = AttendanceStatus.Present,
            ClassAttended = true,
            ClassStatus = AttendanceStatus.Present
        });

        db.AcademicYears.Add(year);
        db.Classes.Add(cls);
        db.Students.AddRange(studentA, studentB);
        db.Enrollments.AddRange(enrA, enrB);
        db.GradeConfigurations.Add(cfg);
        await db.SaveChangesAsync();

        var service = new SacramentEvaluationService(db, _gradeService, NullLogger<SacramentEvaluationService>.Instance);
        var result = await service.EvaluateCandidatesAsync(
            year.Id,
            GradeLevel.RuocLe,
            cls.Id,
            (int)SacramentTypeEnum.FirstCommunion,
            minYearAvg: 5.0m,
            minAttendanceRate: 80.0,
            requireSacramentExam: false);

        var rowA = result.Candidates.Single(c => c.StudentId == studentA.Id);
        Assert.Equal(0.0, rowA.MassAttendanceRate);
        Assert.Equal(0.0, rowA.ClassAttendanceRate);
        Assert.Equal(0.0, rowA.CombinedAttendanceRate);
        Assert.False(rowA.IsSystemEligible);
        Assert.False(rowA.IsApprovedForSacrament);

        var rowB = result.Candidates.Single(c => c.StudentId == studentB.Id);
        Assert.Equal(100.0, rowB.CombinedAttendanceRate);
        Assert.True(rowB.IsSystemEligible);
    }
}
