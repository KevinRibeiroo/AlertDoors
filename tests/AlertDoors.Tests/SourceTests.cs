using AlertDoors.Sources;

namespace AlertDoors.Tests;

public class SourceTests
{
    private static readonly DateTimeOffset Observed = DateTimeOffset.Parse("2026-09-19T12:00:00Z");
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void ExtractsCanonicalIdentityAndKeepsMissingFieldsUnknown()
    {
        var result = new LinkedInPageParser().Parse(Fixture("linkedin-jobs.html"), Observed);
        Assert.Equal(SourceStatus.Success, result.Status);
        var job = Assert.Single(result.Jobs);
        Assert.Equal("linkedin:12345", job.Id);
        Assert.Equal("https://www.linkedin.com/jobs/view/12345/", job.Url.AbsoluteUri);
        Assert.Equal("Desenvolvedor .NET Júnior", job.Title);
        Assert.Equal("Empresa Exemplo", job.Company);
        Assert.Equal("BR", job.CountryCode);
        Assert.Null(job.PublishedAt);
        Assert.Equal(AlertDoors.Jobs.WorkMode.Unknown, job.Mode);
    }

    [Theory]
    [InlineData("<html><body>unexpected response</body></html>")]
    [InlineData("<ul class='jobs-search__results-list'></ul>")]
    public void UnrecognizedOrAmbiguousHtmlIsNotEmptySuccess(string html) =>
        Assert.Equal(SourceStatus.SchemaChanged, new LinkedInPageParser().Parse(html, Observed).Status);

    [Fact]
    public void LoginPageIsBlockedEvenWhenHttpWasSuccessful() =>
        Assert.Equal(SourceStatus.Blocked, new LinkedInPageParser().Parse(Fixture("linkedin-blocked.html"), Observed).Status);

    [Fact]
    public void ExplicitEmptyStateIsValid()
    {
        var result = new LinkedInPageParser().Parse(Fixture("linkedin-empty.html"), Observed);
        Assert.Equal(SourceStatus.Success, result.Status);
        Assert.Empty(result.Jobs);
    }

    [Fact]
    public void ExtractsCardWhenTheCardItselfIsTheJobLink()
    {
        var html = "<a class='job-search-card' data-entity-urn='urn:li:jobPosting:12345' href='https://br.linkedin.com/jobs/view/developer-12345'><h3 class='base-search-card__title'>.NET Pleno</h3><h4 class='base-search-card__subtitle'>Empresa Exemplo</h4></a>";
        var result = new LinkedInPageParser().Parse(html, Observed);
        Assert.Equal(SourceStatus.Success, result.Status);
        Assert.Equal("linkedin:12345", Assert.Single(result.Jobs).Id);
    }

    [Fact]
    public void DoesNotAcceptOffDomainJobLinks()
    {
        var html = Fixture("linkedin-jobs.html").Replace("br.linkedin.com", "linkedin.com.attacker.test");
        var result = new LinkedInPageParser().Parse(html, Observed);
        Assert.Equal(SourceStatus.SchemaChanged, result.Status);
        Assert.Empty(result.Jobs);
    }
}
