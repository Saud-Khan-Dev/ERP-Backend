public sealed record GetAssetHistoryResponse(AssetHistoryPageDto History);

/// The asset's History: the audit trail's lines about the asset and everything under it.
public class GetAssetHistory : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/history", async (Guid id, HttpRequest request, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetHistoryQuery(id, AuditQueries.FilterFrom(request)));
      return Results.Ok(new GetAssetHistoryResponse(result.Value!.History));
    })
      .RequirePermission(PermissionCatalog.Assets.View)

      .WithName("GetAssetHistory")
      .Produces<GetAssetHistoryResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset History")
      .WithDescription("Who did what to the asset and everything under it (moves, purchase, documents, depreciation, valuations, disposals, field values), newest first, from the audit trail. Filters: before (continue a listing: strictly older), from / to (days), action, kind, search, pageSize (1-200, default 50). Without ASSET_FINANCE.VIEW the lines about money keep the act, who and when, but no figures (restricted = true).");
  }
}
