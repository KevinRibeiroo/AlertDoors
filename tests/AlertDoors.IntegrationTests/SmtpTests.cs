using System.Text.Json;
using AlertDoors.Jobs;
using AlertDoors.Notifications;
namespace AlertDoors.IntegrationTests;
public class SmtpTests
{
    [Fact]
    public async Task DeliversTextAndEscapedHtmlToLocalMailbox()
    {
        var recipient = Guid.NewGuid().ToString("N") + "@example.test";
        var job = new JobPosting("linkedin:123", new("https://www.linkedin.com/jobs/view/123/"), ".NET Pleno <script>alert(1)</script>", "Exemplo & Cia", "São Paulo, SP", "BR", WorkMode.Hybrid, null, null, DateTimeOffset.UtcNow, "");
        var notifier = new EmailNotifier(new("127.0.0.1", 1025, null, null, "bot@example.test", recipient, true), TimeProvider.System);
        var batch = Assert.Single(notifier.Prepare([job]));
        var receipt = await notifier.SendAsync(batch, default);
        Assert.NotNull(receipt.ProviderId);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var search = JsonDocument.Parse(await http.GetStringAsync("http://127.0.0.1:8025/api/v1/search?query=" + Uri.EscapeDataString("to:" + recipient)));
        var id = search.RootElement.GetProperty("messages")[0].GetProperty("ID").GetString();
        using var message = JsonDocument.Parse(await http.GetStringAsync("http://127.0.0.1:8025/api/v1/message/" + id));
        var html = message.RootElement.GetProperty("HTML").GetString()!;
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains(job.Url.AbsoluteUri, message.RootElement.GetProperty("Text").GetString());
    }
}
