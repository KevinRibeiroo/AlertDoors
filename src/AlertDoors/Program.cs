using System.Text.Json;
using AlertDoors.Sources;
using AlertDoors.Jobs;

if (args is ["probe"])
{
    using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
    var source = new LinkedInJobSource(new(client, TimeProvider.System), TimeProvider.System, maxDetails: 2);
    var result = await source.SearchAsync(new(".NET", "São Paulo, São Paulo, Brazil", WorkMode.Hybrid, DateTimeOffset.UtcNow.AddDays(-1), 1), CancellationToken.None);
    var decisions = result.Jobs.Select(j => new JobFilter().Evaluate(j)).GroupBy(x => x.Reason).ToDictionary(g => g.Key, g => g.Count());
    Console.WriteLine(JsonSerializer.Serialize(new { Status = result.Status.ToString(), Count = result.Jobs.Count, result.Truncated, result.RetryAt, Decisions = decisions }));
    return result.Status == SourceStatus.Success ? 0 : result.Status is SourceStatus.Blocked or SourceStatus.RateLimited ? 3 : 4;
}

if (args is ["probe", "--html", var file])
{
    var info = new FileInfo(file);
    if (!info.Exists || info.Length > 2 * 1024 * 1024)
    {
        Console.Error.WriteLine("O arquivo de diagnóstico deve existir e ter no máximo 2 MiB.");
        return 2;
    }
    var result = new LinkedInPageParser().Parse(await File.ReadAllTextAsync(file), DateTimeOffset.UtcNow);
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return result.Status == SourceStatus.Success ? 0 : result.Status == SourceStatus.Blocked ? 3 : 4;
}
Console.Error.WriteLine("Uso inicial: probe --html <arquivo público obtido para diagnóstico>");
return 2;
