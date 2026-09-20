using System.Text.RegularExpressions;
using AlertDoors.Sources;
namespace AlertDoors.Jobs;
public enum MatchDecision { Include, Exclude, Unknown }
public sealed record FilterResult(MatchDecision Decision, string Reason);
public sealed class JobFilter
{
    public FilterResult Evaluate(JobPosting job)
    {
        var title = LinkedInDetailParser.Normalize(job.Title);
        var description = LinkedInDetailParser.Normalize(job.Description);
        var level = LinkedInDetailParser.Normalize(job.SeniorityText ?? "");
        var location = LinkedInDetailParser.Normalize(job.Location).Trim();
        if (!Regex.IsMatch(title + " " + description, @"(?<!\w)(?:\.?asp\.net|\.net|dotnet|c#)(?!\w)"))
            return new(MatchDecision.Exclude, "technology_mismatch");
        var wanted = Regex.IsMatch(title, @"\b(junior|jr|pleno|pl|mid[ -]level)\b");
        var excluded = Regex.IsMatch(title, @"\b(senior|sr|lead|lider|diretor|director|manager|gerente|estagio|estagiario|intern|principal|staff)\b");
        var explicitLevelExclusion = Regex.IsMatch(level, @"\b(director|executive|manager|internship|estagio)\b")
            || (Regex.IsMatch(level, @"\b(senior|sr)\b") && !level.Contains("mid-senior"));
        if (wanted && (excluded || explicitLevelExclusion)) return new(MatchDecision.Unknown, "seniority_conflict");
        if (excluded || explicitLevelExclusion) return new(MatchDecision.Exclude, "seniority_excluded");
        if (!wanted && !Regex.IsMatch(level, @"\b(junior|pleno|mid[ -]level)\b")) return new(MatchDecision.Unknown, "seniority_missing");
        if (job.Mode == WorkMode.Unknown) return new(MatchDecision.Unknown, "work_mode_missing");
        if (job.Mode == WorkMode.Remote)
        {
            if (Regex.IsMatch(description, @"\b(us only|usa only|united states only|canada only|canada apenas|portugal only|europe only|somente (?:nos )?eua|must (?:reside|be based) in (?:the )?(?:us|united states|canada|portugal))\b"))
                return new(MatchDecision.Exclude, "remote_country_restriction");
            if (job.CountryCode is null) return new(MatchDecision.Unknown, "remote_country_missing");
            if (!job.CountryCode.Equals("BR", StringComparison.OrdinalIgnoreCase)) return new(MatchDecision.Exclude, "outside_country");
        }
        else
        {
            if (location == "sao paulo" || location.Length == 0) return new(MatchDecision.Unknown, "city_ambiguous");
            if (!Regex.IsMatch(location, @"^sao paulo\s*[,/-]\s*(?:sp|sao paulo)(?:\s*,\s*(?:brazil|brasil|br))?$"))
                return new(MatchDecision.Exclude, "outside_city");
        }
        return new(MatchDecision.Include, "matched");
    }
}
