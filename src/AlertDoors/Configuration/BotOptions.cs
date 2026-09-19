using AlertDoors.Notifications;
namespace AlertDoors.Configuration;
public sealed record BotOptions(string ProjectId, bool Development, Uri? DiscordWebhook, SmtpOptions? Smtp);
public static class OptionsValidator
{
    public static BotOptions Load(Func<string, string?> get)
    {
        string Required(string key) => string.IsNullOrWhiteSpace(get(key)) ? throw new ArgumentException("Missing configuration: " + key) : get(key)!;
        bool Flag(string key)
        {
            var value = get(key);
            if (string.IsNullOrEmpty(value)) return false;
            if (!bool.TryParse(value, out var parsed)) throw new ArgumentException("Expected true or false: " + key);
            return parsed;
        }
        var project = Required("GOOGLE_CLOUD_PROJECT");
        var mode = get("ALERTDOORS_MODE") ?? "Production";
        if (mode is not ("Production" or "Development")) throw new ArgumentException("Invalid ALERTDOORS_MODE.");
        var development = mode == "Development";
        var emulator = !string.IsNullOrWhiteSpace(get("FIRESTORE_EMULATOR_HOST"));
        if (development != emulator) throw new ArgumentException("Development requires an emulator; Production forbids it.");
        Uri? webhook = null;
        if (Flag("DISCORD_ENABLED"))
        {
            if (!Uri.TryCreate(Required("DISCORD_WEBHOOK_URL"), UriKind.Absolute, out webhook)) throw new ArgumentException("Invalid DISCORD_WEBHOOK_URL.");
            using var validationClient = new HttpClient();
            _ = new DiscordNotifier(validationClient, webhook, TimeProvider.System);
        }
        SmtpOptions? smtp = null;
        if (Flag("EMAIL_ENABLED"))
        {
            if (!int.TryParse(Required("SMTP_PORT"), out var port)) throw new ArgumentException("Invalid SMTP_PORT.");
            var insecure = Flag("SMTP_ALLOW_LOCAL_INSECURE");
            if (insecure && !development) throw new ArgumentException("Insecure SMTP is forbidden in Production.");
            var user = get("SMTP_USER");
            var password = string.IsNullOrEmpty(user) ? null : Required("SMTP_PASSWORD");
            smtp = new(Required("SMTP_HOST"), port, user, password, Required("EMAIL_FROM"), Required("EMAIL_TO"), insecure);
            _ = new EmailNotifier(smtp, TimeProvider.System);
        }
        if (webhook is null && smtp is null) throw new ArgumentException("Enable and configure at least one notification channel.");
        return new(project, development, webhook, smtp);
    }
}
