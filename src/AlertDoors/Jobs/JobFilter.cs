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
        const string technology = @"(?<!\w)(?:\.?asp\.net|\.net|dotnet|c#)(?!\w)";
        if (!Regex.IsMatch(title + " " + description, technology))
            return new(MatchDecision.Exclude, "technology_mismatch");
        var namedDevelopmentRole = Regex.IsMatch(title + " " + description,
            @"(?<!\w)(?:desenvolvedor(?:a|\(a\))?|developer|engenheir(?:o(?:\(a\))?|a)\s+de\s+software|software\s+engineer)(?!\w)")
            || Regex.IsMatch(title,
                @"(?<!\w)(?:programador(?:a|\(a\))?|programmer|analista\s+(?:de\s+)?(?:sistemas?|software|desenvolvimento))(?!\w)");
        var technologyRoleTitle = Regex.IsMatch(title, technology)
            && Regex.IsMatch(title, @"(?<!\w)(?:analista|engenheir(?:o(?:\(a\))?|a))(?!\w)")
            && !Regex.IsMatch(title, @"\b(?:dados|data|qa|qualidade|testes?|infraestrutura|devops|seguranca)\b");
        if (!namedDevelopmentRole && !technologyRoleTitle)
            return new(MatchDecision.Exclude, "role_mismatch");
        var wanted = Regex.IsMatch(title, @"\b(junior|jr|pleno|pl|mid[ -]level)\b");
        var seniorTitle = Regex.IsMatch(title, @"\b(senior|sr)\b");
        var excludedRole = Regex.IsMatch(title, @"\b(lead|lider|diretor|director|manager|gerente|estagio|estagiario|intern|principal|staff)\b");
        var explicitLevelExclusion = Regex.IsMatch(level, @"\b(director|executive|manager|internship|estagio)\b")
            || (Regex.IsMatch(level, @"\b(senior|sr)\b") && !level.Contains("mid-senior") && !(wanted && seniorTitle));
        if (wanted && (excludedRole || explicitLevelExclusion)) return new(MatchDecision.Unknown, "seniority_conflict");
        if (!wanted && (seniorTitle || excludedRole || explicitLevelExclusion)) return new(MatchDecision.Exclude, "seniority_excluded");
        var brazil = job.CountryCode?.Equals("BR", StringComparison.OrdinalIgnoreCase) == true;
        if (job.Mode is WorkMode.Remote or WorkMode.Unknown
            && Regex.IsMatch(description, @"\b(us only|usa only|united states only|canada only|canada apenas|portugal only|europe only|somente (?:nos )?eua|must (?:reside|be based) in (?:the )?(?:us|united states|canada|portugal))\b"))
            return new(MatchDecision.Exclude, "remote_country_restriction");
        if (job.Mode == WorkMode.Unknown)
        {
            if (job.CountryCode is not null && !brazil) return new(MatchDecision.Exclude, "outside_country");
            if (brazil || Regex.IsMatch(location, @"^sao paulo\s*[,/-]\s*(?:sp|sao paulo)(?:\s*,\s*(?:brazil|brasil|br))?$"))
                return new(MatchDecision.Include, "matched_mode_unverified");
            return new(MatchDecision.Unknown, "location_unverified");
        }
        if (job.Mode == WorkMode.Remote)
        {
            if (job.CountryCode is null) return new(MatchDecision.Unknown, "remote_country_missing");
            if (!brazil) return new(MatchDecision.Exclude, "outside_country");
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
