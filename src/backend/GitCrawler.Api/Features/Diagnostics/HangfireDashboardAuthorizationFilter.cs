using Hangfire.Dashboard;

namespace GitCrawler.Api.Features.Diagnostics;

// Hangfire's dashboard has no authentication of its own - UseHangfireDashboard exposes admin
// actions (retry/delete/trigger jobs, full run history) to anyone who can reach the port unless an
// IDashboardAuthorizationFilter is supplied. This codebase has no user-account/auth system
// anywhere (single-operator v1 per PRD - the same assumption F-004's Bookmark feature already
// made), so inventing ASP.NET Core Identity/JWT/etc. just to gate one diagnostic page would be
// disproportionate (Task Packet Constraints). Instead: a shared secret sourced from config/
// environment, the same pattern GITHUB_TOKEN already uses (env-driven, never hardcoded -
// .env.example is the single source of truth for its default; see Program.cs's bridge and
// appsettings.json's blank baseline here). The operator supplies it once as a `key` query string
// parameter when opening the dashboard in a browser - Hangfire's dashboard is browser-navigated,
// not an API a script calls, so a query parameter is the practical mechanism here, not a login
// form or session/cookie infrastructure this single-operator tool doesn't otherwise need.
//
// A loopback-only check (RemoteIpAddress) was considered and rejected: this app's only documented
// deployment path (docker-compose.yml, `make up`) publishes its port directly to the host, and
// Docker Desktop's port-publishing proxy does not preserve 127.0.0.1 as the apparent remote
// address for host-browser requests - a loopback check would lock the operator out of their own
// deployment, not just unauthorized callers.
//
// Fails closed on every unclear path (NFR-003): no secret configured => deny (a forgotten/blank
// secret must not silently leave the dashboard open to anyone who can reach the port); missing or
// wrong `key` => deny. There is no branch anywhere in Authorize that returns true except an exact
// match against a genuinely configured secret.
public class HangfireDashboardAuthorizationFilter(IConfiguration configuration) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var configuredKey = configuration["Hangfire:DashboardAccessKey"];
        if (string.IsNullOrEmpty(configuredKey))
        {
            return false;
        }

        var suppliedKey = context.Request.GetQuery("key");

        return !string.IsNullOrEmpty(suppliedKey) && suppliedKey == configuredKey;
    }
}