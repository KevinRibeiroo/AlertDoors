using AlertDoors.Execution;
namespace AlertDoors.Tests;
public class RunnerTests
{
    [Theory]
    [InlineData("2026-09-19T00:39:59Z", "20260919-0000")]
    [InlineData("2026-09-19T00:40:00Z", "20260919-0040")]
    [InlineData("2026-09-19T23:59:59Z", "20260919-2320")]
    public void MapsClockToFortyMinuteWindow(string input, string expected) => Assert.Equal(expected, RunWindow.Id(DateTimeOffset.Parse(input)));
}
