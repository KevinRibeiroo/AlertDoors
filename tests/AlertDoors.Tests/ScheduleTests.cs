using System.Text.RegularExpressions;
namespace AlertDoors.Tests;
public class ScheduleTests
{
    [Fact]
    public void TerraformSchedulesKeepFortyMinuteGapsAcrossMidnight()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AlertDoors.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var text = File.ReadAllText(Path.Combine(directory.FullName, "infra/terraform/scheduler.tf"));
        var expressions = Regex.Matches(text, "\"(?:even|odd)\"\\s*=\\s*\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(2, expressions.Length);
        var starts = Enumerable.Range(0, 48 * 60).Where(m => expressions.Any(e => Matches(e, m))).ToArray();
        Assert.Equal(72, starts.Length);
        for (var i = 1; i < starts.Length; i++) Assert.Equal(40, starts[i] - starts[i - 1]);
    }
    [Fact]
    public void MinuteStepAloneDoesNotMeanFortyMinuteIntervals()
    {
        var starts = Enumerable.Range(0, 120).Where(m => Matches("*/40 * * * *", m)).ToArray();
        Assert.Equal(new[] { 0, 40, 60, 100 }, starts);
    }
    private static bool Matches(string expression, int minute)
    {
        var fields = expression.Split(' ');
        Assert.Equal(5, fields.Length);
        Assert.All(fields.Skip(2), field => Assert.Equal("*", field));
        return Field(fields[0], minute % 60, 59) && Field(fields[1], minute / 60 % 24, 23);
    }
    private static bool Field(string field, int value, int max)
    {
        return field.Split(',').Any(part =>
        {
            var stepParts = part.Split('/');
            var step = stepParts.Length == 2 ? int.Parse(stepParts[1]) : 1;
            var range = stepParts[0].Split('-');
            var start = range[0] == "*" ? 0 : int.Parse(range[0]);
            var end = range[0] == "*" ? max : range.Length == 2 ? int.Parse(range[1]) : start;
            return value >= start && value <= end && (value - start) % step == 0;
        });
    }
}
