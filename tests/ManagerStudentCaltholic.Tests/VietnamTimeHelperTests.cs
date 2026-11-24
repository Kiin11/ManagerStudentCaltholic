using ManagerStudentCaltholic.Services;
using Xunit;

namespace ManagerStudentCaltholic.Tests;

public class VietnamTimeHelperTests
{
    [Fact]
    public void ToVietnamTime_Saturday2330Utc_ConvertsToSunday0630UtcPlus7()
    {
        // Saturday 2026-11-21 23:30:00 UTC -> Sunday 2026-11-22 06:30:00 UTC+7
        var saturdayUtc = new DateTime(2026, 11, 21, 23, 30, 0, DateTimeKind.Utc);

        var vnTime = VietnamTimeHelper.ToVietnamTime(saturdayUtc);

        Assert.Equal(DayOfWeek.Sunday, vnTime.DayOfWeek);
        Assert.Equal(new DateTime(2026, 11, 22, 6, 30, 0), vnTime);
    }

    [Fact]
    public void ToVietnamTime_UnspecifiedKind_PreservesValueWithoutDoubleShift()
    {
        var alreadyVn = new DateTime(2026, 11, 22, 6, 30, 0, DateTimeKind.Unspecified);

        var result = VietnamTimeHelper.ToVietnamTime(alreadyVn);

        Assert.Equal(alreadyVn, result);
    }

    [Fact]
    public void NormalizeBusinessDateUtcKind_StripsTimeAndSetsUtcKind()
    {
        var input = new DateTime(2026, 11, 22, 14, 45, 30, DateTimeKind.Unspecified);

        var normalized = VietnamTimeHelper.NormalizeBusinessDateUtcKind(input);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc), normalized);
    }
}
