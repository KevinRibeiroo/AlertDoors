using System.Net;
using AlertDoors.Jobs;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
namespace AlertDoors.Notifications;
public sealed record SmtpOptions(string Host, int Port, string? User, string? Password, string From, string To, bool AllowLocalInsecure = false);
public sealed class EmailNotifier : INotifier
{
    private readonly SmtpOptions options;
    private readonly TimeProvider clock;
    public EmailNotifier(SmtpOptions options, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(options.Host) || options.Port is < 1 or > 65535
            || (options.AllowLocalInsecure && options.Host is not ("localhost" or "127.0.0.1" or "::1")))
            throw new ArgumentException("Invalid SMTP configuration.");
        _ = MailboxAddress.Parse(options.From); _ = MailboxAddress.Parse(options.To);
        this.options = options; this.clock = clock;
    }
    public Channel Channel => Channel.Email;
    public IReadOnlyList<NotificationBatch> Prepare(IReadOnlyList<JobPosting> jobs) => MessageFormatter.Prepare(Channel, jobs);
    public async Task<SendReceipt> SendAsync(NotificationBatch batch, CancellationToken ct)
    {
        if (batch.Channel != Channel) throw new ArgumentException("Invalid email batch.");
        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.From)); message.To.Add(MailboxAddress.Parse(options.To));
        message.Subject = batch.Subject;
        message.Body = new BodyBuilder { TextBody = batch.Body, HtmlBody = "<pre>" + WebUtility.HtmlEncode(batch.Body) + "</pre>" }.ToMessageBody();
        message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();
        using var smtp = new SmtpClient { Timeout = 20000 };
        var tls = options.AllowLocalInsecure ? SecureSocketOptions.None : options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(options.Host, options.Port, tls, ct);
        if (!string.IsNullOrEmpty(options.User)) await smtp.AuthenticateAsync(options.User, options.Password ?? throw new ArgumentException("SMTP password is missing."), ct);
        await smtp.SendAsync(message, ct);
        // SMTP accepted the message. A failure while disconnecting must not turn it back into a pending delivery.
        try { await smtp.DisconnectAsync(true, ct); } catch (IOException) { } catch (OperationCanceledException) { }
        return new(message.MessageId, clock.GetUtcNow());
    }
}
