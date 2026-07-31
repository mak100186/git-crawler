using GitCrawler.Api.Features.Diagnostics.Ping;

namespace GitCrawler.Api.Tests;

// Placeholder smoke test proving the xUnit harness is wired into the solution and can reference
// GitCrawler.Api's compiled output (Directory.Build.props/csproj reference, test discovery via
// `dotnet test --list-tests`). No feature logic exists yet to test substantively - later features
// add real coverage here, this only proves the wiring.
public class SmokeTests
{
    [Fact]
    public void PingQueryHandler_Handle_ReturnsOkStatus()
    {
        var handler = new PingQueryHandler();

        var result = handler.Handle(new PingQuery());

        Assert.Equal("ok", result.Status);
    }
}