using Microsoft.AspNetCore.Mvc;

public sealed record RecordHistoryRequest(IReadOnlyList<HistoryStepInput> Steps, string? Remarks = null);
public sealed record ReasonRequest(string Reason);
public sealed record CorrectTransferRequest(
  Guid TransferTypeId,
  DateOnly TransferDate,
  DatePrecision TransferDatePrecision,
  string? TransferReferenceNo,
  decimal? ConsiderationAmount,
  string? Relationship,
  string? ApprovalOrderRef,
  string? Remarks,
  string Reason);
public sealed record CorrectOwnershipRequest(
  Guid? AcquisitionTransferTypeId,
  string? ReferenceNo,
  string? Remarks,
  DateOnly EffectiveFrom,
  DatePrecision EffectiveFromPrecision,
  string Reason);
public sealed record OriginalRegistrationRequest(DateOnly? RegisteredOn, DatePrecision RegisteredOnPrecision, string? RegistrationReference, string? Reason = null);

/// A property's ownership from the old records (1960 onwards): the timeline, ownership history entries (entered once,
/// approved once), corrections to registered transfers and holdings, and the original registration.
public class OwnershipHistoryEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var properties = app.MapGroup("/properties").WithTags("Ownership history");

    properties.MapGet("/{id:guid}/ownership-timeline", async (Guid id, ISender sender) =>
        (await sender.Send(new GetOwnershipTimelineQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetOwnershipTimeline")
      .Produces<GetOwnershipTimelineResult>()
      .WithSummary("Get Ownership Timeline")
      .WithDescription("The property's ownership as one story, oldest first: its original registration, every owner numbered in the order "
        + "they came to hold it (Owner #1 ...) with their identity (CNIC, or an old NIC / passport / no ID; deceased), every event - the first "
        + "owners entered, each registered transfer (who gave, who received, deed no., price, relationship, approval order), allottees confirmed "
        + "as owners - with its papers, and the present holders. Dates carry their precision (exact, month, year, about). canEnterHistory: an "
        + "ownership history entry fits the property; waitingHistoryId: one already waits.");

    properties.MapPut("/{id:guid}/original-registration", async (Guid id, OriginalRegistrationRequest request, ISender sender) =>
        (await sender.Send(new SetOriginalRegistrationCommand(id, request.RegisteredOn, request.RegisteredOnPrecision, request.RegistrationReference, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("SetOriginalRegistration")
      .Produces<CorrectionResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Original Registration")
      .WithDescription("When and under what reference the property was first registered, as the old papers give it (registeredOn null: not "
        + "known). A month or year precision keeps the first day of it. Not in the future, and not after its first holding on record.");

    properties.MapGet("/{id:guid}/ownership-history", async (Guid id, HistoryEntryStatus? status, ISender sender) =>
        (await sender.Send(new GetHistoryEntriesQuery(id, status))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetPropertyHistoryEntries")
      .Produces<GetHistoryEntriesResult>()
      .WithSummary("Get Property Ownership History Entries")
      .WithDescription("The property's ownership history entries, newest first (status filters).");

    properties.MapPost("/{id:guid}/ownership-history", async (Guid id, RecordHistoryRequest request, ISender sender) =>
        (await sender.Send(new RecordHistoryCommand(id, request.Steps ?? Array.Empty<HistoryStepInput>(), request.Remarks))).ToCreated(r => $"/ownership-history/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RecordHistory")
      .Produces<HistoryEntryResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Ownership History")
      .WithDescription("A clerk enters the property's ownership from the old records in one go (HIS-00001): step 1 the first owner(s) on "
        + "record (shares 100% together), then every transfer oldest first (givers -> receivers, balanced). It is played forward now through "
        + "the transfer rules - nothing changes in the register - and waits for a second officer (status WAITING). While the PROPERTY_TRANSFER "
        + "rule is off, an officer with PROPERTY.APPROVE registers it at once (REGISTERED, transferNos). Fits a property with no earlier "
        + "holders or transfers in the register: nobody holds it, or its present holders were entered with nothing behind them - then the "
        + "history must end with exactly them (owners, shares, start dates). People with no CNIC (old NIC, passport, no ID) may appear on the "
        + "way; whoever is left holding today needs a CNIC unless deceased. Papers are filed per step afterwards.");

    var history = app.MapGroup("/ownership-history").WithTags("Ownership history");

    history.MapGet("/", async (HistoryEntryStatus? status, ISender sender) =>
        (await sender.Send(new GetHistoryEntriesQuery(null, status ?? HistoryEntryStatus.Waiting))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetHistoryEntries")
      .Produces<GetHistoryEntriesResult>()
      .WithSummary("Get Ownership History Entries")
      .WithDescription("Entries across the register in one status (default WAITING: the approver's queue, oldest first, at most 500).");

    history.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        (await sender.Send(new GetHistoryEntryQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetHistoryEntry")
      .Produces<GetHistoryEntryResult>()
      .WithSummary("Get Ownership History Entry");

    history.MapPut("/{id:guid}", async (Guid id, RecordHistoryRequest request, ISender sender) =>
        (await sender.Send(new ReviseHistoryCommand(id, request.Steps ?? Array.Empty<HistoryStepInput>(), request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ReviseHistory")
      .Produces<HistoryEntryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Revise Ownership History")
      .WithDescription("Changes a WAITING entry (its steps, and the papers named on each - only the entry's own); played forward again.");

    history.MapPost("/{id:guid}/steps/{step:int}/documents", async (Guid id, int step, [FromForm] UploadDocumentForm form, ISender sender) =>
    {
      await using var content = DocumentEndpoints.OpenUpload(form.File, out var upload);
      return (await sender.Send(new FileHistoryPaperCommand(id, step, form.DocumentTypeId, form.Details(), upload)))
        .ToCreated(r => $"/documents/{r.Id}");
    })
      .DisableAntiforgery()
      .Accepts<UploadDocumentForm>("multipart/form-data")
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("FileHistoryPaper")
      .Produces<UploadDocumentCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("File Ownership History Paper")
      .WithDescription("A paper (deed, mutation, court order ...) for one step of a WAITING entry (step 1 = the first owners). When the entry is "
        + "registered it moves to the transfer the step became (or to the first holding). The document type must apply to PROPERTY.");

    history.MapPost("/{id:guid}/register", async (Guid id, ISender sender) =>
        (await sender.Send(new RegisterHistoryCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RegisterHistory")
      .Produces<HistoryEntryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register Ownership History")
      .WithDescription("A second officer registers a WAITING entry: it is played forward against the property as it stands now and the "
        + "transfers (COMPLETED, new TRF numbers) and holdings are written; the present holders take on their history; papers move to their "
        + "steps' records. Refused for the officer who entered it while the PROPERTY_TRANSFER rule is on.");

    history.MapPost("/{id:guid}/reject", async (Guid id, ReasonRequest request, ISender sender) =>
        (await sender.Send(new CloseHistoryCommand(id, Reject: true, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RejectHistory")
      .Produces<HistoryEntryResult>()
      .WithSummary("Reject Ownership History")
      .WithDescription("Closes a WAITING entry without changing anything; the reason tells the clerk what to put right.");

    history.MapPost("/{id:guid}/withdraw", async (Guid id, ReasonRequest request, ISender sender) =>
        (await sender.Send(new CloseHistoryCommand(id, Reject: false, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("WithdrawHistory")
      .Produces<HistoryEntryResult>()
      .WithSummary("Withdraw Ownership History")
      .WithDescription("The clerk takes a WAITING entry back; nothing changes.");

    app.MapPost("/transfers/{id:guid}/corrections", async (Guid id, CorrectTransferRequest request, ISender sender) =>
        (await sender.Send(new CorrectTransferCommand(id, request.TransferTypeId, request.TransferDate, request.TransferDatePrecision,
          request.TransferReferenceNo, request.ConsiderationAmount, request.Relationship, request.ApprovalOrderRef, request.Remarks, request.Reason))).ToOk())
      .WithTags("Ownership history")
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("CorrectTransfer")
      .Produces<CorrectionResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Registered Transfer")
      .WithDescription("An approving officer puts a COMPLETED transfer right as the old papers show it: kind, date and its precision, deed / "
        + "mutation no., price, relationship, approval order, remarks; the reason is kept in the audit trail. A new date moves the holdings it "
        + "closed and opened, so it must stay after each closed holding began and before each opened one passed on.");

    app.MapPost("/ownerships/{id:guid}/corrections", async (Guid id, CorrectOwnershipRequest request, ISender sender) =>
        (await sender.Send(new CorrectOwnershipCommand(id, request.AcquisitionTransferTypeId, request.ReferenceNo, request.Remarks,
          request.EffectiveFrom, request.EffectiveFromPrecision, request.Reason))).ToOk())
      .WithTags("Ownership history")
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("CorrectOwnership")
      .Produces<CorrectionResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Holding")
      .WithDescription("An approving officer puts a holding right: how it was acquired, deed / mutation no., remarks - and, for a holding no "
        + "transfer or allotment opened (a first owner), its start date and precision. The reason is kept in the audit trail.");
  }
}
