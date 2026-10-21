/// The bell and the overview's "Needs attention": the Assets queues the caller may act on, with exact counts. Any
/// signed-in person may ask (never 403s); the keys are listed on GetAttentionQuery and in docs/claude/assets-management.md.
public class GetAttention : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attention", async (ISender sender) =>
    {
      var result = await sender.Send(new GetAttentionQuery());
      return Results.Ok(result.Value);
    })
      .RequireSignedIn()

      .WithName("GetAssetsAttention")
      .Produces<AttentionResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Attention")
      .WithDescription("Counts of the Assets queues the caller may act on: assets.loans-overdue, assets.location-missing, assets.out-of-service-long, assets.warranty-ending (nextDue), assets.depreciation-behind, assets.no-purchase and, for a person linked to an HRM employee, assets.in-my-custody (forMe).");
  }
}
