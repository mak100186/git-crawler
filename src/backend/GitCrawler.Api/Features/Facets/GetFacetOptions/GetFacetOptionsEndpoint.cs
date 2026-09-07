using Wolverine;

namespace GitCrawler.Api.Features.Facets.GetFacetOptions;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Categories/GetCategories/GetCategoriesEndpoint.cs.
public static class GetFacetOptionsEndpoint
{
    public static IEndpointRouteBuilder MapGetFacetOptionsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/facets", async (IMessageBus bus) =>
                await bus.InvokeAsync<GetFacetOptionsResult>(new GetFacetOptionsQuery()))
            .WithName("GetFacetOptions");

        return app;
    }
}