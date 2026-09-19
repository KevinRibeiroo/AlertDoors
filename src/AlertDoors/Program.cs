using System.Text.Json;
using AlertDoors.Configuration;
using AlertDoors.Execution;
using AlertDoors.Jobs;
using AlertDoors.Notifications;
using AlertDoors.Sources;
using AlertDoors.State;
using Google.Api.Gax;
using Google.Cloud.Firestore;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
var clock = TimeProvider.System;
try
{
    if (args is ["demo"])
    {
        var job = new JobPosting("linkedin:12345", new("https://www.linkedin.com/jobs/view/12345/"), "[DEMONSTRAÇÃO] Desenvolvedor .NET Pleno", "Empresa fictícia", "São Paulo, SP", "BR", WorkMode.Hybrid, null, null, clock.GetUtcNow(), "ASP.NET Core");
        Console.WriteLine(JsonSerializer.Serialize(new { Mode = "DEMONSTRAÇÃO — dados fictícios; nenhum envio", Decision = new JobFilter().Evaluate(job), Preview = MessageFormatter.Prepare(Channel.Email, [job])[0].Body }));
        return 0;
    }
    if (args is ["probe", "--html", var file])
    {
        var info = new FileInfo(file);
        if (!info.Exists || info.Length > 2097152) throw new ArgumentException("Diagnostic file must exist and be at most 2 MiB.");
        var parsed = new LinkedInPageParser().Parse(await File.ReadAllTextAsync(file, shutdown.Token), clock.GetUtcNow());
        Console.WriteLine(JsonSerializer.Serialize(parsed));
        return parsed.Status == SourceStatus.Success ? 0 : parsed.Status == SourceStatus.Blocked ? 3 : 4;
    }
    if (!(args is ["probe"] or ["run", "--dry-run"] or ["run"]))
    {
        Console.Error.WriteLine("Uso: demo | probe | probe --html <arquivo> | run --dry-run | run");
        return 2;
    }
    // Validate before constructing anything that can access production services.
    var options = args is ["run"] ? OptionsValidator.Load(Environment.GetEnvironmentVariable) : null;
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    var source = new LinkedInJobSource(new(http, clock), clock, args is ["probe"] ? 2 : 20);
    if (options is null)
    {
        var requests = args is ["probe"] ? BotRunner.Requests(clock.GetUtcNow()).Take(1).Select(r => r with { MaxPages = 1 }) : BotRunner.Requests(clock.GetUtcNow());
        var jobs = new Dictionary<string, JobPosting>();
        var status = SourceStatus.Success;
        var truncated = false;
        foreach (var request in requests)
        {
            var result = await source.SearchAsync(request, shutdown.Token);
            foreach (var job in result.Jobs) jobs[job.Id] = job;
            truncated |= result.Truncated;
            status = result.Status;
            if (status != SourceStatus.Success) break;
        }
        var filter = new JobFilter();
        Console.WriteLine(JsonSerializer.Serialize(new { Mode = "dry-run", Status = status.ToString(), Count = jobs.Count, Truncated = truncated,
            Decisions = jobs.Values.Select(filter.Evaluate).GroupBy(d => d.Reason).ToDictionary(g => g.Key, g => g.Count()),
            Matches = jobs.Values.Where(j => filter.Evaluate(j).Decision == MatchDecision.Include).Select(j => new { j.Title, j.Company, j.Location, j.Url }) }));
        return status == SourceStatus.Success ? 0 : status is SourceStatus.Blocked or SourceStatus.RateLimited ? 3 : 4;
    }
    var db = new FirestoreDbBuilder { ProjectId = options.ProjectId,
        EmulatorDetection = options.Development ? EmulatorDetection.EmulatorOnly : EmulatorDetection.ProductionOnly }.Build();
    using var notificationHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 65536 };
    var notifiers = new List<INotifier>();
    if (options.DiscordWebhook is not null) notifiers.Add(new DiscordNotifier(notificationHttp, options.DiscordWebhook, clock));
    if (options.Smtp is not null) notifiers.Add(new EmailNotifier(options.Smtp, clock));
    return await new BotRunner(source, new FirestoreStateStore(db, clock), new JobFilter(), notifiers, clock).RunAsync(shutdown.Token);
}
catch (OperationCanceledException) { Console.Error.WriteLine("Execução cancelada."); return 130; }
catch (ArgumentException) { Console.Error.WriteLine("Configuração inválida. Confira os nomes e requisitos no README; valores secretos não são registrados."); return 2; }
catch (Exception ex) { Console.Error.WriteLine(JsonSerializer.Serialize(new { Error = "Execution failed", Type = ex.GetType().Name })); return 5; }
