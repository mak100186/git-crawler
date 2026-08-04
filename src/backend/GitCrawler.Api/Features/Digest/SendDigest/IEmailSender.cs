namespace GitCrawler.Api.Features.Digest.SendDigest;

// ADR-001's provider-agnostic abstraction pattern, mirrored here for outbound email the same way
// IRepositorySummarizer decouples GenerateSummariesCommandHandler from LM Studio specifically
// (Features/Summarization/GenerateSummaries/IRepositorySummarizer.cs): SendDigestCommandHandler is
// only ever allowed to reach an SMTP server through this contract, never a direct SmtpClient call,
// so a unit test can substitute a fake and never touch a real mail server. SmtpEmailSender is the
// sole implementation today.
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

// Exactly the content a digest send needs - a plain To/Subject/Body(/IsHtml), not a
// System.Net.Mail.MailMessage - so this contract stays decoupled from the specific mail client
// SmtpEmailSender happens to use underneath (same "expose only what the caller needs" rationale as
// RepositorySummarizationContext's own comment). IsHtml defaults to false so any future plain-text
// caller doesn't have to think about it.
public record EmailMessage(string To, string Subject, string Body, bool IsHtml = false);