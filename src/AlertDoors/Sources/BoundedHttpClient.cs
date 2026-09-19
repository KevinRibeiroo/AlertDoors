using System.Net;
using System.Text;

namespace AlertDoors.Sources;

public sealed record FetchResult(SourceStatus Status, string Html = "", DateTimeOffset? RetryAt = null);
public sealed class BoundedHttpClient(HttpClient client, TimeProvider clock, TimeSpan? pause = null)
{
    private readonly DateTimeOffset deadline = clock.GetUtcNow().AddMinutes(6);
    private DateTimeOffset nextRequest;
    public async Task<FetchResult> GetAsync(Uri uri, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!Allowed(uri)) return new(SourceStatus.Blocked);
            if (clock.GetUtcNow() >= deadline) return new(SourceStatus.Unavailable);
            var wait = nextRequest - clock.GetUtcNow();
            if (wait > TimeSpan.Zero) await Task.Delay(wait, clock, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("AlertDoors/0.1");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                nextRequest = clock.GetUtcNow() + (pause ?? TimeSpan.FromSeconds(1));
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    var target = response.Headers.Location;
                    if (target is null) return new(SourceStatus.Blocked);
                    uri = target.IsAbsoluteUri ? target : new Uri(uri, target);
                    if (!Allowed(uri)) return new(SourceStatus.Blocked);
                    continue;
                }
                if ((int)response.StatusCode is 401 or 403 or 999) return new(SourceStatus.Blocked);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retry = response.Headers.RetryAfter;
                    var retryAt = retry?.Date ?? (retry?.Delta is { } delta ? clock.GetUtcNow() + delta : (DateTimeOffset?)null);
                    if (retryAt is null || retryAt >= deadline || attempt == 2) return new(SourceStatus.RateLimited, RetryAt: retryAt);
                    var retryWait = retryAt.Value - clock.GetUtcNow();
                    if (retryWait > TimeSpan.Zero) await Task.Delay(retryWait, clock, ct);
                    continue;
                }
                if ((int)response.StatusCode >= 500 && attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 << attempt), clock, ct);
                    continue;
                }
                if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > 2097152)
                    return new(SourceStatus.Unavailable);
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream();
                var bytes = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(bytes, timeout.Token)) > 0)
                {
                    if (buffer.Length + read > 2097152) return new(SourceStatus.Unavailable);
                    buffer.Write(bytes, 0, read);
                }
                return new(SourceStatus.Success, Encoding.UTF8.GetString(buffer.ToArray()));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(SourceStatus.Unavailable); }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 << attempt), clock, ct);
            }
            catch (HttpRequestException) { return new(SourceStatus.Unavailable); }
            catch (IOException) { return new(SourceStatus.Unavailable); }
        }
        return new(SourceStatus.Unavailable);
    }
    private static bool Allowed(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" && uri.IsDefaultPort
        && uri.UserInfo.Length == 0 && (uri.Host is "www.linkedin.com" or "br.linkedin.com" or "linkedin.com")
        && (uri.AbsolutePath.StartsWith("/jobs/search", StringComparison.Ordinal) || LinkedInPageParser.IsJobUri(uri));
}
