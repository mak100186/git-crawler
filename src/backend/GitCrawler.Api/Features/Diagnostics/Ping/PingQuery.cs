namespace GitCrawler.Api.Features.Diagnostics.Ping;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 —
// everything this operation needs lives in this folder.
public record PingQuery;

public record PingResult(string Status, DateTimeOffset ServerTimeUtc);

// Wolverine discovers this handler by convention (a public "Handle"/"HandleAsync" method on a
// class named *Handler in the same assembly) - no manual registration required.
public class PingQueryHandler
{
    public PingResult Handle(PingQuery query) => new("ok", DateTimeOffset.UtcNow);
}