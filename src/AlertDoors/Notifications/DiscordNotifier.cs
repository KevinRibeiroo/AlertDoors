using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlertDoors.Jobs;
namespace AlertDoors.Notifications;
public sealed class DiscordNotifier : INotifier
{
    private readonly HttpClient client;
    private readonly Uri webhook;
    private readonly TimeProvider clock;
    public DiscordNotifier(HttpClient client, Uri webhook, TimeProvider clock)
    {
        if (webhook.Scheme != "https" || webhook.Host != "discord.com" || !webhook.IsDefaultPort || webhook.UserInfo.Length != 0
            || !System.Text.RegularExpressions.Regex.IsMatch(webhook.AbsolutePath, @"^/api(?:/v\d+)?/webhooks/\d+/[A-Za-z0-9_-]+$"))
            throw new ArgumentException("Invalid Discord webhook.", nameof(webhook));
        this.client = client; this.clock = clock;
        this.webhook = new UriBuilder(webhook) { Query = "wait=true", Fragment = "" }.Uri;
    }
    public Channel Channel => Channel.Discord;
    public IReadOnlyList<NotificationBatch> Prepare(IReadOnlyList<JobPosting> jobs) => MessageFormatter.Prepare(Channel, jobs);
    public async Task<SendReceipt> SendAsync(NotificationBatch batch, CancellationToken ct)
    {
        if (batch.Channel != Channel || batch.Body.Length > 1900) throw new ArgumentException("Invalid Discord batch.");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await client.PostAsJsonAsync(webhook, new { content = batch.Body, allowed_mentions = new { parse = Array.Empty<string>() } }, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var seconds = error.RootElement.TryGetProperty("retry_after", out var value) && value.TryGetDouble(out var number) ? number : double.NaN;
                if (!double.IsFinite(seconds) || seconds < 0 || seconds > 30 || attempt == 2)
                    throw new InvalidOperationException("Discord rate limit; delivery remains pending.");
                await Task.Delay(TimeSpan.FromSeconds(seconds), clock, ct);
                continue;
            }
            if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Discord delivery was not confirmed.");
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var id = payload.RootElement.TryGetProperty("id", out var identifier) ? identifier.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Discord receipt is missing.");
            return new(id, clock.GetUtcNow());
        }
        throw new InvalidOperationException("Discord delivery was not confirmed.");
    }
}
