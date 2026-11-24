using ManagerStudentCaltholic.Models.ViewModels;
using ManagerStudentCaltholic.Services;
using Xunit;

namespace ManagerStudentCaltholic.Tests;

public class AgeValidationTests
{
    [Theory]
    [InlineData(2026, 2, 27, 17, false)]
    [InlineData(2026, 2, 28, 18, true)]
    [InlineData(2026, 3, 1, 18, true)]
    public void LeapYearBirthDate_2008_02_29_AgeAndMinimum18Check_MatchConsistently(
        int targetYear,
        int targetMonth,
        int targetDay,
        int expectedAge,
        bool expectedEligible)
    {
        var dob = new DateTime(2008, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var targetDate = new DateTime(targetYear, targetMonth, targetDay, 0, 0, 0, DateTimeKind.Utc);

        int helperAge = AgeValidationHelper.CalculateAgeAtDate(dob, targetDate);
        int viewModelAge = StudentViewModel.CalculateAgeAtDate(dob, targetDate);
        bool isAtLeast18 = AgeValidationHelper.IsAtLeastAge(dob, targetDate, 18);

        Assert.Equal(expectedAge, helperAge);
        Assert.Equal(expectedAge, viewModelAge);
        Assert.Equal(expectedEligible, isAtLeast18);
    }

    [Fact]
    public void FutureBirthDate_ReturnsZeroAgeAndNotEligible()
    {
        var dob = new DateTime(2030, 1, 1);
        var target = new DateTime(2026, 11, 24);

        Assert.Equal(0, AgeValidationHelper.CalculateAgeAtDate(dob, target));
        Assert.False(AgeValidationHelper.IsAtLeastAge(dob, target, 18));
    }
}
