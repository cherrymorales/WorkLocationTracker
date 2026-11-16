using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Tests;

public sealed class WorkdayTargetCalculatorTests
{
    [Theory]
    [InlineData(0, 0, false, 456, 0)]
    [InlineData(7.5, 450, false, 6, 0)]
    [InlineData(7.6, 456, true, 0, 0)]
    [InlineData(8.0, 480, true, 0, 24)]
    [InlineData(9.25, 555, true, 0, 99)]
    public void Calculate_ReturnsTargetProgress(
        double workedHours,
        int expectedWorkedMinutes,
        bool expectedIsMet,
        int expectedRemainingMinutes,
        int expectedAdditionalMinutes)
    {
        var result = WorkdayTargetCalculator.Calculate(
            workedHours,
            targetMinutes: 456);

        Assert.Equal(expectedWorkedMinutes, result.WorkedMinutes);
        Assert.Equal(expectedIsMet, result.IsMet);
        Assert.Equal(expectedRemainingMinutes, result.RemainingMinutes);
        Assert.Equal(expectedAdditionalMinutes, result.AdditionalMinutes);
        Assert.Equal(
            expectedAdditionalMinutes,
            WorkdayTargetCalculator.CalculateAdditionalMinutes(workedHours, targetMinutes: 456));
    }
}
