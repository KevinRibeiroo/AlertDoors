using System.Net;
using AlertDoors.Jobs;
using AlertDoors.Sources;

namespace AlertDoors.Tests;

public class TransportTests
{
    private static readonly Uri Search = new("https://www.linkedin.com/jobs/search/");
    [Theory]
    [InlineData(200, SourceStatus.Success)]
    [InlineData(403, SourceStatus.Blocked)]
    [InlineData(401, SourceStatus.Blocked)]
    [InlineData(429, SourceStatus.RateLimited)]
    public async Task ClassifiesResponses(int code, SourceStatus expected)
    {
        using var client = new HttpClient(new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent("public html") })));
        var result = await new BoundedHttpClient(client, TimeProvider.System, TimeSpan.Zero).GetAsync(Search, default);
        Assert.Equal(expected, result.Status);
    }
    [Fact]
    public async Task NeverRequestsOffDomainRedirect()
    {
        var requests = new List<Uri>();
        using var client = new HttpClient(new StubHttpHandler((req, _) =>
        {
            requests.Add(req.RequestUri!);
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://attacker.test/private");
            return Task.FromResult(response);
        }));
        var result = await new BoundedHttpClient(client, TimeProvider.System, TimeSpan.Zero).GetAsync(Search, default);
        Assert.Equal(SourceStatus.Blocked, result.Status);
        Assert.Single(requests);
    }
    [Fact]
    public async Task RejectsOversizedBodyEvenWithSuccessCode()
    {
        using var client = new HttpClient(new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 2097153)) })));
        var result = await new BoundedHttpClient(client, TimeProvider.System, TimeSpan.Zero).GetAsync(Search, default);
        Assert.Equal(SourceStatus.Unavailable, result.Status);
        Assert.Empty(result.Html);
    }
    [Fact]
    public async Task LongRetryAfterIsReturnedWithoutRetryingEarly()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpHandler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromHours(1));
            return Task.FromResult(response);
        }));
        var result = await new BoundedHttpClient(client, TimeProvider.System, TimeSpan.Zero).GetAsync(Search, default);
        Assert.Equal(SourceStatus.RateLimited, result.Status);
        Assert.NotNull(result.RetryAt);
        Assert.Single(Enumerable.Range(0, calls));
    }
    [Fact]
    public async Task HonorsCallerCancellation()
    {
        using var client = new HttpClient(new StubHttpHandler((_, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BoundedHttpClient(client, TimeProvider.System, TimeSpan.Zero).GetAsync(Search, new CancellationToken(true)));
    }
    [Fact]
    public void EncodesCSharpAndLocation()
    {
        var uri = LinkedInQueryBuilder.Build(new("C#", "São Paulo, Brazil", WorkMode.Hybrid, DateTimeOffset.UtcNow.AddDays(-1), 2));
        Assert.Empty(uri.Fragment);
        Assert.Contains("C%23", uri.Query);
        Assert.Contains("f_WT=3", uri.Query);
    }
    [Fact]
    public async Task RetriesTransientServerFailureAtMostThreeTimes()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpHandler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }));
        var result = await new BoundedHttpClient(client, TimeProvider.System, TimeSpan.Zero).GetAsync(Search, default);
        Assert.Equal(SourceStatus.Unavailable, result.Status);
        Assert.Equal(3, calls);
    }
    [Fact]
    public async Task TransportTimeoutDoesNotBecomeSuccessfulEmptyData()
    {
        using var client = new HttpClient(new StubHttpHandler((_, _) => throw new TaskCanceledException("Synthetic transport timeout")));
        var result = await new BoundedHttpClient(client, TimeProvider.System, TimeSpan.Zero).GetAsync(Search, default);
        Assert.Equal(SourceStatus.Unavailable, result.Status);
        Assert.Empty(result.Html);
    }
    [Fact]
    public void DetailUsesOnlyActualDescriptionNotRelatedJobs()
    {
        var job = new JobPosting("linkedin:12345", new("https://www.linkedin.com/jobs/view/12345/"), "Desenvolvedor .NET Pleno", "Teste", "São Paulo, SP, Brasil", "BR", WorkMode.Unknown, null, null, DateTimeOffset.UtcNow, "");
        var html = "<h1 class='top-card-layout__title'>Desenvolvedor .NET Pleno</h1><div class='description__text'>Modalidade: Híbrido. ASP.NET Core</div><li class='description__job-criteria-item'><h3>Seniority level</h3><span class='description__job-criteria-text'>Mid-Senior level</span></li><aside>Vaga relacionada Remoto</aside>";
        var result = new LinkedInDetailParser().Parse(html, job);
        Assert.Equal(SourceStatus.Success, result.Status);
        var enriched = Assert.Single(result.Jobs);
        Assert.Equal(WorkMode.Hybrid, enriched.Mode);
        Assert.DoesNotContain("relacionada", enriched.Description);
        Assert.Equal("Mid-Senior level", enriched.SeniorityText);
    }
    [Fact]
    public void DetailRecognizesFlexibleOnsiteSchedule()
    {
        var job = new JobPosting("linkedin:12345", new("https://www.linkedin.com/jobs/view/12345/"), "Engenheiro(a) de Software Júnior", "Empresa Exemplo", "São Paulo, São Paulo, Brazil", "BR", WorkMode.Unknown, null, null, DateTimeOffset.UtcNow, "");
        var html = "<h1 class='top-card-layout__title'>Engenheiro(a) de Software Júnior</h1><div class='description__text'>Conhecimento em C#. Modelo presencial com flexibilidade (2x por semana no escritório) em São Paulo/SP.</div>";
        var result = new LinkedInDetailParser().Parse(html, job);
        Assert.Equal(SourceStatus.Success, result.Status);
        var enriched = Assert.Single(result.Jobs);
        Assert.Equal(WorkMode.Hybrid, enriched.Mode);
        Assert.Equal(MatchDecision.Include, new JobFilter().Evaluate(enriched).Decision);
    }
}

public sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
}
