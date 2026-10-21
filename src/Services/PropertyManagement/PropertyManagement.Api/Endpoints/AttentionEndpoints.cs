using Microsoft.Extensions.Options;

/// How this service is set up, for the screens: whether the person who started a transfer may approve it, how far
/// ahead a transfer may be dated without confirming, the slope above which a survey warns.
public sealed record PropertySettingsDto(bool RequireSeparateApprover, int FarFutureTransferDays, decimal SteepSlopeDegrees);

public sealed record PropertySettingsResponse(PropertySettingsDto Settings);

/// The bell's queues and the service settings the screens read.
public class AttentionEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attention", async (ISender sender) => (await sender.Send(new GetAttentionQuery())).ToOk())
      .RequireSignedIn()
      .WithTags("Attention").WithName("GetAttention")
      .Produces<AttentionResponse>()
      .WithSummary("What waits for the caller")
      .WithDescription("Counts of the records waiting for an act, only for the queues the caller may act on (never 403): transfers.pending, "
        + "transfers.unregistered, appeals.overdue, plans.waiting, allotments.unconfirmed (PROPERTY.APPROVE); agreements.ending (60 days, nextDue), "
        + "agreements.overdue, hearings.week (nextDue), legal.next-passed, encroachments.open, fines.unpaid (PROPERTY.EDIT). Active properties only.");

    app.MapGet("/properties/{id:guid}/changes", async (Guid id, int? pageIndex, int? pageSize, ISender sender) =>
        (await sender.Send(new GetChangesQuery(new PaginationRequest(pageIndex ?? 0, pageSize ?? 50), PropertyId: id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Properties").WithName("GetPropertyChanges")
      .Produces<GetChangesQueryResult>()
      .WithSummary("Change log of a property")
      .WithDescription("Who changed what on the property and every record hanging off it, newest first: added / removed records, and per fact old -> new "
        + "(list values and owners by name). changedBy is the Identity user id; changedByName the account name as it stood. pageSize 1-500.");

    app.MapGet("/owners/{id:guid}/changes", async (Guid id, int? pageIndex, int? pageSize, ISender sender) =>
        (await sender.Send(new GetChangesQuery(new PaginationRequest(pageIndex ?? 0, pageSize ?? 50), OwnerId: id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Owners").WithName("GetOwnerChanges")
      .Produces<GetChangesQueryResult>()
      .WithSummary("Change log of an owner")
      .WithDescription("Who changed the owner's own record (name, CNIC, NTN, type ...), newest first.");

    app.MapGet("/property-settings", (IOptions<TransferOptions> transfers) =>
        Results.Ok(new PropertySettingsResponse(new PropertySettingsDto(
          transfers.Value.RequireSeparateApprover, PropertyTransfer.FarFutureDays, PropertyBoundary.SteepSlopeDegrees))))
      .RequireAnyPermission(PermissionCatalog.Property.View, PermissionCatalog.PropertySetup.View)
      .WithTags("Attention").WithName("GetPropertySettings")
      .Produces<PropertySettingsResponse>()
      .WithSummary("Property settings")
      .WithDescription("requireSeparateApprover: the one who started a transfer cannot approve it (Transfers:RequireSeparateApprover); "
        + "farFutureTransferDays: a transfer dated further ahead needs confirmFarFuture; steepSlopeDegrees: the BCR 2026 slope limit.");
  }
}
