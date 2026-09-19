using AlertDoors.Jobs;
using AlertDoors.Notifications;
using AlertDoors.State;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace AlertDoors.IntegrationTests;

public class FirestoreStateTests
{
    private readonly ManualClock clock = new();
    private readonly FirestoreDb db;
    private readonly string root = "test_" + Guid.NewGuid().ToString("N");
    public FirestoreStateTests()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")))
            throw new InvalidOperationException("Set FIRESTORE_EMULATOR_HOST for integration tests; production is never used.");
        db = new FirestoreDbBuilder { ProjectId = "alertdoors-test", EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    }
    private FirestoreStateStore Store() => new(db, clock, root);
    private static JobPosting Job(int id = 123) => new($"linkedin:{id}", new($"https://www.linkedin.com/jobs/view/{id}/"), ".NET Pleno", "Exemplo", "São Paulo, SP", "BR", WorkMode.Hybrid, null, null, DateTimeOffset.Parse("2026-09-19T12:00:00Z"), "ASP.NET");
    [Fact]
    public async Task OnlyOneConcurrentExecutionAcquiresLease()
    {
        var leases = await Task.WhenAll(Store().TryAcquireAsync("w", "a", clock.GetUtcNow(), default), Store().TryAcquireAsync("w", "b", clock.GetUtcNow(), default));
        Assert.Single(leases, l => l is not null);
    }
    [Fact]
    public async Task ExpiredOwnerCannotReleaseNewOwnersLease()
    {
        var first = await Store().TryAcquireAsync("w1", "a", clock.GetUtcNow(), default);
        Assert.NotNull(first);
        clock.Now = clock.Now.AddMinutes(16);
        var second = await Store().TryAcquireAsync("w2", "b", clock.GetUtcNow(), default);
        Assert.NotNull(second);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().FinishAsync(first, true, default));
        await Store().EnsureLeaseAsync(second, default);
    }
    [Fact]
    public async Task RestartAndRequeuePreserveConfirmedChannelAndBacklog()
    {
        var store = Store();
        var lease = await store.TryAcquireAsync("w1", "a", clock.GetUtcNow(), default);
        Assert.NotNull(lease);
        var jobs = Enumerable.Range(1, 21).Select(Job).ToArray();
        await store.QueueAsync(lease, jobs, [Channel.Discord, Channel.Email], default);
        var pending = await store.GetPendingAsync(lease, Channel.Discord, 20, default);
        Assert.Equal(20, pending.Count);
        await store.MarkDeliveredAsync(lease, new("batch", Channel.Discord, pending, "test", "test"), new("receipt", clock.GetUtcNow()), default);
        await store.QueueAsync(lease, jobs, [Channel.Discord, Channel.Email], default);
        Assert.Single(await Store().GetPendingAsync(lease, Channel.Discord, 20, default));
        Assert.Equal(20, (await Store().GetPendingAsync(lease, Channel.Email, 20, default)).Count);
        await store.FinishAsync(lease, true, default);
        Assert.Null(await Store().TryAcquireAsync("w1", "new", clock.GetUtcNow(), default));
        var next = await Store().TryAcquireAsync("w2", "new", clock.GetUtcNow(), default);
        Assert.NotNull(next);
        Assert.Single(await Store().GetPendingAsync(next, Channel.Discord, 20, default));
    }
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-09-19T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
