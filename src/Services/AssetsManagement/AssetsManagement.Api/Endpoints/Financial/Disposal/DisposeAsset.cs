public sealed record DisposeAssetRequest(
  Guid DisposalMethodId,
  DateOnly DisposalDate,
  Guid ToStatusId,
  decimal? DisposalValue = null,
  string? CurrencyCode = null,
  string? BuyerInfo = null,
  string? Reason = null);

public sealed record DisposeAssetResponse(Guid DisposalId);

public class DisposeAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/disposal", async (Guid id, DisposeAssetRequest request, ISender sender) =>
    {
      var command = new DisposeAssetCommand(
        id, request.DisposalMethodId, request.DisposalDate, request.ToStatusId,
        request.DisposalValue, request.CurrencyCode, request.BuyerInfo, request.Reason);

      var result = await sender.Send(command);

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<DisposeAssetResponse>();
      return Results.Created($"/assets/{id}/disposal", response);
    })
      .RequirePermission(PermissionCatalog.AssetFinance.Approve)

      .WithName("DisposeAsset")
      .Produces<DisposeAssetResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Dispose Asset")
      .WithDescription("Records the disposal: depreciates up to the disposal date, closes the schedules, works out gain / loss = (proceeds or 0) - book value on the day, moves the asset to a terminal status and writes the Disposed timeline line. The person approving is the signed-in user. Refused while the asset is on loan, for a date before the purchase or after today, and for proceeds a method does not allow.");
  }
}
