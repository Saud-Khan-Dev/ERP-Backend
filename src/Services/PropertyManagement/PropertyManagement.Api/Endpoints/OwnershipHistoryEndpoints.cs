public sealed record HistoryRequest(IReadOnlyList<HistoryStepInput> Steps, string? Remarks = null);
public sealed record HistoryReasonRequest(string Reason);
public sealed record CorrectFirstOwnersRequest(IReadOnlyList<OwnershipInput> Owners, string Reason);
public sealed record CorrectHistoryTransferRequest(Guid TransferTypeId, DateOnly TransferDate, DatePrecision Precision, string? ReferenceNo, string? Relationship, string Reason);

/// A property's ownership from the old records: entered by a clerk, registered once by a second officer; its timeline,
/// and corrections of what came from the old papers.
public class OwnershipHistoryEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var properties = app.MapGroup("/properties").WithTags("Ownership history");

    properties.MapGet("/{id:guid}/ownership-history", async (Guid id, ISender sender) => (await sender.Send(new GetPropertyHistoryQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetPropertyOwnershipHistory")
      .Produces<PropertyHistoryResult>()
      .WithSummary("Ownership History Entries Of A Property")
      .WithDescription("Every history entered for the property, newest first, and whether one may be entered now (nobody on the register, no allotment, none waiting).");

    properties.MapPost("/{id:guid}/ownership-history", async (Guid id, HistoryRequest request, ISender sender) =>
        (await sender.Send(new RecordHistoryCommand(id, request.Steps, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RecordOwnershipHistory")
      .Produces<HistoryEntryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Enter An Ownership History")
      .WithDescription("The first owners on record, then each transfer, oldest first. It is checked step by step now and waits for a second officer; nothing changes hands until then.");

    properties.MapGet("/{id:guid}/ownership-timeline", async (Guid id, ISender sender) => (await sender.Send(new GetOwnershipTimelineQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetOwnershipTimeline")
      .Produces<OwnershipTimelineResult>()
      .WithSummary("Ownership Timeline")
      .WithDescription("First owners, confirmed allottees and completed transfers, oldest first; owners numbered #1, #2 … with today's holders marked; each transfer's papers.");

    properties.MapPut("/{id:guid}/first-owners", async (Guid id, CorrectFirstOwnersRequest request, ISender sender) =>
        (await sender.Send(new CorrectFirstOwnersCommand(id, request.Owners, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("CorrectFirstOwners")
      .Produces<HistoryEntryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct The First Owners")
      .WithDescription("Who and what share (together 100%), with the reason - only while no transfer came after them, and not by who entered them (PROPERTY_TRANSFER).");

    var history = app.MapGroup("/ownership-history").WithTags("Ownership history");

    history.MapGet("/", async (ISender sender) => (await sender.Send(new GetWaitingHistoriesQuery())).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetWaitingOwnershipHistories")
      .Produces<WaitingHistoriesResult>()
      .WithSummary("Ownership Histories Waiting")
      .WithDescription("Entries waiting for a second officer, oldest first (the Decisions queue).");

    history.MapPut("/{id:guid}", async (Guid id, HistoryRequest request, ISender sender) =>
        (await sender.Send(new ReviseHistoryCommand(id, request.Steps, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ReviseOwnershipHistory")
      .Produces<HistoryEntryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Put An Ownership History Right")
      .WithDescription("A waiting or rejected entry; it waits for a second officer again.");

    history.MapPost("/{id:guid}/register", async (Guid id, ISender sender) => (await sender.Send(new RegisterHistoryCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RegisterOwnershipHistory")
      .Produces<HistoryEntryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register An Ownership History")
      .WithDescription("Writes the first owners and the completed transfers (TRF-…). Not by who entered or last changed it (PROPERTY_TRANSFER).");

    history.MapPost("/{id:guid}/reject", async (Guid id, HistoryReasonRequest request, ISender sender) =>
        (await sender.Send(new RejectHistoryCommand(id, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RejectOwnershipHistory")
      .Produces<HistoryEntryResult>()
      .WithSummary("Send An Ownership History Back")
      .WithDescription("With what is wrong; the clerk puts it right and sends it again.");

    history.MapPost("/{id:guid}/withdraw", async (Guid id, HistoryReasonRequest request, ISender sender) =>
        (await sender.Send(new WithdrawHistoryCommand(id, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("WithdrawOwnershipHistory")
      .Produces<HistoryEntryResult>()
      .WithSummary("Withdraw An Ownership History");

    app.MapPut("/transfers/{id:guid}/history", async (Guid id, CorrectHistoryTransferRequest request, ISender sender) =>
        (await sender.Send(new CorrectHistoryTransferCommand(id, request.TransferTypeId, request.TransferDate, request.Precision, request.ReferenceNo, request.Relationship, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithTags("Ownership history")
      .WithName("CorrectHistoryTransfer")
      .Produces<HistoryEntryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct A Transfer From The Old Records")
      .WithDescription("Its date (and how well it is known), kind, deed and relationship, with the reason; it stays between the steps around it. Not by who entered its history.");
  }
}
