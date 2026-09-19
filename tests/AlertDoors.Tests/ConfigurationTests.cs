using AlertDoors.Configuration;
namespace AlertDoors.Tests;
public class ConfigurationTests
{
    [Fact]
    public void RealRunRequiresAtLeastOneConfiguredDestination() => Assert.Throws<ArgumentException>(() => OptionsValidator.Load(k => k == "GOOGLE_CLOUD_PROJECT" ? "example-project" : null));
    [Fact]
    public void ProductionDoesNotSilentlyUseEmulator()
    {
        var values = new Dictionary<string, string> { ["GOOGLE_CLOUD_PROJECT"] = "example-project", ["FIRESTORE_EMULATOR_HOST"] = "localhost:8080", ["DISCORD_ENABLED"] = "true", ["DISCORD_WEBHOOK_URL"] = "https://discord.com/api/webhooks/123/fake" };
        Assert.Throws<ArgumentException>(() => OptionsValidator.Load(k => values.GetValueOrDefault(k)));
    }
    [Fact]
    public void RejectsExternalInsecureSmtp()
    {
        var values = new Dictionary<string, string> { ["GOOGLE_CLOUD_PROJECT"] = "example-project", ["ALERTDOORS_MODE"] = "Development", ["FIRESTORE_EMULATOR_HOST"] = "localhost:8080", ["EMAIL_ENABLED"] = "true", ["SMTP_HOST"] = "smtp.example.test", ["SMTP_PORT"] = "1025", ["SMTP_ALLOW_LOCAL_INSECURE"] = "true", ["EMAIL_FROM"] = "sender@example.test", ["EMAIL_TO"] = "target@example.test" };
        Assert.Throws<ArgumentException>(() => OptionsValidator.Load(k => values.GetValueOrDefault(k)));
    }
}
