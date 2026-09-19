using AlertDoors.Jobs;
namespace AlertDoors.Notifications;
public enum Channel { Discord, Email }
public sealed record NotificationBatch(string Id, Channel Channel, IReadOnlyList<JobPosting> Jobs, string Subject, string Body);
public sealed record SendReceipt(string? ProviderId, DateTimeOffset SentAt);
public interface INotifier
{
    Channel Channel { get; }
    IReadOnlyList<NotificationBatch> Prepare(IReadOnlyList<JobPosting> jobs);
    Task<SendReceipt> SendAsync(NotificationBatch batch, CancellationToken ct);
}
