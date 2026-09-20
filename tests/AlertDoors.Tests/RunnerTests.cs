using AlertDoors.Execution;
namespace AlertDoors.Tests;
public class RunnerTests
{
    [Fact]
    public void RequestsThirdPageForEachSearchVariant() =>
        Assert.All(BotRunner.Requests(DateTimeOffset.Parse("2026-09-19T12:00:00Z")), request => Assert.Equal(3, request.MaxPages));
    [Theory]
    [InlineData("2026-09-19T00:39:59Z", "20260919-0000")]
    [InlineData("2026-09-19T00:59:59Z", "20260919-0000")]
    [InlineData("2026-09-19T01:00:00Z", "20260919-0100")]
    [InlineData("2026-09-19T23:59:59Z", "20260919-2300")]
    public void MapsClockToHourlyWindow(string input, string expected) => Assert.Equal(expected, RunWindow.Id(DateTimeOffset.Parse(input)));
}
