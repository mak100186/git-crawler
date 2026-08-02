using Wolverine;

namespace GitCrawler.Api.Features.Categories.GetCategories;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Diagnostics/Ping/PingEndpoint.cs.
public static class GetCategoriesEndpoint
{
    public static IEndpointRouteBuilder MapGetCategoriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", async (IMessageBus bus) =>
                await bus.InvokeAsync<GetCategoriesResult>(new GetCategoriesQuery()))
            .WithName("GetCategories");

        return app;
    }
}