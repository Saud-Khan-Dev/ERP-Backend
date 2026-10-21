public sealed record CancelDisposalRequest(string Reason);

/// The disposal's companions: the book value a disposal would record on a date, and taking a mistaken disposal off.
public class DisposalActs : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/book-value", async (Guid id, DateOnly? asOf, ISender sender) =>
    {
      var result = await sender.Send(new GetBookValueQuery(id, asOf));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });
      return Results.Ok(result.Value);
    })
      .RequirePermission(PermissionCatalog.AssetFinance.View)

      .WithName("GetAssetBookValue")
      .Produces<GetBookValueQueryResult>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Book Value")
      .WithDescription("The book value on asOf (default today) as one figure for the record, the disposal and the reports: cost - every posted month not reversed, across all the asset's schedules. monthsToPost / amountToPost: the months up to asOf a disposal on that day would post first.");

    app.MapPost("/assets/{id}/disposal/cancel", async (Guid id, CancelDisposalRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CancelDisposalCommand(id, request.Reason));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });
      return Results.Ok(result.Value);
    })
      .RequirePermission(PermissionCatalog.AssetFinance.Approve)

      .WithName("CancelAssetDisposal")
      .Produces<CancelDisposalCommandResult>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Cancel Disposal")
      .WithDescription("Takes off a disposal recorded by mistake, with a reason: the asset goes back to its earlier status, a schedule the disposal stopped runs again and \"Disposal cancelled\" goes on its timeline. Months the disposal posted stay posted.");
  }
}
