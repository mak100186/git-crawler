using System.Net;
using System.Net.Mail;

namespace GitCrawler.Api.Features.Digest.SendDigest;

// Sole implementation of IEmailSender (Architecture §3 "Email provider (SMTP)"). Uses the BCL's own
// System.Net.Mail.SmtpClient rather than pulling in MailKit or another package - this feature's send
// shape is a single plain-text message with no attachments/HTML/OAuth requirement, well within what
// SmtpClient already covers with zero new dependencies (Task Packet's own "your call, document it"
// on the client choice - simplicity favored over a package for functionality already in the BCL).
public class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        var fromAddress = configuration["Smtp:FromAddress"];

        // Read at call time, not cached in a constructor field the way LmStudioRepositorySummarizer
        // caches _model - an unset Smtp:Host/FromAddress is a legitimate "not configured yet" v1
        // state (see SendDigestCommandHandler's own Digest:RecipientEmail comment: no operator
        // onboarding flow collects this yet), not a fatal misconfiguration the process can't run
        // without. Throwing here, inside the try/catch SendDigestCommandHandler already wraps every
        // send call in, means a missing SMTP config is logged and skipped exactly like a real
        // network failure (FR-006) instead of crashing DI's handler-construction step before that
        // catch block ever gets a chance to run.
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("Smtp:Host and Smtp:FromAddress must both be configured to send the digest email.");
        }

        // TryParse rather than GetValue<T>: docker-compose.yml passes Smtp__Port/Smtp__EnableSsl
        // through unconditionally, so they arrive as empty strings when the operator hasn't set
        // them in .env - and GetValue<int>/<bool> throw on an empty string rather than falling back
        // to the default. An unusable value here means "not configured", same as an unset one.
        var port = int.TryParse(configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        var enableSsl = !bool.TryParse(configuration["Smtp:EnableSsl"], out var configuredSsl) || configuredSsl;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
        };

        // Credentials only set when configured - a local/dev SMTP relay (e.g. a catch-all test
        // server) commonly needs none, mirroring LmStudioRepositorySummarizer's own "no auth header
        // needed" case for a local service.
        if (!string.IsNullOrEmpty(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var mailMessage = new MailMessage(fromAddress, message.To, message.Subject, message.Body)
        {
            // MailMessage defaults IsBodyHtml to false regardless of the body content itself - an
            // HTML body sent without this set renders as literal markup text in Mailpit/any real
            // client instead of being parsed, which is exactly what EmailMessage.IsHtml exists to
            // avoid getting wrong silently.
            IsBodyHtml = message.IsHtml,
        };

        await client.SendMailAsync(mailMessage, cancellationToken);
    }
}