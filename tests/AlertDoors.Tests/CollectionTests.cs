using System.Net;
using AlertDoors.Jobs;
using AlertDoors.Sources;
namespace AlertDoors.Tests;
public class CollectionTests
{
    private static string Html => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/linkedin-jobs.html"));
    [Fact]
    public async Task SearchesThirdPageWhenDistinctJobsContinue()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpHandler((_, _) =>
        {
            var html = Html.Replace("12345", (12345 + calls++).ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }));
        var source = new LinkedInJobSource(new(client, TimeProvider.System, TimeSpan.Zero), TimeProvider.System, 0);
        var result = await source.SearchAsync(new(".NET", "Brazil", WorkMode.Remote, DateTimeOffset.UtcNow.AddDays(-1), 3), default);
        Assert.Equal(SourceStatus.Success, result.Status);
        Assert.Equal(3, result.Jobs.Count);
        Assert.Equal(3, calls);
    }
    [Fact]
    public async Task StopsRepeatedPagesAndDeduplicatesIdentity()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpHandler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Html) }); }));
        var source = new LinkedInJobSource(new(client, TimeProvider.System, TimeSpan.Zero), TimeProvider.System, 0);
        var result = await source.SearchAsync(new(".NET", "Brazil", WorkMode.Remote, DateTimeOffset.UtcNow.AddDays(-1), 50), default);
        Assert.Equal(SourceStatus.Success, result.Status);
        Assert.Single(result.Jobs);
        Assert.Equal(2, calls);
        Assert.True(result.Truncated);
    }
    [Fact]
    public async Task PreservesFirstPageWhenNextPageIsBlocked()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpHandler((_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Html) }
            : new HttpResponseMessage(HttpStatusCode.Forbidden))));
        var source = new LinkedInJobSource(new(client, TimeProvider.System, TimeSpan.Zero), TimeProvider.System, 0);
        var result = await source.SearchAsync(new(".NET", "Brazil", WorkMode.Remote, DateTimeOffset.UtcNow.AddDays(-1), 2), default);
        Assert.Equal(SourceStatus.Blocked, result.Status);
        Assert.Single(result.Jobs);
    }
}
