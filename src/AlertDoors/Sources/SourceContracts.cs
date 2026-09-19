using AlertDoors.Jobs;

namespace AlertDoors.Sources;

public enum SourceStatus { Success, Blocked, RateLimited, SchemaChanged, Unavailable }
public sealed record SearchRequest(string Keywords, string Location, WorkMode Mode,
    DateTimeOffset Since, int MaxPages);
public sealed record SourceResult(SourceStatus Status, IReadOnlyList<JobPosting> Jobs,
    bool Truncated = false, DateTimeOffset? RetryAt = null);
public interface IJobSource
{
    Task<SourceResult> SearchAsync(SearchRequest request, CancellationToken ct);
}
