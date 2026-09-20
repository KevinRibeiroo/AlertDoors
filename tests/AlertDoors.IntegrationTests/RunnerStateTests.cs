using AlertDoors.Execution;
using AlertDoors.Jobs;
using AlertDoors.Notifications;
using AlertDoors.Sources;
using AlertDoors.State;
using Google.Api.Gax;
using Google.Cloud.Firestore;
namespace AlertDoors.IntegrationTests;
public class RunnerStateTests
{
    private readonly FirestoreStateStore store;
    private readonly MutableClock clock = new();
    public RunnerStateTests()
    {
        var db = new FirestoreDbBuilder { ProjectId = "alertdoors-test", EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        store = new(db, clock, "runner_" + Guid.NewGuid().ToString("N"));
    }
    private static JobPosting Job(int id) => new($"linkedin:{id}", new($"https://www.linkedin.com/jobs/view/{id}/"), "Desenvolvedor .NET Pleno", "Exemplo", "São Paulo, SP", "BR", WorkMode.Hybrid, null, null, DateTimeOffset.UtcNow, "");
    [Fact]
    public async Task FailedSecondMessageDoesNotRepeatConfirmedMessageOrOtherChannel()
    {
        var source = new FixedSource([Job(1), Job(2)]);
        var discord = new RecordingNotifier(Channel.Discord) { FailCall = 2 };
        var email = new RecordingNotifier(Channel.Email);
        Assert.Equal(5, await new BotRunner(source, store, new(), [discord, email], clock).RunAsync(default));
        Assert.Equal(2, email.Accepted.Count);
        Assert.Single(discord.Accepted);
        var retryDiscord = new RecordingNotifier(Channel.Discord);
        var retryEmail = new RecordingNotifier(Channel.Email);
        Assert.Equal(0, await new BotRunner(source, store, new(), [retryDiscord, retryEmail], clock).RunAsync(default));
        var retriedId = Assert.Single(retryDiscord.Accepted);
        Assert.DoesNotContain(retriedId, discord.Accepted);
        Assert.Equal(new[] { "linkedin:1", "linkedin:2" }, discord.Accepted.Concat(retryDiscord.Accepted).Order().ToArray());
        Assert.Empty(retryEmail.Accepted);
        var duplicate = new RecordingNotifier(Channel.Discord);
        Assert.Equal(0, await new BotRunner(source, store, new(), [duplicate], clock).RunAsync(default));
        Assert.Empty(duplicate.Accepted);
    }
    [Fact]
    public async Task BacklogOverTwentySurvivesIntoNextWindow()
    {
        var source = new FixedSource(Enumerable.Range(1, 21).Select(Job).ToArray());
        var notifier = new RecordingNotifier(Channel.Discord);
        Assert.Equal(0, await new BotRunner(source, store, new(), [notifier], clock).RunAsync(default));
        Assert.Equal(20, notifier.Accepted.Count);
        clock.Now = clock.Now.AddMinutes(40);
        var next = new RecordingNotifier(Channel.Discord);
        Assert.Equal(0, await new BotRunner(source, store, new(), [next], clock).RunAsync(default));
        Assert.Single(next.Accepted);
    }
    [Fact]
    public async Task BlockedSourceStillDeliversPreviouslyQueuedJobsAndReportsFailure()
    {
        var lease = await store.TryAcquireAsync("setup", "setup", clock.Now, default);
        Assert.NotNull(lease);
        await store.QueueAsync(lease, [Job(1)], [Channel.Discord], default);
        await store.FinishAsync(lease, false, default);
        var notifier = new RecordingNotifier(Channel.Discord);
        var result = await new BotRunner(new FixedSource([], SourceStatus.Blocked), store, new(), [notifier], clock).RunAsync(default);
        Assert.Equal(3, result);
        Assert.Single(notifier.Accepted);
    }
    private sealed class FixedSource(IReadOnlyList<JobPosting> jobs, SourceStatus status = SourceStatus.Success) : IJobSource
    {
        public Task<SourceResult> SearchAsync(SearchRequest request, CancellationToken ct) => Task.FromResult(new SourceResult(status, jobs));
    }
    private sealed class RecordingNotifier(Channel channel) : INotifier
    {
        private int calls;
        public int FailCall { get; init; } = -1;
        public List<string> Accepted { get; } = [];
        public Channel Channel => channel;
        public IReadOnlyList<NotificationBatch> Prepare(IReadOnlyList<JobPosting> jobs) => jobs.Select(j => new NotificationBatch(j.Id, channel, [j], "test", "test")).ToArray();
        public Task<SendReceipt> SendAsync(NotificationBatch batch, CancellationToken ct)
        {
            if (++calls == FailCall) throw new InvalidOperationException("Synthetic channel failure.");
            Accepted.AddRange(batch.Jobs.Select(j => j.Id));
            return Task.FromResult(new SendReceipt("test", DateTimeOffset.UtcNow));
        }
    }
    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-09-19T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
