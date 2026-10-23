public sealed record DisposalReasonRequest(string Reason);
public sealed record ApproveDisposalRequest(string? Note = null);
public sealed record WithdrawDisposalRequest(string? Reason = null);

/// The disposal's companions: the book value a disposal would record on a date, step two (approve or reject), the
/// proposer taking a proposal back, and cancelling an approved disposal recorded by mistake.
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

    app.MapPost("/assets/{id}/disposal/approve", async (Guid id, ApproveDisposalRequest? request, ISender sender) =>
    {
      var result = await sender.Send(new ApproveDisposalCommand(id, request?.Note));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });
      return Results.Ok(result.Value);
    })
      .RequireFinance(PermissionCatalog.AssetFinance.Approve)

      .WithName("ApproveAssetDisposal")
      .Produces<ApproveDisposalCommandResult>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Approve Disposal")
      .WithDescription("Step two (ASSET_FINANCE.APPROVE): approves the proposal waiting on the asset - posts depreciation up to the disposal date, keeps the book value on the day, works out gain / loss = (proceeds or 0) - book value, closes the schedules, moves the asset to the proposed final status and writes the Disposed timeline line. Refused to the person who proposed it while the rule \"Disposals pass through two people\" is on.");

    app.MapPost("/assets/{id}/disposal/reject", async (Guid id, DisposalReasonRequest request, ISender sender) =>
    {
      var result = await sender.Send(new RejectDisposalCommand(id, request.Reason));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });
      return Results.Ok(result.Value);
    })
      .RequireFinance(PermissionCatalog.AssetFinance.Approve)

      .WithName("RejectAssetDisposal")
      .Produces<DisposalDecisionResult>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Reject Disposal")
      .WithDescription("An approver turns the proposal down with a reason; the asset stays as it is and the proposal stays on record as rejected.");

    app.MapPost("/assets/{id}/disposal/withdraw", async (Guid id, WithdrawDisposalRequest? request, ISender sender) =>
    {
      var result = await sender.Send(new WithdrawDisposalCommand(id, request?.Reason));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });
      return Results.Ok(result.Value);
    })
      .RequireFinance(PermissionCatalog.AssetFinance.Create)

      .WithName("WithdrawAssetDisposal")
      .Produces<DisposalDecisionResult>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Withdraw Disposal Proposal")
      .WithDescription("The person who proposed the disposal takes it back while it waits (anyone else is refused - an approver rejects it instead).");

    app.MapPost("/assets/{id}/disposal/cancel", async (Guid id, DisposalReasonRequest request, ISender sender) =>
    {
      var result = await sender.Send(new CancelDisposalCommand(id, request.Reason));
      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });
      return Results.Ok(result.Value);
    })
      .RequireFinance(PermissionCatalog.AssetFinance.Approve)

      .WithName("CancelAssetDisposal")
      .Produces<CancelDisposalCommandResult>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Cancel Disposal")
      .WithDescription("Cancels an approved disposal recorded by mistake, with a reason: the disposal stays on record marked cancelled, the asset goes back to its earlier status, a schedule the disposal stopped runs again and \"Disposal cancelled\" goes on its timeline. Months the disposal posted stay posted.");
  }
}
