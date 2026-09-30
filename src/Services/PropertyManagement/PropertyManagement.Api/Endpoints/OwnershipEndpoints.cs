public sealed record EndOwnershipRequest(DateOnly EffectiveTo, string? Remarks = null);
public sealed record RemarksRequest(string? Remarks = null);
public sealed record ApproveTransferRequest(string? ApprovedBy = null, DateOnly? ApprovalDate = null);
public sealed record CompleteTransferRequest(Guid? TenureTypeId = null);
public sealed record CancelTransferRequest(string? Reason = null);
public sealed record RecordCompletedTransferRequest(TransferInput Transfer, string? ApprovedBy = null, DateOnly? ApprovalDate = null, Guid? TenureTypeId = null);
public sealed record ReleaseEncumbranceRequest(DateOnly ReleaseDate, string? ReleaseReferenceNo = null);

/// Who owns what share, how it changed hands, and what charges sit on it.
public class OwnershipEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var properties = app.MapGroup("/properties").WithTags("Ownership");

    properties.MapGet("/{id:guid}/ownerships", async (Guid id, bool? includeHistory, ISender sender) =>
        (await sender.Send(new GetPropertyOwnershipsQuery(id, includeHistory ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetPropertyOwnerships")
      .Produces<GetOwnershipsQueryResult>()
      .WithSummary("Get Owners Of Property")
      .WithDescription("Current owners and the share allocated so far (at most 100%); includeHistory=true adds ended and disputed rows.");

    properties.MapPost("/{id:guid}/ownerships", async (Guid id, OwnershipInput ownership, ISender sender) =>
        (await sender.Send(new RegisterOwnershipCommand(id, ownership))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RegisterOwnership")
      .Produces<RegisterOwnershipCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register Ownership")
      .WithDescription("Records an owner's share directly (first owners, or the paper record). Refused if active shares would exceed 100%. Changes between owners go through a transfer.");

    var ownerships = app.MapGroup("/ownerships").WithTags("Ownership");

    ownerships.MapPost("/{id:guid}/end", async (Guid id, EndOwnershipRequest request, ISender sender) =>
        (await sender.Send(new EndOwnershipCommand(id, request.EffectiveTo, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("EndOwnership")
      .Produces<EndOwnershipCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("End Ownership")
      .WithDescription("Closes the period without a transfer. The row stays as ownership history.");

    ownerships.MapPost("/{id:guid}/dispute", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new SetOwnershipDisputeCommand(id, true, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("DisputeOwnership")
      .Produces<SetOwnershipDisputeCommandResult>()
      .WithSummary("Mark Ownership Disputed");

    ownerships.MapPost("/{id:guid}/resolve-dispute", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new SetOwnershipDisputeCommand(id, false, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ResolveOwnershipDispute")
      .Produces<SetOwnershipDisputeCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Resolve Ownership Dispute");

    // ---- transfers ----

    properties.MapPost("/{id:guid}/transfers", async (Guid id, TransferInput transfer, ISender sender) =>
        (await sender.Send(new InitiateTransferCommand(id, transfer))).ToCreated(r => $"/transfers/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("InitiateTransfer")
      .Produces<InitiateTransferCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Initiate Transfer")
      .WithDescription("TRF-00001 is generated. Transferors must be current owners giving no more than they hold, and both sides must add up to the same share. Gift and Inheritance need the relationship.");

    properties.MapPost("/{id:guid}/transfers/completed", async (Guid id, RecordCompletedTransferRequest request, ISender sender) =>
        (await sender.Send(new RecordCompletedTransferCommand(id, request.Transfer, request.ApprovedBy, request.ApprovalDate, request.TenureTypeId)))
          .ToCreated(r => $"/transfers/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RecordCompletedTransfer")
      .Produces<RecordCompletedTransferCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Completed Transfer")
      .WithDescription("Enters a transfer that already happened (e.g. from the paper register) as COMPLETED in one step, with the same share rules; ownership rows are closed and opened on the transfer date.");

    properties.MapGet("/{id:guid}/transfers", async (Guid id, TransferStatus? status, ISender sender) =>
        (await sender.Send(new GetPropertyTransfersQuery(id, status))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetPropertyTransfers")
      .Produces<GetPropertyTransfersQueryResult>()
      .WithSummary("Get Transfers Of Property");

    var transfers = app.MapGroup("/transfers").WithTags("Ownership");

    transfers.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetTransferQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetTransfer")
      .Produces<GetTransferQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Transfer");

    transfers.MapPost("/{id:guid}/approve", async (Guid id, ApproveTransferRequest request, ISender sender) =>
        (await sender.Send(new ApproveTransferCommand(id, request.ApprovedBy, request.ApprovalDate))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("ApproveTransfer")
      .Produces<ApproveTransferCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Approve Transfer");

    transfers.MapPost("/{id:guid}/complete", async (Guid id, CompleteTransferRequest request, ISender sender) =>
        (await sender.Send(new CompleteTransferCommand(id, request.TenureTypeId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("CompleteTransfer")
      .Produces<CompleteTransferCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Complete Transfer")
      .WithDescription("Changes ownership: the parties' current rows are closed on the transfer date and new rows opened with the resulting shares, pointing back at this transfer.");

    transfers.MapPost("/{id:guid}/cancel", async (Guid id, CancelTransferRequest request, ISender sender) =>
        (await sender.Send(new CancelTransferCommand(id, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("CancelTransfer")
      .Produces<CancelTransferCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Cancel Transfer");

    // ---- encumbrances ----

    properties.MapPost("/{id:guid}/encumbrances", async (Guid id, EncumbranceInput encumbrance, ISender sender) =>
        (await sender.Send(new RegisterEncumbranceCommand(id, encumbrance))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RegisterEncumbrance")
      .Produces<RegisterEncumbranceCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register Encumbrance")
      .WithDescription("A mortgage, lien or charge (Act s.6(4)(c)), optionally on one owner's share.");

    properties.MapGet("/{id:guid}/encumbrances", async (Guid id, EncumbranceStatus? status, ISender sender) =>
        (await sender.Send(new GetPropertyEncumbrancesQuery(id, status))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetPropertyEncumbrances")
      .Produces<GetPropertyEncumbrancesQueryResult>()
      .WithSummary("Get Encumbrances");

    var encumbrances = app.MapGroup("/encumbrances").WithTags("Ownership");

    encumbrances.MapPut("/{id:guid}", async (Guid id, EncumbranceInput encumbrance, ISender sender) =>
        (await sender.Send(new UpdateEncumbranceCommand(id, encumbrance))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateEncumbrance")
      .Produces<UpdateEncumbranceCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Encumbrance");

    encumbrances.MapPost("/{id:guid}/release", async (Guid id, ReleaseEncumbranceRequest request, ISender sender) =>
        (await sender.Send(new ReleaseEncumbranceCommand(id, request.ReleaseDate, request.ReleaseReferenceNo))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ReleaseEncumbrance")
      .Produces<ReleaseEncumbranceCommandResult>()
      .WithSummary("Release Encumbrance");

    encumbrances.MapPost("/{id:guid}/enforce", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new EnforceEncumbranceCommand(id, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("EnforceEncumbrance")
      .Produces<EnforceEncumbranceCommandResult>()
      .WithSummary("Enforce Encumbrance");
  }
}
