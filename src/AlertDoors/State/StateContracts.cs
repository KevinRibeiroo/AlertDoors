using AlertDoors.Jobs;
using AlertDoors.Notifications;
namespace AlertDoors.State;
public sealed record RunLease(string OwnerId, string WindowId, DateTimeOffset ExpiresAt);
public interface IStateStore
{
    Task<RunLease?> TryAcquireAsync(string windowId, string ownerId, DateTimeOffset now, CancellationToken ct);
    Task EnsureLeaseAsync(RunLease lease, CancellationToken ct);
    Task QueueAsync(RunLease lease, IReadOnlyList<JobPosting> jobs, IReadOnlyList<Channel> channels, CancellationToken ct);
    Task<IReadOnlyList<JobPosting>> GetPendingAsync(RunLease lease, Channel channel, int limit, CancellationToken ct);
    Task MarkDeliveredAsync(RunLease lease, NotificationBatch batch, SendReceipt receipt, CancellationToken ct);
    Task FinishAsync(RunLease lease, bool succeeded, CancellationToken ct);
}
