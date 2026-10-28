using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LMSFree.Infrastructure.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public sealed class SmtpOptions
{
    public const string Section = "Email:Smtp";

    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "LMSFree <no-reply@lmsfree.local>";
    public bool UseStartTls { get; set; } = true;
}

/// <summary>Writes mail to the log. Used when no SMTP host is configured.</summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> log) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        log.LogInformation("Email to {To}: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var o = options.Value;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(o.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(o.Host, o.Port, o.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
        if (!string.IsNullOrEmpty(o.User))
            await client.AuthenticateAsync(o.User, o.Password ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
