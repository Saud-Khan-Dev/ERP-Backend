using Microsoft.EntityFrameworkCore;

/// What waits for the caller in HRM (the bell and the HRM overview's "Needs attention"): one exact count per queue the
/// caller may act on, gated by the permission that acts on it, plus the caller's own work (forMe) when the account is
/// linked to an employee. Keys are documented in docs/claude/hrm.md › Attention.
public sealed record GetAttentionQuery : IQuery<Result<AttentionResponse>>;

public class AttentionHandlers(IApplicationDbContext context, ICurrentUser currentUser, CurrentEmployee me, HrmSettings settings, IClock clock, DutyGuard duty, EmployeeScope scope, MyTeam team, MediatR.ISender sender) :
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
    if (Can(PermissionCatalog.Hr.Approve))
    {
      var pending = context.HrActions.Where(a => a.Status == HrActionStatus.Pending);
      if (await duty.IsOnAsync(DutyRules.HrAction, cancellationToken))
        pending = pending.Where(a => a.CreatedBy != user && a.SubmittedBy != user);
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

      if (Can(PermissionCatalog.HrSensitive.View))
      {
        var changes = context.ProfileChanges.Where(r => r.Status == ProfileChangeStatus.Pending);
        items.Add(new("profileChanges.toDecide", await changes.CountAsync(cancellationToken), At(await changes.MinAsync(r => (DateTime?)r.SubmittedAt, cancellationToken))));
      }

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

      var unsettled = context.Separations.Where(s => s.SettlementPayrollTransactionId == null && s.FinalSettlementAmount == null);
      items.Add(new("separations.unsettled", await unsettled.CountAsync(cancellationToken), Day(await unsettled.MinAsync(s => (DateOnly?)s.SeparationDate, cancellationToken))));

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
        && (a.EffectiveTo == null || a.EffectiveTo >= today)), cancellationToken)));
    }
    if (Can(PermissionCatalog.Hr.Approve))
    {
      var increments = context.Employees.Where(e => e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus)
        && e.EmploymentStatus != EmploymentStatus.Suspended
        && context.PayRecords.Any(p => p.EmployeeId == e.Id && p.EffectiveTo == null && p.NextIncrementDate != null && p.NextIncrementDate < today));
      items.Add(new("increments.overdue", await increments.CountAsync(cancellationToken)));

      var acknowledged = context.PerformanceReviews.Where(r => r.Status == ReviewStatus.Acknowledged);
      items.Add(new("reviews.toFinalize", await acknowledged.CountAsync(cancellationToken), At(await acknowledged.MinAsync(r => r.UpdatedAt ?? r.CreatedAt, cancellationToken))));
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

    // ---- the caller's own work (never limited by an office scope: the people they appraise may sit anywhere) ----
    using var own = scope.Unrestricted();
    if (await me.TryIdAsync(cancellationToken) is { } self)
    {
      var mine = context.Tasks.Where(t => t.EmployeeId == self && t.Status != EmployeeTaskStatus.Completed && t.Status != EmployeeTaskStatus.Cancelled);
      items.Add(new("my.tasks", await mine.CountAsync(cancellationToken), null, await mine.MinAsync(t => t.DueDate, cancellationToken), ForMe: true));
      items.Add(new("my.tasksOverdue", await mine.CountAsync(t => t.DueDate != null && t.DueDate < today, cancellationToken), null, null, ForMe: true));

      // announcements running today for the person that they have not opened yet
      var announcements = (await sender.Send(new GetMyAnnouncementsQuery(self.Value), cancellationToken)).Value.Announcements;
      items.Add(new("my.announcements", announcements.Count(a => !a.IsRead), null, null, ForMe: true));

      // notesheets waiting with the person, and their own returned to them
      var withMe = context.Notesheets.Where(n => n.HolderId == self && n.Status == NotesheetStatus.InProgress);
      items.Add(new("my.notesheets", await withMe.CountAsync(cancellationToken), At(await withMe.MinAsync(n => (DateTime?)n.UpdatedAt ?? n.CreatedAt, cancellationToken)), null, ForMe: true));
      items.Add(new("my.notesheetsReturned", await context.Notesheets.CountAsync(n => n.WriterId == self && n.Status == NotesheetStatus.Returned, cancellationToken), null, null, ForMe: true));

      // a supervisor: the team's tasks past their due date, and the ones reported done in the last week to look at
      var members = await team.MembersAsync(self, cancellationToken);
      if (members.Count > 0)
      {
        var teamOpen = context.Tasks.Where(t => members.Contains(t.EmployeeId) && t.Status != EmployeeTaskStatus.Completed && t.Status != EmployeeTaskStatus.Cancelled);
        items.Add(new("team.tasksOverdue", await teamOpen.CountAsync(t => t.DueDate != null && t.DueDate < today, cancellationToken), null,
          await teamOpen.Where(t => t.DueDate != null && t.DueDate < today).MinAsync(t => t.DueDate, cancellationToken), ForMe: true));
        var weekAgo = clock.UtcNow.AddDays(-7);
        items.Add(new("team.tasksDone", await context.Tasks.CountAsync(t => members.Contains(t.EmployeeId) && t.Status == EmployeeTaskStatus.Completed
          && t.CompletedAt >= weekAgo, cancellationToken), null, null, ForMe: true));
      }

      // with a countersigning officer the employee reads the review once it is countersigned
      var toAcknowledge = context.PerformanceReviews.Where(r => r.EmployeeId == self
        && (r.Status == ReviewStatus.Countersigned || (r.Status == ReviewStatus.Submitted && r.CountersigningOfficerId == null)));
      items.Add(new("my.reviewsToAcknowledge", await toAcknowledge.CountAsync(cancellationToken), At(await toAcknowledge.MinAsync(r => r.UpdatedAt ?? r.CreatedAt, cancellationToken)), null, ForMe: true));

      var toWrite = context.PerformanceReviews.Where(r => r.EvaluatorId == self && r.Status == ReviewStatus.Draft
        && context.PerformancePeriods.Any(p => p.Id == r.PerformancePeriodId && p.Status == RecordStatus.Active));
      items.Add(new("my.evaluationsToWrite", await toWrite.CountAsync(cancellationToken), At(await toWrite.MinAsync(r => r.CreatedAt, cancellationToken)), null, ForMe: true));

      var toCountersign = context.PerformanceReviews.Where(r => r.CountersigningOfficerId == self && r.Status == ReviewStatus.Submitted);
      items.Add(new("my.countersignings", await toCountersign.CountAsync(cancellationToken), At(await toCountersign.MinAsync(r => r.SubmittedAt ?? r.UpdatedAt ?? r.CreatedAt, cancellationToken)), null, ForMe: true));
    }

    return Result<AttentionResponse>.Success(new(new AttentionDto(DateTimeOffset.UtcNow, items)));
  }
}
