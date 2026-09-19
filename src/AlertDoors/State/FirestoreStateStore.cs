using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlertDoors.Jobs;
using AlertDoors.Notifications;
using Google.Cloud.Firestore;

namespace AlertDoors.State;

public sealed class FirestoreStateStore : IStateStore
{
    private readonly FirestoreDb db;
    private readonly TimeProvider clock;
    private readonly DocumentReference root;
    private DocumentReference LeaseDoc => root.Collection("control").Document("lease");
    public FirestoreStateStore(FirestoreDb db, TimeProvider clock, string rootCollection = "alertdoors")
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(rootCollection, "^[a-zA-Z0-9_-]+$")) throw new ArgumentException("Invalid state namespace.", nameof(rootCollection));
        this.db = db;
        this.clock = clock;
        root = db.Collection(rootCollection).Document("state");
    }
    public Task<RunLease?> TryAcquireAsync(string windowId, string ownerId, DateTimeOffset now, CancellationToken ct) =>
        db.RunTransactionAsync<RunLease?>(async transaction =>
        {
            var lease = await transaction.GetSnapshotAsync(LeaseDoc, ct);
            var run = await transaction.GetSnapshotAsync(root.Collection("runs").Document(windowId), ct);
            if (run.Exists && run.GetValue<string>("status") == "complete") return null;
            if (lease.Exists && lease.GetValue<Timestamp>("expiresAt").ToDateTimeOffset() > now) return null;
            var result = new RunLease(ownerId, windowId, now.AddMinutes(15));
            transaction.Set(LeaseDoc, new Dictionary<string, object> { ["ownerId"] = ownerId, ["windowId"] = windowId, ["expiresAt"] = Timestamp.FromDateTimeOffset(result.ExpiresAt) });
            return result;
        }, cancellationToken: ct);

    public async Task EnsureLeaseAsync(RunLease lease, CancellationToken ct) => Validate(await LeaseDoc.GetSnapshotAsync(ct), lease);
    private void Validate(DocumentSnapshot snapshot, RunLease lease)
    {
        if (!snapshot.Exists || snapshot.GetValue<string>("ownerId") != lease.OwnerId
            || snapshot.GetValue<string>("windowId") != lease.WindowId
            || snapshot.GetValue<Timestamp>("expiresAt").ToDateTimeOffset() <= clock.GetUtcNow())
            throw new InvalidOperationException("Execution lease lost or expired.");
    }
    private DocumentReference Delivery(Channel channel, string id) => root.Collection("deliveries").Document(channel + "-" + Hash(id));
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public async Task QueueAsync(RunLease lease, IReadOnlyList<JobPosting> jobs, IReadOnlyList<Channel> channels, CancellationToken ct)
    {
        foreach (var job in jobs)
        {
            await db.RunTransactionAsync(async transaction =>
            {
                Validate(await transaction.GetSnapshotAsync(LeaseDoc, ct), lease);
                var jobRef = root.Collection("jobs").Document(Hash(job.Id));
                var previous = await transaction.GetSnapshotAsync(jobRef, ct);
                var snapshots = new List<(Channel Channel, DocumentReference Ref, DocumentSnapshot Snapshot)>();
                foreach (var channel in channels.Distinct())
                {
                    var reference = Delivery(channel, job.Id);
                    snapshots.Add((channel, reference, await transaction.GetSnapshotAsync(reference, ct)));
                }
                var now = Timestamp.FromDateTimeOffset(clock.GetUtcNow());
                var payload = JsonSerializer.Serialize(job);
                transaction.Set(jobRef, new Dictionary<string, object>
                {
                    ["payload"] = payload, ["firstSeen"] = previous.Exists ? previous.GetValue<Timestamp>("firstSeen") : now, ["lastSeen"] = now
                });
                foreach (var item in snapshots)
                {
                    if (item.Snapshot.Exists && item.Snapshot.GetValue<string>("status") == "sent") continue;
                    transaction.Set(item.Ref, new Dictionary<string, object>
                    {
                        ["channel"] = item.Channel.ToString(), ["status"] = "pending", ["payload"] = payload,
                        ["createdAt"] = item.Snapshot.Exists ? item.Snapshot.GetValue<Timestamp>("createdAt") : now
                    });
                }
            }, cancellationToken: ct);
        }
    }
    public async Task<IReadOnlyList<JobPosting>> GetPendingAsync(RunLease lease, Channel channel, int limit, CancellationToken ct)
    {
        await EnsureLeaseAsync(lease, ct);
        var snapshot = await root.Collection("deliveries").WhereEqualTo("channel", channel.ToString())
            .WhereEqualTo("status", "pending").OrderBy("createdAt").Limit(Math.Clamp(limit, 1, 20)).GetSnapshotAsync(ct);
        return snapshot.Documents.Select(d => JsonSerializer.Deserialize<JobPosting>(d.GetValue<string>("payload"))
            ?? throw new InvalidOperationException("Invalid saved job payload.")).ToArray();
    }
    public Task MarkDeliveredAsync(RunLease lease, NotificationBatch batch, SendReceipt receipt, CancellationToken ct) =>
        db.RunTransactionAsync(async transaction =>
        {
            Validate(await transaction.GetSnapshotAsync(LeaseDoc, ct), lease);
            var refs = batch.Jobs.Select(j => Delivery(batch.Channel, j.Id)).Distinct().ToArray();
            foreach (var reference in refs)
            {
                var snapshot = await transaction.GetSnapshotAsync(reference, ct);
                if (!snapshot.Exists) throw new InvalidOperationException("Delivery was not queued.");
            }
            foreach (var reference in refs)
                transaction.Update(reference, new Dictionary<string, object>
                {
                    ["status"] = "sent", ["sentAt"] = Timestamp.FromDateTimeOffset(receipt.SentAt),
                    ["providerId"] = receipt.ProviderId ?? "", ["batchId"] = batch.Id
                });
        }, cancellationToken: ct);
    public Task FinishAsync(RunLease lease, bool succeeded, CancellationToken ct) =>
        db.RunTransactionAsync(async transaction =>
        {
            Validate(await transaction.GetSnapshotAsync(LeaseDoc, ct), lease);
            transaction.Set(root.Collection("runs").Document(lease.WindowId), new Dictionary<string, object>
            {
                ["status"] = succeeded ? "complete" : "failed", ["finishedAt"] = Timestamp.FromDateTimeOffset(clock.GetUtcNow())
            });
            transaction.Delete(LeaseDoc);
        }, cancellationToken: ct);
}
