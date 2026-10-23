public sealed record GetAssetDisposalResponse(AssetDisposalDto Disposal);
public sealed record GetAssetDisposalsResponse(IReadOnlyList<AssetDisposalDto> Disposals);

public class GetAssetDisposal : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/disposal", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetDisposalQuery(id));
      return Results.Ok(new GetAssetDisposalResponse(result.Value!.Disposal));
    })
      .RequirePermission(PermissionCatalog.AssetFinance.View)

      .WithName("GetAssetDisposal")
      .Produces<GetAssetDisposalResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Disposal")
      .WithDescription("The asset's live disposal: the proposal waiting for approval (state Proposed) or the approved one (Approved). 404 when there is neither.");

    app.MapGet("/assets/{id}/disposals", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetDisposalsQuery(id));
      return Results.Ok(new GetAssetDisposalsResponse(result.Value!.Disposals));
    })
      .RequirePermission(PermissionCatalog.AssetFinance.View)

      .WithName("GetAssetDisposals")
      .Produces<GetAssetDisposalsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Asset Disposals")
      .WithDescription("Every disposal of the asset, newest first: the live one and those rejected, withdrawn or cancelled.");
  }
}
