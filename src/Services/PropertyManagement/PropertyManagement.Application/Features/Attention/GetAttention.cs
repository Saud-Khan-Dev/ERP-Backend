using Microsoft.EntityFrameworkCore;

/// The bell's queues for the caller (BuildingBlock AttentionDto). Each key is listed only for a person who may act on
/// it (the permission in brackets) and counts records on active properties; a zero means "checked, nothing waits".
///
///   transfers.pending       [APPROVE] transfers waiting for approval (INITIATED); oldest = when the first was started
///   transfers.unregistered  [APPROVE] transfers approved before approval registered them (legacy APPROVED); "Register now"
///   agreements.ending       [EDIT]    leases, rentals, contracts and allotments in force that end within 60 days; nextDue
///   agreements.overdue      [EDIT]    the same, in force although their end date has passed; oldest = the earliest end
///   hearings.week           [LEGAL.EDIT] open court cases with a hearing in the next 7 days; nextDue
///   legal.next-passed       [LEGAL.EDIT] open court cases whose next hearing passed, open violations whose comply-by date passed
///   appeals.overdue         [LEGAL.APPROVE] open departmental appeals past their s.32 decision date
///   encroachments.open      [LEGAL.EDIT] unresolved encroachments; oldest = the earliest detection
///   plans.waiting           [APPROVE] building plans waiting for a decision (submitted, under review ...)
///   allotments.unconfirmed  [APPROVE] allotments in force whose allottee has not been confirmed as owner
///   fines.unpaid            [EDIT or WAIVE] fines imposed and not yet paid or waived (also sent for recovery), open violation or not
public sealed record GetAttentionQuery : IQuery<Result<AttentionResponse>>;

public class GetAttentionHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : IQueryHandler<GetAttentionQuery, Result<AttentionResponse>>
{
  public const int EndingWithinDays = 60;
  public const int HearingsWithinDays = 7;

  public async Task<Result<AttentionResponse>> Handle(GetAttentionQuery query, CancellationToken cancellationToken)
  {
    var items = new List<AttentionItemDto>();
    var today = PakistanDate.Today;
    var active = context.Properties.AsNoTracking().Where(p => p.IsActive).Select(p => p.Id);
    var canApprove = currentUser.HasPermission(PermissionCatalog.Property.Approve);
    var canEdit = currentUser.HasPermission(PermissionCatalog.Property.Edit);
    var canWorkLegal = currentUser.HasPermission(PermissionCatalog.PropertyLegal.Edit);
    var canDecideLegal = currentUser.HasPermission(PermissionCatalog.PropertyLegal.Approve);
    var canSettleFines = canEdit || currentUser.HasPermission(PermissionCatalog.Property.Waive);

    if (canApprove)
    {
      var pending = context.Transfers.AsNoTracking().Where(t => active.Contains(t.PropertyId) && t.TransferStatus == TransferStatus.Initiated);
      items.Add(new AttentionItemDto("transfers.pending", await pending.CountAsync(cancellationToken),
        Instant(await pending.MinAsync(t => t.CreatedAt, cancellationToken))));

      var unregistered = context.Transfers.AsNoTracking().Where(t => active.Contains(t.PropertyId) && t.TransferStatus == TransferStatus.Approved);
      items.Add(new AttentionItemDto("transfers.unregistered", await unregistered.CountAsync(cancellationToken),
        Day(await unregistered.MinAsync(t => (DateOnly?)(t.ApprovalDate ?? t.TransferDate), cancellationToken))));
    }

    if (canEdit)
    {
      var (endingCount, nextEnd) = await EndingAsync(active, today, today, today.AddDays(EndingWithinDays), cancellationToken);
      items.Add(new AttentionItemDto("agreements.ending", endingCount, NextDue: nextEnd));

      var (overdueCount, firstEnd) = await EndingAsync(active, today, DateOnly.MinValue, today.AddDays(-1), cancellationToken);
      items.Add(new AttentionItemDto("agreements.overdue", overdueCount, Day(firstEnd)));
    }

    var closedCase = await OpenMatters.ClosedCaseStatusIdsAsync(context, cancellationToken);
    var openCases = context.Litigations.AsNoTracking().Where(l => active.Contains(l.PropertyId)).Where(OpenMatters.CaseIsOpen(closedCase));

    if (canWorkLegal)
    {
      var week = today.AddDays(HearingsWithinDays);
      var hearings = openCases.Where(l => l.NextHearingDate != null && l.NextHearingDate >= today && l.NextHearingDate <= week);
      items.Add(new AttentionItemDto("hearings.week", await hearings.CountAsync(cancellationToken),
        NextDue: await hearings.MinAsync(l => l.NextHearingDate, cancellationToken)));

      var passedHearings = openCases.Where(l => l.NextHearingDate != null && l.NextHearingDate < today);
      var passedNotices = context.Violations.AsNoTracking()
        .Where(v => active.Contains(v.PropertyId)).Where(OpenMatters.ViolationIsOpen)
        .Where(v => v.NoticeDeadline != null && v.NoticeDeadline < today);
      var firstPassed = Earliest(await passedHearings.MinAsync(l => l.NextHearingDate, cancellationToken),
        await passedNotices.MinAsync(v => v.NoticeDeadline, cancellationToken));
      items.Add(new AttentionItemDto("legal.next-passed",
        await passedHearings.CountAsync(cancellationToken) + await passedNotices.CountAsync(cancellationToken), Day(firstPassed)));
    }

    if (canDecideLegal)
    {
      var overdueAppeals = context.Appeals.AsNoTracking()
        .Where(a => active.Contains(a.PropertyId)).Where(OpenMatters.AppealIsOpen).Where(a => a.DecisionDueDate < today);
      items.Add(new AttentionItemDto("appeals.overdue", await overdueAppeals.CountAsync(cancellationToken),
        Day(await overdueAppeals.MinAsync(a => (DateOnly?)a.DecisionDueDate, cancellationToken))));
    }

    if (canWorkLegal)
    {
      var encroachments = context.Encroachments.AsNoTracking().Where(e => active.Contains(e.PropertyId)).Where(OpenMatters.EncroachmentIsOpen);
      items.Add(new AttentionItemDto("encroachments.open", await encroachments.CountAsync(cancellationToken),
        Day(await encroachments.MinAsync(e => (DateOnly?)e.DetectionDate, cancellationToken))));
    }

    if (canApprove)
    {
      var decided = await InForce.StatusIdsAsync<BuildingPlanStatus>(context, BuildingPlan.ClosedStatusCodes, cancellationToken);
      var plans = context.BuildingPlans.AsNoTracking().Where(b => active.Contains(b.PropertyId) && !decided.Contains(b.BuildingPlanStatusId));
      items.Add(new AttentionItemDto("plans.waiting", await plans.CountAsync(cancellationToken),
        Instant(await plans.MinAsync(b => b.CreatedAt, cancellationToken))));

      var current = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);
      var confirmed = context.Ownerships.AsNoTracking().Where(o => o.AcquiredViaAllotmentId != null);
      var unconfirmed = context.Allotments.AsNoTracking()
        .Where(a => active.Contains(a.PropertyId) && a.IsActive && current.Contains(a.AllotmentStatusId) && (a.EffectiveDate ?? a.AllotmentDate) <= today)
        .Where(a => !confirmed.Any(o => o.AcquiredViaAllotmentId == a.Id && (a.RestorationDate == null || o.EffectiveFrom >= a.RestorationDate)));
      items.Add(new AttentionItemDto("allotments.unconfirmed", await unconfirmed.CountAsync(cancellationToken),
        Day(await unconfirmed.MinAsync(a => (DateOnly?)(a.EffectiveDate ?? a.AllotmentDate), cancellationToken))));
    }

    if (canSettleFines)
    {
      var fines = context.Violations.AsNoTracking()
        .Where(v => active.Contains(v.PropertyId) && (v.FineStatus == FineStatus.Imposed || v.FineStatus == FineStatus.RecoveryAsArrears));
      items.Add(new AttentionItemDto("fines.unpaid", await fines.CountAsync(cancellationToken),
        Day(await fines.MinAsync(v => v.FineImposedOn ?? (DateOnly?)v.ViolationDate, cancellationToken))));
    }

    return Result<AttentionResponse>.Success(new AttentionResponse(new AttentionDto(DateTimeOffset.UtcNow, items)));
  }

  /// Leases, rentals and contracts ACTIVE and started, and current allotments in effect, whose end (termination else
  /// agreed end; an allotment's expiry) falls in [from, to]: how many, and the earliest such end.
  private async Task<(int Count, DateOnly? First)> EndingAsync(IQueryable<PropertyId> active, DateOnly today, DateOnly from, DateOnly to, CancellationToken cancellationToken)
  {
    var lease = await InForce.StatusIdsAsync<LeaseStatus>(context, InForce.LeaseStatuses, cancellationToken);
    var rental = await InForce.StatusIdsAsync<RentalStatus>(context, InForce.RentalStatuses, cancellationToken);
    var contract = await InForce.StatusIdsAsync<ContractStatus>(context, InForce.ContractStatuses, cancellationToken);
    var allotment = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);

    var leases = context.Leases.AsNoTracking()
      .Where(x => active.Contains(x.PropertyId) && lease.Contains(x.LeaseStatusId) && x.LeaseStartDate <= today)
      .Select(x => (DateOnly?)(x.TerminationDate ?? x.LeaseEndDate)).Where(d => d >= from && d <= to);
    var rentals = context.Rentals.AsNoTracking()
      .Where(x => active.Contains(x.PropertyId) && rental.Contains(x.RentalStatusId) && x.RentalStartDate <= today)
      .Select(x => x.TerminationDate ?? x.RentalEndDate).Where(d => d != null && d >= from && d <= to);
    var contracts = context.Outsourcings.AsNoTracking()
      .Where(x => active.Contains(x.PropertyId) && contract.Contains(x.ContractStatusId) && x.ContractStartDate <= today)
      .Select(x => x.TerminationDate ?? x.ContractEndDate).Where(d => d != null && d >= from && d <= to);
    var allotments = context.Allotments.AsNoTracking()
      .Where(x => active.Contains(x.PropertyId) && x.IsActive && allotment.Contains(x.AllotmentStatusId) && (x.EffectiveDate ?? x.AllotmentDate) <= today)
      .Select(x => x.ExpiryDate).Where(d => d != null && d >= from && d <= to);

    var count = await leases.CountAsync(cancellationToken) + await rentals.CountAsync(cancellationToken)
      + await contracts.CountAsync(cancellationToken) + await allotments.CountAsync(cancellationToken);
    var first = Earliest(await leases.MinAsync(cancellationToken), await rentals.MinAsync(cancellationToken),
      await contracts.MinAsync(cancellationToken), await allotments.MinAsync(cancellationToken));
    return (count, first);
  }

  private static DateOnly? Earliest(params DateOnly?[] dates) => dates.Where(d => d is not null).Min();

  private static DateTimeOffset? Day(DateOnly? day) => day is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;

  private static DateTimeOffset? Instant(DateTime? at) => at is { } t ? new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)) : null;
}
