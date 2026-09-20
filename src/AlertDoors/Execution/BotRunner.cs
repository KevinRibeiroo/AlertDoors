using System.Text.Json;
using AlertDoors.Jobs;
using AlertDoors.Notifications;
using AlertDoors.Sources;
using AlertDoors.State;
namespace AlertDoors.Execution;
public sealed class BotRunner(IJobSource source, IStateStore state, JobFilter filter, IReadOnlyList<INotifier> notifiers, TimeProvider clock)
{
    public static IReadOnlyList<SearchRequest> Requests(DateTimeOffset now) =>
        (from word in new[] { ".NET", "C#", "ASP.NET" }
         from mode in new[] { WorkMode.Remote, WorkMode.Hybrid, WorkMode.Onsite }
         select new SearchRequest(word, mode == WorkMode.Remote ? "Brazil" : "São Paulo, São Paulo, Brazil", mode, now.AddDays(-1), 3)).ToArray();
    public async Task<int> RunAsync(CancellationToken ct)
    {
        if (notifiers.Count == 0) return 2;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromMinutes(9));
        var runId = Guid.NewGuid().ToString("N");
        var windowId = RunWindow.Id(clock.GetUtcNow());
        RunLease? lease = null;
        var exit = 0;
        var accepted = new Dictionary<string, JobPosting>();
        var stage = "acquire";
        try
        {
            lease = await state.TryAcquireAsync(windowId, runId, clock.GetUtcNow(), budget.Token);
            if (lease is null) { Log("duplicate_ignored", 0); return 0; }
            stage = "collect";
            foreach (var request in Requests(clock.GetUtcNow()))
            {
                var result = await source.SearchAsync(request, budget.Token);
                foreach (var job in result.Jobs)
                    if (filter.Evaluate(job).Decision == MatchDecision.Include) accepted[job.Id] = job;
                if (result.Truncated) Log("collection_truncated", result.Jobs.Count);
                if (result.Status != SourceStatus.Success)
                {
                    exit = result.Status is SourceStatus.Blocked or SourceStatus.RateLimited ? 3 : 4;
                    Log("source_" + result.Status, result.Jobs.Count);
                    break;
                }
            }
            stage = "queue";
            await state.QueueAsync(lease, accepted.Values.ToArray(), notifiers.Select(n => n.Channel).Distinct().ToArray(), budget.Token);
            foreach (var notifier in notifiers)
            {
                stage = "deliver_" + notifier.Channel;
                try
                {
                    var pending = await state.GetPendingAsync(lease, notifier.Channel, 20, budget.Token);
                    foreach (var batch in notifier.Prepare(pending))
                    {
                        await state.EnsureLeaseAsync(lease, budget.Token);
                        var receipt = await notifier.SendAsync(batch, budget.Token);
                        await state.MarkDeliveredAsync(lease, batch, receipt, budget.Token);
                        Log("delivered_" + notifier.Channel, batch.Jobs.Count);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { exit = 5; Log("channel_failed", 0, ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) { exit = 130; Log("cancelled", 0); }
        catch (Exception ex) { exit = stage == "collect" ? 4 : 5; Log("failed", 0, ex.GetType().Name); }
        finally
        {
            if (lease is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await state.FinishAsync(lease, exit == 0, cleanup.Token); }
                catch (Exception ex) { if (exit == 0) exit = 5; Log("finalization_failed", 0, ex.GetType().Name); }
            }
        }
        Log("finished", accepted.Count);
        return exit;
        void Log(string action, int count, string? errorType = null) => Console.WriteLine(JsonSerializer.Serialize(new { runId, windowId, stage, action, count, exitCode = exit, errorType }));
    }
}
