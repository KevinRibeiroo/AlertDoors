using AlertDoors.Jobs;
namespace AlertDoors.Tests;
public class FilterTests
{
    internal static JobPosting Sample(string title = "Desenvolvedor .NET Pleno") => new("linkedin:12345", new("https://www.linkedin.com/jobs/view/12345/"), title, "Empresa Exemplo", "São Paulo, São Paulo, Brazil", "BR", WorkMode.Hybrid, null, null, DateTimeOffset.UtcNow, "ASP.NET Core");
    [Theory]
    [InlineData("Desenvolvedor .NET Júnior", MatchDecision.Include)]
    [InlineData("Desenvolvedor C# Jr.", MatchDecision.Include)]
    [InlineData("Desenvolvedor .NET Pleno", MatchDecision.Include)]
    [InlineData("Desenvolvedor C# Pl.", MatchDecision.Include)]
    [InlineData("Desenvolvedor .NET Pleno / Sênior", MatchDecision.Include)]
    [InlineData("Mid-level .NET Developer", MatchDecision.Include)]
    [InlineData("Desenvolvedor .NET Sênior", MatchDecision.Exclude)]
    [InlineData("Estágio .NET", MatchDecision.Exclude)]
    [InlineData("Tech Lead .NET", MatchDecision.Exclude)]
    [InlineData("Desenvolvedor .NET", MatchDecision.Include)]
    [InlineData("Desenvolvedor .NET Júnior / Sênior", MatchDecision.Include)]
    public void FiltersExplicitSeniority(string title, MatchDecision expected) => Assert.Equal(expected, new JobFilter().Evaluate(Sample(title)).Decision);
    [Theory]
    [InlineData("QA Engineer .NET Pleno", "ASP.NET Core")]
    [InlineData("Analista de Dados Pleno", "ASP.NET Core; colaboração com desenvolvedores")]
    [InlineData("QA Engineer .NET Pleno", "Parceria com analista de sistemas; ASP.NET Core")]
    public void ExcludesJobsWithoutDeveloperOrSoftwareEngineerRole(string title, string description) =>
        Assert.Equal(MatchDecision.Exclude, new JobFilter().Evaluate(Sample(title) with { Description = description }).Decision);
    [Theory]
    [InlineData("Programador .NET Pleno", "C# e Angular")]
    [InlineData("Programadora C# Júnior", "ASP.NET Core e React")]
    [InlineData("Analista de Sistemas .NET Pleno", "ASP.NET Core e Angular")]
    [InlineData("Analista .NET Pleno", "ASP.NET Core e React")]
    [InlineData("Engenheiro .NET Pleno", "C# e Angular")]
    public void AcceptsRequestedRolesWithMixedStacks(string title, string description) =>
        Assert.Equal(MatchDecision.Include, new JobFilter().Evaluate(Sample(title) with { Description = description }).Decision);
    [Fact]
    public void MixedStackWithoutDotNetIsNotEligible() =>
        Assert.Equal(MatchDecision.Exclude, new JobFilter().Evaluate(Sample("Programador Pleno") with { Description = "Angular e React" }).Decision);
    [Theory]
    [InlineData("Backend .NET Pleno", "Vaga para Desenvolvedor .NET; ASP.NET Core")]
    [InlineData("Backend .NET Pleno", "Cargo: Engenheira de Software; C#")]
    [InlineData("Software Engineer .NET Mid-level", "ASP.NET Core")]
    [InlineData("Pessoa Desenvolvedora C# Pleno", "ASP.NET Core")]
    public void AcceptsDevelopmentRoleInTitleOrDescription(string title, string description) =>
        Assert.Equal(MatchDecision.Include, new JobFilter().Evaluate(Sample(title) with { Description = description }).Decision);
    [Fact]
    public void AcceptsGenderNeutralSoftwareEngineerTitle() =>
        Assert.Equal(MatchDecision.Include,
            new JobFilter().Evaluate(Sample("Engenheiro(a) de Software | Assistente e Júnior") with
            { Description = "Desenvolvimento com C#", SeniorityText = "Assistente" }).Decision);
    [Theory]
    [InlineData("São Paulo, SP", MatchDecision.Include)]
    [InlineData("São Paulo, São Paulo, Brasil", MatchDecision.Include)]
    [InlineData("São Paulo", MatchDecision.Unknown)]
    [InlineData("Campinas, São Paulo, Brazil", MatchDecision.Exclude)]
    [InlineData("Osasco, SP", MatchDecision.Exclude)]
    public void MatchesCityNotState(string location, MatchDecision expected) => Assert.Equal(expected, new JobFilter().Evaluate(Sample() with { Location = location }).Decision);
    [Theory]
    [InlineData("BR", MatchDecision.Include)]
    [InlineData("US", MatchDecision.Exclude)]
    [InlineData(null, MatchDecision.Unknown)]
    public void RemoteRequiresCountryEvidence(string? country, MatchDecision expected) => Assert.Equal(expected, new JobFilter().Evaluate(Sample() with { Mode = WorkMode.Remote, CountryCode = country }).Decision);
    [Fact]
    public void GenericMidSeniorDoesNotOverrideExplicitPleno() => Assert.Equal(MatchDecision.Include, new JobFilter().Evaluate(Sample() with { SeniorityText = "Mid-Senior level" }).Decision);
    [Fact]
    public void MixedPlenoSeniorTitleIsEligibleEvenWhenLinkedInCategorySaysSenior() =>
        Assert.Equal(MatchDecision.Include, new JobFilter().Evaluate(Sample("Desenvolvedor .NET Pleno / Sênior") with { SeniorityText = "Senior" }).Decision);
    [Fact]
    public void MidSeniorAloneStillAlertsWithUnverifiedSeniority() => Assert.Equal(MatchDecision.Include, new JobFilter().Evaluate(Sample("Desenvolvedor .NET") with { SeniorityText = "Mid-Senior level" }).Decision);
    [Fact]
    public void ConflictingLevelIsUnknown() => Assert.Equal(MatchDecision.Unknown, new JobFilter().Evaluate(Sample() with { SeniorityText = "Director" }).Decision);
    [Fact]
    public void InternetIsNotDotNet() => Assert.Equal(MatchDecision.Exclude, new JobFilter().Evaluate(Sample("Analista pleno de internet") with { Description = "Rede e infraestrutura" }).Decision);
    [Fact]
    public void MissingModeStillAlertsWhenLocationIsBrazil() => Assert.Equal(MatchDecision.Include, new JobFilter().Evaluate(Sample() with { Mode = WorkMode.Unknown }).Decision);
    [Fact]
    public void MissingModeWithForeignCountryDoesNotAlert() => Assert.Equal(MatchDecision.Exclude, new JobFilter().Evaluate(Sample() with { Mode = WorkMode.Unknown, CountryCode = "US", Location = "United States" }).Decision);
    [Fact]
    public void MissingModeWithoutBrazilEvidenceNeedsReview() => Assert.Equal(MatchDecision.Unknown, new JobFilter().Evaluate(Sample() with { Mode = WorkMode.Unknown, CountryCode = null, Location = "" }).Decision);
    [Fact]
    public void RemoteUsRestrictionOverridesBrazilListing() => Assert.Equal(MatchDecision.Exclude, new JobFilter().Evaluate(Sample() with { Mode = WorkMode.Remote, Description = "Remote: US only. ASP.NET" }).Decision);
    [Theory]
    [InlineData("Remote: Canada only.")]
    [InlineData("Must be based in Portugal.")]
    public void RemoteOtherCountryRestrictionOverridesBrazilListing(string description) => Assert.Equal(MatchDecision.Exclude, new JobFilter().Evaluate(Sample() with { Mode = WorkMode.Remote, Description = description }).Decision);
}
