/// The first owners of a property nobody holds, all at once (joint owners together): exactly 100% between them.
public sealed record RegisterFirstOwnersRequest(IReadOnlyList<OwnershipInput> Owners);
public sealed record RemarksRequest(string? Remarks = null);
/// The approver is the signed-in officer: there is no "approved by" to type.
public sealed record ApproveTransferRequest(DateOnly? ApprovalDate = null, string? ApprovalOrderRef = null, Guid? TenureTypeId = null, string? NoDuesCertificateNo = null);
public sealed record CompleteTransferRequest(Guid? TenureTypeId = null, string? ApprovalOrderRef = null);
public sealed record CancelTransferRequest(string Reason);
public sealed record RejectTransferRequest(string Reason);
public sealed record RecordCompletedTransferRequest(
  TransferInput Transfer, DateOnly? ApprovalDate = null, Guid? TenureTypeId = null, string? ApprovalOrderRef = null, string? NoDuesCertificateNo = null);
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
      .WithDescription("Current owners (active and disputed shares; a disputed share still counts) and the share allocated so far (at most 100%); includeHistory=true adds the ended rows.");

    properties.MapPost("/{id:guid}/ownerships", async (Guid id, RegisterFirstOwnersRequest request, ISender sender) =>
        (await sender.Send(new RegisterFirstOwnersCommand(id, request.Owners ?? Array.Empty<OwnershipInput>()))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RegisterFirstOwners")
      .Produces<RegisterFirstOwnersCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status403Forbidden)
      .WithSummary("Register First Owners")
      .WithDescription("Enters the owners of a property NOBODY holds now (no ACTIVE or DISPUTED share), all in one request - joint owners together - "
        + "with shares that add up to exactly 100%. Once a property is held, a new owner comes in only through a transfer (POST /properties/{id}/transfers, "
        + "or /transfers/completed for one that already happened), so this is refused (400) on a held property. On a property nobody has held yet the "
        + "clerk enters them from the register (PROPERTY.EDIT); on one that was held before it needs PROPERTY.APPROVE (403 otherwise). Also refused "
        + "while an allotment is in force (confirm the allottee as owner instead), for an owner listed twice, an effectiveFrom in the future or one "
        + "that overlaps the owner's earlier entries on the property. There is no act that ends a share by hand: a share ends through a transfer, or "
        + "through the cancellation or surrender of the allotment it was confirmed by.");

    var ownerships = app.MapGroup("/ownerships").WithTags("Ownership");

    ownerships.MapPost("/{id:guid}/dispute", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new SetOwnershipDisputeCommand(id, true, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.PropertyLegal.Edit)
      .WithName("DisputeOwnership")
      .Produces<SetOwnershipDisputeCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Mark Ownership Disputed")
      .WithDescription("The share stays held (it counts toward the 100%) but cannot be transferred until the dispute is resolved.");

    ownerships.MapPost("/{id:guid}/resolve-dispute", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new SetOwnershipDisputeCommand(id, false, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.PropertyLegal.Edit)
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
      .WithDescription("TRF-00001 is generated; the transfer waits for approval and nothing changes hands yet. Transferors must be current, undisputed, "
        + "uncharged owners giving no more than they hold minus what other pending transfers already give; both sides must add up to the same share and "
        + "the transfer must move a share. Every party must be an active owner. The date may be in the future (more than 90 days ahead only with "
        + "confirmFarFuture=true) but must fall after each party's earlier ownership entries. Gift and Inheritance need the relationship.");

    properties.MapPost("/{id:guid}/transfers/completed", async (Guid id, RecordCompletedTransferRequest request, ISender sender) =>
        (await sender.Send(new RecordCompletedTransferCommand(id, request.Transfer, request.ApprovalDate, request.TenureTypeId, request.ApprovalOrderRef, request.NoDuesCertificateNo)))
          .ToCreated(r => $"/transfers/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RecordCompletedTransfer")
      .Produces<RecordCompletedTransferCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Completed Transfer")
      .WithDescription("Enters a transfer that already happened (e.g. from the paper register), with the same rules as initiate + approve (transfer date "
        + "not in the future). While the PROPERTY_TRANSFER separation-of-duties rule is on (GET /property-settings requireSeparateApprover) it is saved "
        + "waiting for approval (status Initiated, awaitingApproval = true, recordedPast on the transfer) with the approval date, order and no-dues "
        + "certificate read off the paper kept on it, and another officer approves it; nothing changes hands until then. With the rule off it is "
        + "COMPLETED in one step: ownership rows are closed and opened on the transfer date, the signed-in officer as approver.");

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
        (await sender.Send(new ApproveTransferCommand(id, request.ApprovalDate, request.ApprovalOrderRef, request.TenureTypeId, request.NoDuesCertificateNo))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("ApproveTransfer")
      .Produces<ApproveTransferCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Approve Transfer")
      .WithDescription("Approves AND registers a transfer waiting for approval, in one step: the rules are checked again, the givers' rows are closed on the "
        + "transfer date and the resulting rows opened (status COMPLETED). The approver is the signed-in officer; while the PROPERTY_TRANSFER "
        + "separation-of-duties rule is on, the one who started or recorded the transfer cannot approve it (GET /property-settings). Refused before the "
        + "transfer date; approvalDate defaults to the one recorded with a past transfer, else today and may not be in the future or before the transfer date. noDuesCertificateNo: the Taxation clearance relied on (optional).");

    transfers.MapPost("/{id:guid}/reject", async (Guid id, RejectTransferRequest request, ISender sender) =>
        (await sender.Send(new RejectTransferCommand(id, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RejectTransfer")
      .Produces<RejectTransferCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Reject Transfer")
      .WithDescription("The approving officer refuses a transfer waiting for approval (status REJECTED). The reason is required; nothing changes hands.");

    transfers.MapPost("/{id:guid}/complete", async (Guid id, CompleteTransferRequest request, ISender sender) =>
        (await sender.Send(new CompleteTransferCommand(id, request.TenureTypeId, request.ApprovalOrderRef))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("CompleteTransfer")
      .Produces<CompleteTransferCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Complete Transfer")
      .WithDescription("Only for a transfer approved before approval and completion became one step (status APPROVED): registers it like an approval does. "
        + "A transfer waiting for approval is registered by approving it.");

    transfers.MapPost("/{id:guid}/cancel", async (Guid id, CancelTransferRequest request, ISender sender) =>
        (await sender.Send(new CancelTransferCommand(id, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.PropertyLegal.Edit)
      .WithName("CancelTransfer")
      .Produces<CancelTransferCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status403Forbidden)
      .WithSummary("Cancel Transfer")
      .WithDescription("Withdraws a transfer that has not changed ownership (status CANCELLED); the reason is required (PROPERTY_LEGAL.EDIT). A transfer still in status APPROVED "
        + "also needs PROPERTY.APPROVE (403 otherwise). A completed transfer is reversed with a new transfer.");

    // ---- encumbrances ----

    properties.MapPost("/{id:guid}/encumbrances", async (Guid id, EncumbranceInput encumbrance, ISender sender) =>
        (await sender.Send(new RegisterEncumbranceCommand(id, encumbrance))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RegisterEncumbrance")
      .Produces<RegisterEncumbranceCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register Encumbrance")
      .WithDescription("A mortgage, lien or charge (Act s.6(4)(c)), optionally on one owner's current share; startDate not in the future. While it is "
        + "ACTIVE the share (or, without ownershipId, every share) cannot be transferred.");

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
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("ReleaseEncumbrance")
      .Produces<ReleaseEncumbranceCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Release Encumbrance")
      .WithDescription("An approver releases the charge; releaseDate between the start date and today.");

    encumbrances.MapPost("/{id:guid}/enforce", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new EnforceEncumbranceCommand(id, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("EnforceEncumbrance")
      .Produces<EnforceEncumbranceCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Enforce Encumbrance")
      .WithDescription("The holder foreclosed. The holder must be linked to an owner record. The share stays in place until it is transferred to the "
        + "holder; approving that transfer releases the charge (reference = the transfer no.).");
  }
}
