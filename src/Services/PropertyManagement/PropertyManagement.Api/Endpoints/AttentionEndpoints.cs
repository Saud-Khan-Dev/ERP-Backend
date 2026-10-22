/// How this service is set up, for the screens: whether the person who started (or recorded) a transfer may approve it
/// (RequireSeparateApprover = the PROPERTY_TRANSFER separation-of-duties rule, switched in Administration › Configuration;
/// read-only here), how far ahead a transfer may be dated without confirming, the slope above which a survey warns.
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

    app.MapGet("/properties/{id:guid}/history", async (Guid id, HttpRequest request, ISender sender) =>
        (await sender.Send(new GetHistoryQuery(id, null, AuditQueries.FilterFrom(request)))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Properties").WithName("GetPropertyHistory")
      .Produces<HistoryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("History of a property")
      .WithDescription("The audit trail of the property and every record on it (shares, transfers and their parties, charges, agreements, bids, legal "
        + "matters with parties and hearings, papers, custom-field values), newest first: who (name, address, device), when, what (added / changed field "
        + "old -> new / removed with its facts / the decisions: Approved, Rejected, Cancelled, Waived, Decided ... with the reason) and papers downloaded. "
        + "Query: before (an instant: lines strictly older, to continue), from / to (days, Pakistan time), action, search, pageSize 1-200 (default 50). "
        + "Without PROPERTY_LEGAL.VIEW the lines of encroachments, cases, appeals and of papers the caller may not see are left out (restrictedCount).");

    app.MapGet("/owners/{id:guid}/history", async (Guid id, HttpRequest request, ISender sender) =>
        (await sender.Send(new GetHistoryQuery(null, id, AuditQueries.FilterFrom(request)))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Owners").WithName("GetOwnerHistory")
      .Produces<HistoryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("History of an owner")
      .WithDescription("The audit trail of the owner's own record (name, CNIC, NTN, type ...), their contacts, addresses and own papers, newest first. "
        + "Same query as GET /properties/{id}/history.");

    app.MapGet("/property-settings", async (IDutyRules dutyRules, CancellationToken cancellationToken) =>
        Results.Ok(new PropertySettingsResponse(new PropertySettingsDto(
          await dutyRules.IsOnAsync(DutyRules.PropertyTransfer, cancellationToken), PropertyTransfer.FarFutureDays, PropertyBoundary.SteepSlopeDegrees))))
      .RequireAnyPermission(PermissionCatalog.Property.View, PermissionCatalog.PropertySetup.View)
      .WithTags("Attention").WithName("GetPropertySettings")
      .Produces<PropertySettingsResponse>()
      .WithSummary("Property settings")
      .WithDescription("requireSeparateApprover: the PROPERTY_TRANSFER separation-of-duties rule is on - the one who started or recorded a transfer cannot approve it, and a "
        + "past transfer recorded through /transfers/completed waits for a second officer (switched in Administration › Configuration); "
        + "farFutureTransferDays: a transfer dated further ahead needs confirmFarFuture; steepSlopeDegrees: the BCR 2026 slope limit.");
  }
}
