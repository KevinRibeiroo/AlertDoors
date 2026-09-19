using AlertDoors.Jobs;
namespace AlertDoors.Sources;
public sealed class LinkedInJobSource(BoundedHttpClient http, TimeProvider clock, int maxDetails = 20) : IJobSource
{
    private readonly Dictionary<string, JobPosting> details = [];
    private int detailRequests;
    public async Task<SourceResult> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var found = new Dictionary<string, JobPosting>();
        var status = SourceStatus.Success;
        DateTimeOffset? retryAt = null;
        var truncated = false;
        var start = 0;
        for (var page = 0; page < Math.Clamp(request.MaxPages, 1, 2); page++)
        {
            var response = await http.GetAsync(LinkedInQueryBuilder.Build(request, start), ct);
            if (response.Status != SourceStatus.Success) { status = response.Status; retryAt = response.RetryAt; break; }
            var parsed = new LinkedInPageParser().Parse(response.Html, clock.GetUtcNow());
            var added = 0;
            foreach (var job in parsed.Jobs) if (found.TryAdd(job.Id, job)) added++;
            if (parsed.Status != SourceStatus.Success) { status = parsed.Status; break; }
            if (parsed.Jobs.Count == 0) break;
            if (added == 0) { truncated = true; break; }
            start += parsed.Jobs.Count;
            if (page == Math.Clamp(request.MaxPages, 1, 2) - 1) truncated = true;
        }
        foreach (var job in found.Values.OrderByDescending(j => System.Text.RegularExpressions.Regex.IsMatch(j.Title, @"(?i)\b(j[uú]nior|jr|pleno|pl|mid.level)\b")).ToArray())
        {
            if (details.TryGetValue(job.Id, out var cached)) { found[job.Id] = cached; continue; }
            if (status != SourceStatus.Success || detailRequests >= maxDetails) { truncated = true; continue; }
            detailRequests++;
            var response = await http.GetAsync(job.Url, ct);
            if (response.Status != SourceStatus.Success) { status = response.Status; retryAt = response.RetryAt; continue; }
            var parsed = new LinkedInDetailParser().Parse(response.Html, job);
            if (parsed.Status != SourceStatus.Success) { status = parsed.Status; continue; }
            found[job.Id] = details[job.Id] = parsed.Jobs[0];
        }
        return new(status, found.Values.Where(j => j.PublishedAt is null || j.PublishedAt.Value.Date >= request.Since.UtcDateTime.Date).ToArray(), truncated, retryAt);
    }
}
