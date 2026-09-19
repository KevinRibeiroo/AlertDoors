using System.Globalization;
using System.Text.RegularExpressions;
using AlertDoors.Jobs;
using AngleSharp.Html.Parser;

namespace AlertDoors.Sources;

public sealed class LinkedInPageParser
{
    public SourceResult Parse(string html, DateTimeOffset observedAt)
    {
        using var document = new HtmlParser().ParseDocument(html);
        var cards = document.QuerySelectorAll(".job-search-card");
        var jobs = new Dictionary<string, JobPosting>();
        var invalid = false;
        foreach (var card in cards)
        {
            var href = card.QuerySelector(".base-card__full-link")?.GetAttribute("href");
            var id = Regex.Match(card.GetAttribute("data-entity-urn") ?? "", @"^urn:li:jobPosting:(\d+)$").Groups[1].Value;
            var title = Clean(card.QuerySelector(".base-search-card__title")?.TextContent);
            var company = Clean(card.QuerySelector(".base-search-card__subtitle")?.TextContent);
            if (!Uri.TryCreate(href, UriKind.Absolute, out var link) || !IsJobUri(link)
                || id.Length == 0 || title.Length == 0 || company.Length == 0
                || !Regex.IsMatch(link.AbsolutePath.TrimEnd('/'), @"(?:/|-)" + id + "$"))
            {
                invalid = true;
                continue;
            }
            var location = Clean(card.QuerySelector(".job-search-card__location")?.TextContent);
            var rawDate = card.QuerySelector("time[datetime]")?.GetAttribute("datetime");
            DateTimeOffset? published = DateTimeOffset.TryParse(rawDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date : null;
            var country = Regex.IsMatch(location, @"(?:^|,\s*)(Brazil|Brasil)$", RegexOptions.IgnoreCase) ? "BR" : null;
            jobs.TryAdd(id, new JobPosting("linkedin:" + id, new Uri($"https://www.linkedin.com/jobs/view/{id}/"),
                title, company, location, country, WorkMode.Unknown, null, published, observedAt, ""));
        }
        if (cards.Length > 0)
            return new(invalid ? SourceStatus.SchemaChanged : SourceStatus.Success, jobs.Values.ToArray());
        if (document.QuerySelector("form[action*='checkpoint'], form[action*='login-submit'], #captcha, .challenge-dialog") is not null)
            return new(SourceStatus.Blocked, []);
        if (document.QuerySelector(".jobs-search-no-results-banner") is not null)
            return new(SourceStatus.Success, []);
        return new(SourceStatus.SchemaChanged, []);
    }

    public static bool IsJobUri(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Host == "www.linkedin.com" || uri.Host == "br.linkedin.com" || uri.Host == "linkedin.com")
        && uri.AbsolutePath.StartsWith("/jobs/view/", StringComparison.Ordinal);

    internal static string Clean(string? value) => Regex.Replace(value ?? "", @"\s+", " ").Trim();
}
