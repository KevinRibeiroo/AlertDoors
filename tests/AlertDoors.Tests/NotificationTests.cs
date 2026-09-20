using System.Net;
using System.Text.Json;
using AlertDoors.Notifications;
namespace AlertDoors.Tests;
public class NotificationTests
{
    private static readonly Uri FakeWebhook = new("https://discord.com/api/webhooks/123/fictional-test-value");
    [Fact]
    public void MarksUnknownSeniorityAndModeInAlert()
    {
        var job = FilterTests.Sample("Programador .NET") with { Mode = AlertDoors.Jobs.WorkMode.Unknown };
        var batch = Assert.Single(MessageFormatter.Prepare(Channel.Discord, [job]));
        Assert.Contains("Modalidade a confirmar", batch.Body);
        Assert.Contains("Senioridade a confirmar", batch.Body);
    }
    [Fact]
    public void SplitsLongSummariesWithoutDroppingJobsOrLinks()
    {
        using var client = new HttpClient();
        var jobs = Enumerable.Range(1, 20).Select(i => FilterTests.Sample(new string('x', 2000)) with { Id = $"linkedin:{i}", Url = new($"https://www.linkedin.com/jobs/view/{i}/") }).ToArray();
        var batches = new DiscordNotifier(client, FakeWebhook, TimeProvider.System).Prepare(jobs);
        Assert.True(batches.Count > 1);
        Assert.Equal(20, batches.Sum(b => b.Jobs.Count));
        Assert.All(batches, b => { Assert.True(b.Body.Length <= 1900); Assert.All(b.Jobs, j => Assert.Contains(j.Url.AbsoluteUri, b.Body)); });
    }
    [Fact]
    public async Task DisablesMentionsAndWaitsForDeliveryReceipt()
    {
        string body = "";
        Uri? target = null;
        using var client = new HttpClient(new StubHttpHandler(async (req, ct) =>
        {
            target = req.RequestUri;
            body = await req.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"test-message\"}") };
        }));
        var notifier = new DiscordNotifier(client, FakeWebhook, TimeProvider.System);
        var batches = notifier.Prepare([FilterTests.Sample(".NET Pleno @everyone")]);
        var batch = Assert.Single(batches);
        var receipt = await notifier.SendAsync(batch, default);
        Assert.Equal("test-message", receipt.ProviderId);
        Assert.Contains("wait=true", target!.Query);
        using var payload = JsonDocument.Parse(body);
        Assert.Empty(payload.RootElement.GetProperty("allowed_mentions").GetProperty("parse").EnumerateArray());
    }
    [Fact]
    public async Task IdentifiesClientWhenSendingDiscordNotification()
    {
        using var client = new HttpClient(new StubHttpHandler((request, _) =>
        {
            var identified = request.Headers.UserAgent.Any(product => product.Product?.Name == "AlertDoors");
            return Task.FromResult(identified
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"test-message\"}") }
                : new HttpResponseMessage(HttpStatusCode.Forbidden));
        }));
        var notifier = new DiscordNotifier(client, FakeWebhook, TimeProvider.System);
        var batch = new NotificationBatch("x", Channel.Discord, [FilterTests.Sample()], "x", "x");

        var receipt = await notifier.SendAsync(batch, default);

        Assert.Equal("test-message", receipt.ProviderId);
    }
    [Fact]
    public async Task LongRateLimitLeavesDeliveryUnconfirmed()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHttpHandler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{\"retry_after\":3600}") }); }));
        var notifier = new DiscordNotifier(client, FakeWebhook, TimeProvider.System);
        var batch = new NotificationBatch("x", Channel.Discord, [FilterTests.Sample()], "x", "x");
        await Assert.ThrowsAsync<InvalidOperationException>(() => notifier.SendAsync(batch, default));
        Assert.Equal(1, calls);
    }
    [Fact]
    public void RefusesInsecureSmtpToExternalHost() => Assert.Throws<ArgumentException>(() => new EmailNotifier(new("smtp.example.test", 1025, null, null, "bot@example.test", "target@example.test", true), TimeProvider.System));
}
