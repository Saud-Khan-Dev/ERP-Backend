public sealed record ProposeDisposalRequest(
  Guid DisposalMethodId,
  DateOnly DisposalDate,
  Guid ToStatusId,
  decimal? DisposalValue = null,
  string? CurrencyCode = null,
  string? BuyerInfo = null,
  string? Reason = null);

public sealed record ProposeDisposalResponse(Guid DisposalId);

/// Step one of a disposal: the proposal.
public class DisposeAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/disposal", async (Guid id, ProposeDisposalRequest request, ISender sender) =>
    {
      var command = new ProposeDisposalCommand(
        id, request.DisposalMethodId, request.DisposalDate, request.ToStatusId,
        request.DisposalValue, request.CurrencyCode, request.BuyerInfo, request.Reason);

      var result = await sender.Send(command);

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Created($"/assets/{id}/disposal", new ProposeDisposalResponse(result.Value.DisposalId));
    })
      .RequireFinance(PermissionCatalog.AssetFinance.Create)

      .WithName("ProposeAssetDisposal")
      .Produces<ProposeDisposalResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Propose Disposal")
      .WithDescription("Step one (ASSET_FINANCE.CREATE): proposes how the asset leaves - method, date, final status, proceeds, buyer, reason. Nothing happens to the asset until another person approves it (POST /assets/{id}/disposal/approve). Refused while the asset is on loan, while another proposal waits, for a date before the purchase or after today, for proceeds the method does not allow and for a date with depreciation posted after it.");
  }
}
