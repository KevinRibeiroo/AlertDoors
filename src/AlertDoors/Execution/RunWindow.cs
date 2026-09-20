using System.Globalization;
namespace AlertDoors.Execution;
public static class RunWindow
{
    public static string Id(DateTimeOffset now)
    {
        var seconds = now.ToUnixTimeSeconds();
        var start = seconds - seconds % (60 * 60);
        return DateTimeOffset.FromUnixTimeSeconds(start).UtcDateTime.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
    }
}
