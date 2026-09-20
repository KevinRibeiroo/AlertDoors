using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using AlertDoors.Jobs;
using AngleSharp.Html.Parser;
namespace AlertDoors.Sources;
public sealed class LinkedInDetailParser
{
    public SourceResult Parse(string html, JobPosting job)
    {
        using var document = new HtmlParser().ParseDocument(html);
        var title = LinkedInPageParser.Clean(document.QuerySelector(".top-card-layout__title")?.TextContent);
        var description = LinkedInPageParser.Clean(document.QuerySelector(".description__text")?.TextContent);
        if (title.Length == 0 || description.Length == 0)
            return new(document.QuerySelector("form[action*='checkpoint'], form[action*='login-submit']") is null ? SourceStatus.SchemaChanged : SourceStatus.Blocked, []);
        string? seniority = null;
        foreach (var item in document.QuerySelectorAll(".description__job-criteria-item"))
        {
            var heading = Normalize(item.QuerySelector("h3")?.TextContent ?? "");
            if (heading.Contains("seniority") || heading.Contains("nivel de experiencia"))
                seniority = LinkedInPageParser.Clean(item.QuerySelector(".description__job-criteria-text")?.TextContent);
        }
        var normalizedDescription = Normalize(description);
        var relevant = Normalize(title) + " " + string.Join(" ", Regex.Matches(normalizedDescription,
            @"(?:modalidade|modelo(?: de trabalho)?|regime(?: de trabalho)?|formato|trabalho|atuacao)\s*:\s*(?:100\s*%\s*)?(?:remot[oa]|hibrid[oa]|presencial)")
            .Select(m => m.Value));
        var remote = Regex.IsMatch(relevant, @"\b(remote|remot[oa])\b");
        var hybrid = Regex.IsMatch(relevant, @"\b(hybrid|hibrid[oa])\b");
        var onsite = Regex.IsMatch(relevant, @"\b(onsite|presencial)\b");
        var count = (remote ? 1 : 0) + (hybrid ? 1 : 0) + (onsite ? 1 : 0);
        var mode = count != 1 ? WorkMode.Unknown : remote ? WorkMode.Remote : hybrid ? WorkMode.Hybrid : WorkMode.Onsite;
        if (count == 0 && Regex.IsMatch(normalizedDescription, @"(?<!\w)[1-4]\s*x\s+por\s+semana\s+no\s+escritorio(?!\w)"))
            mode = WorkMode.Hybrid;
        return new(SourceStatus.Success, [job with { Title = title, Description = description, SeniorityText = seniority, Mode = mode }]);
    }
    internal static string Normalize(string value) => new string(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
}
