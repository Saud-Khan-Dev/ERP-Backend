public sealed record DisposeAssetRequest(
  Guid DisposalMethodId,
  DateOnly DisposalDate,
  Guid ToStatusId,
  Guid EventTypeId,
  decimal? DisposalValue = null,
  string? CurrencyCode = null,
  string? BuyerInfo = null,
  string? Reason = null,
  Guid? ApprovedBy = null,
  DateTime? ApprovedAt = null,
  Guid? PerformedBy = null);

public sealed record DisposeAssetResponse(Guid DisposalId);

public class DisposeAsset : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/disposal", async (Guid id, DisposeAssetRequest request, ISender sender) =>
    {
      var command = new DisposeAssetCommand(
        id, request.DisposalMethodId, request.DisposalDate, request.ToStatusId, request.EventTypeId,
        request.DisposalValue, request.CurrencyCode, request.BuyerInfo, request.Reason,
        request.ApprovedBy, request.ApprovedAt, request.PerformedBy);

      var result = await sender.Send(command);

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<DisposeAssetResponse>();
      return Results.Created($"/assets/{id}/disposal", response);
    })
      .WithName("DisposeAsset")
      .Produces<DisposeAssetResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Dispose Asset")
      .WithDescription("Records the disposal (gain / loss against net book value), closes depreciation, moves the asset to a terminal status and logs the lifecycle event.");
  }
}
