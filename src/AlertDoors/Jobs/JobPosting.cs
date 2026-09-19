namespace AlertDoors.Jobs;

public enum WorkMode { Unknown, Remote, Hybrid, Onsite }
public sealed record JobPosting(string Id, Uri Url, string Title, string Company,
    string Location, string? CountryCode, WorkMode Mode, string? SeniorityText,
    DateTimeOffset? PublishedAt, DateTimeOffset ObservedAt, string Description);
