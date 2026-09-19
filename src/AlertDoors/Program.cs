using System.Text.Json;
using AlertDoors.Sources;

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
