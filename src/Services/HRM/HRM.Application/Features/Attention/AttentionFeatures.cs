using Microsoft.EntityFrameworkCore;

/// What waits for the caller in HRM (the bell and the HRM overview's "Needs attention"): one exact count per queue the
/// caller may act on, gated by the permission that acts on it, plus the caller's own work (forMe) when the account is
/// linked to an employee. Keys are documented in docs/claude/hrm.md › Attention.
public sealed record GetAttentionQuery : IQuery<Result<AttentionResponse>>;

public class AttentionHandlers(IApplicationDbContext context, ICurrentUser currentUser, CurrentEmployee me, HrmSettings settings, IClock clock, DutyGuard duty, EmployeeScope scope) :
  IQueryHandler<GetAttentionQuery, Result<AttentionResponse>>
{
  public async Task<Result<AttentionResponse>> Handle(GetAttentionQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var items = new List<AttentionItemDto>();
    var user = currentUser.UserId;
    bool Can(string code) => currentUser.HasPermission(code);
    static DateTimeOffset? At(DateTime? value) => value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
    static DateTimeOffset? Day(DateOnly? value) => value is null ? null : new DateTimeOffset(value.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    var inService = EmploymentStatuses.InService;
    var dated = Enum.GetValues<HrActionType>().Where(HrActionRequest.TakesEffectOnItsDate).ToArray();

    // ---- HR actions ----
    // only the Super Administrator decides HR actions, their own too
    if (currentUser.IsSuperAdmin)
    {
      var pending = context.HrActions.Where(a => a.Status == HrActionStatus.Pending);
      items.Add(new("hrActions.toApprove", await pending.CountAsync(cancellationToken),
        At(await pending.MinAsync(a => (DateTime?)(a.SubmittedAt ?? a.CreatedAt), cancellationToken))));
    }
    if (Can(PermissionCatalog.Hr.Edit))
    {
      var due = context.HrActions.Where(a => a.Status == HrActionStatus.Approved && (!dated.Contains(a.ActionType) || a.EffectiveDate <= today));
      items.Add(new("hrActions.toApply", await due.CountAsync(cancellationToken), At(await due.MinAsync(a => a.ApprovedAt, cancellationToken))));

      var later = context.HrActions.Where(a => a.Status == HrActionStatus.Approved && dated.Contains(a.ActionType) && a.EffectiveDate > today);
      items.Add(new("hrActions.takeEffectLater", await later.CountAsync(cancellationToken), null, await later.MinAsync(a => (DateOnly?)a.EffectiveDate, cancellationToken)));

      var drafts = context.HrActions.Where(a => a.Status == HrActionStatus.Draft && a.CreatedBy == user);
      items.Add(new("hrActions.myDrafts", await drafts.CountAsync(cancellationToken), At(await drafts.MinAsync(a => a.CreatedAt, cancellationToken)), null, ForMe: true));
    }

    // ---- requests, tasks, documents ----
    if (Can(PermissionCatalog.Hr.Approve))
    {
      var requests = context.Requests.Where(r => r.Status == EmployeeRequestStatus.Pending);
      items.Add(new("requests.toDecide", await requests.CountAsync(cancellationToken), At(await requests.MinAsync(r => (DateTime?)r.SubmittedAt, cancellationToken))));

      var toVerify = context.Documents.Where(d => (d.VerificationStatus == VerificationStatus.Unverified || d.VerificationStatus == VerificationStatus.Pending)
        && context.Employees.Any(e => e.Id == d.EmployeeId && e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus)));
      items.Add(new("documents.toVerify", await toVerify.CountAsync(cancellationToken), At(await toVerify.MinAsync(d => d.CreatedAt, cancellationToken))));
    }
    if (Can(PermissionCatalog.Hr.Edit))
    {
      var overdue = context.Tasks.Where(t => t.DueDate != null && t.DueDate < today
        && t.Status != EmployeeTaskStatus.Completed && t.Status != EmployeeTaskStatus.Cancelled);
      items.Add(new("tasks.overdue", await overdue.CountAsync(cancellationToken), Day(await overdue.MinAsync(t => t.DueDate, cancellationToken))));

      var until = today.AddDays(30);
      var expiring = context.Documents.Where(d => d.ExpiryDate != null && d.ExpiryDate <= until && d.VerificationStatus != VerificationStatus.Rejected
        && !context.Documents.Any(n => n.EmployeeId == d.EmployeeId && n.DocumentTypeId == d.DocumentTypeId && n.Id != d.Id
          && n.VerificationStatus != VerificationStatus.Rejected && n.ExpiryDate != null && n.ExpiryDate > d.ExpiryDate)
        && context.Employees.Any(e => e.Id == d.EmployeeId && e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus)));
      items.Add(new("documents.expiring", await expiring.CountAsync(cancellationToken), Day(await expiring.MinAsync(d => d.ExpiryDate, cancellationToken))));

      // the office records the separation order; one without it is not finished
      var noOrder = context.Separations.Where(s => s.OrderNumber == null || s.OrderNumber == "");
      items.Add(new("separations.noOrder", await noOrder.CountAsync(cancellationToken), Day(await noOrder.MinAsync(s => (DateOnly?)s.SeparationDate, cancellationToken))));

      // the people HR has to look at: past the retirement age but not separated, retiring within six months,
      // contracts and deputations ending within 60 days, no date of birth, no post
      var people = context.Employees.Where(e => e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus));
      var age = settings.RetirementAge;
      var pastBirth = today.AddYears(-age);
      var soonBirth = today.AddYears(-age).AddMonths(6);
      var past = people.Where(e => e.DateOfBirth != null && e.DateOfBirth <= pastBirth);
      items.Add(new("retirements.past", await past.CountAsync(cancellationToken)));
      // the same people the register's "retiring within 6 months" filter lists (those already past the age included)
      var soon = people.Where(e => e.DateOfBirth != null && e.DateOfBirth <= soonBirth);
      var nextBirth = await people.Where(e => e.DateOfBirth != null && e.DateOfBirth > pastBirth && e.DateOfBirth <= soonBirth).MinAsync(e => e.DateOfBirth, cancellationToken);
      items.Add(new("retirements.due", await soon.CountAsync(cancellationToken), null, nextBirth?.AddYears(age)));
      var termUntil = today.AddDays(60);
      var terms = people.Where(e => e.TermTo != null && e.TermTo <= termUntil);
      items.Add(new("contracts.ending", await terms.CountAsync(cancellationToken), null, await terms.MinAsync(e => e.TermTo, cancellationToken)));
      items.Add(new("employees.noDateOfBirth", await people.CountAsync(e => e.DateOfBirth == null, cancellationToken)));
      items.Add(new("employees.unplaced", await people.CountAsync(e => !context.PositionAssignments.Any(a => a.EmployeeId == e.Id
        && a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active && a.EffectiveFrom <= today
        && (a.EffectiveTo == null || a.EffectiveTo >= today))
        && !context.EngagementOrders.Any(o => o.EmployeeId == e.Id && o.Status == RecordStatus.Active && o.StartDate <= today && o.EndDate >= today), cancellationToken)));
    }
    if (Can(PermissionCatalog.Hr.Approve))
    {
      var increments = context.Employees.Where(e => e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus)
        && e.EmploymentStatus != EmploymentStatus.Suspended
        && context.PayRecords.Any(p => p.EmployeeId == e.Id && p.EffectiveTo == null && p.NextIncrementDate != null && p.NextIncrementDate < today));
      items.Add(new("increments.overdue", await increments.CountAsync(cancellationToken)));
    }

    // ---- leave ----
    if (Can(PermissionCatalog.Attendance.Approve))
    {
      var leave = context.LeaveApplications.Where(a => a.Status == LeaveStatus.Pending);
      items.Add(new("leave.toDecide", await leave.CountAsync(cancellationToken), At(await leave.MinAsync(a => a.CreatedAt, cancellationToken))));
    }

    // ---- payroll ----
    async Task Runs(string key, PayrollRunStatus status)
    {
      var runs = context.PayrollRuns.Where(r => r.Status == status);
      items.Add(new(key, await runs.CountAsync(cancellationToken), At(await runs.MinAsync(r => r.UpdatedAt ?? r.CreatedAt, cancellationToken))));
    }
    if (Can(PermissionCatalog.Payroll.Edit))
      await Runs("payroll.toReview", PayrollRunStatus.Calculated);
    if (Can(PermissionCatalog.Payroll.Approve))
      await Runs("payroll.toApprove", PayrollRunStatus.Reviewed);
    if (Can(PermissionCatalog.Payroll.Post))
    {
      await Runs("payroll.toFinalize", PayrollRunStatus.Approved);
      await Runs("payroll.toPay", PayrollRunStatus.Finalized);
    }

    // ---- the caller's own work (never limited by an office scope) ----
    using var own = scope.Unrestricted();
    if (await me.TryIdAsync(cancellationToken) is { } self)
    {
      var mine = context.Tasks.Where(t => t.EmployeeId == self && t.Status != EmployeeTaskStatus.Completed && t.Status != EmployeeTaskStatus.Cancelled);
      items.Add(new("my.tasks", await mine.CountAsync(cancellationToken), null, await mine.MinAsync(t => t.DueDate, cancellationToken), ForMe: true));
      items.Add(new("my.tasksOverdue", await mine.CountAsync(t => t.DueDate != null && t.DueDate < today, cancellationToken), null, null, ForMe: true));
      // the person's own separation, for a month after it is recorded
      var since = today.AddDays(-30);
      var separated = context.Separations.Where(s => s.EmployeeId == self && s.SeparationDate >= since);
      items.Add(new("my.separation", await separated.CountAsync(cancellationToken), Day(await separated.MinAsync(s => (DateOnly?)s.SeparationDate, cancellationToken)), null, ForMe: true));

      // what HR decided on what the person sent, for two weeks after (the bell shows a new one as unseen)
      var recent = DateTime.UtcNow.AddDays(-14);
      var decided = context.Requests.Where(r => r.EmployeeId == self && r.ReviewedAt >= recent
        && (r.Status == EmployeeRequestStatus.Approved || r.Status == EmployeeRequestStatus.Rejected));
      items.Add(new("my.requestsDecided", await decided.CountAsync(cancellationToken), null, null, ForMe: true));
      // leave approved, turned down or cancelled by someone else (not the person withdrawing their own)
      var leaveDecided = context.LeaveApplications.Where(a => a.EmployeeId == self && a.DecidedAt >= recent && a.DecidedBy != user
        && a.Status != LeaveStatus.Pending);
      items.Add(new("my.leaveDecided", await leaveDecided.CountAsync(cancellationToken), null, null, ForMe: true));
      // papers the person sent, checked by HR
      var checkedSince = today.AddDays(-14);
      var papers = context.Documents.Where(d => d.EmployeeId == self && d.CreatedBy == user && d.VerificationDate >= checkedSince
        && (d.VerificationStatus == VerificationStatus.Verified || d.VerificationStatus == VerificationStatus.Rejected));
      items.Add(new("my.documentsChecked", await papers.CountAsync(cancellationToken), null, null, ForMe: true));
      var slips =
        from s in context.Payslips
        join t in context.PayrollTransactions on s.PayrollTransactionId equals t.Id
        join r in context.PayrollRuns on t.PayrollRunId equals r.Id
        where t.EmployeeId == self && r.Status != PayrollRunStatus.Reversed && s.GeneratedAt >= recent
        select s;
      items.Add(new("my.payslips", await slips.CountAsync(cancellationToken), null, null, ForMe: true));
      // a contingent person's order running out with no next order after it
      var soon = today.AddDays(15);
      var ending = context.EngagementOrders.Where(o => o.EmployeeId == self && o.Status == RecordStatus.Active
        && o.StartDate <= today && o.EndDate >= today && o.EndDate <= soon
        && !context.EngagementOrders.Any(n => n.EmployeeId == self && n.Status == RecordStatus.Active && n.StartDate > o.EndDate));
      items.Add(new("my.orderEnding", await ending.CountAsync(cancellationToken), null, await ending.MinAsync(o => (DateOnly?)o.EndDate, cancellationToken), ForMe: true));
    }

    // tasks the caller gave that the person reported done, for a week
    if (user is { } giver)
    {
      var week = DateTime.UtcNow.AddDays(-7);
      var done = context.Tasks.Where(t => t.AssignedBy == giver && t.Status == EmployeeTaskStatus.Completed && t.CompletedAt >= week);
      items.Add(new("tasks.doneForYou", await done.CountAsync(cancellationToken), null, null, ForMe: true));
    }

    return Result<AttentionResponse>.Success(new(new AttentionDto(DateTimeOffset.UtcNow, items)));
  }
}
