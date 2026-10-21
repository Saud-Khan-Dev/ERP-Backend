using Microsoft.EntityFrameworkCore;

/// An appointment to a post: the event (Appointment, Deputation In ...), the post and the pay to start on.
public sealed record AppointmentSpec(
  PostId PostId,
  DateOnly EffectiveFrom,
  string EventName,
  string? OrderNumber,
  RecruitmentMethodId? RecruitmentMethodId,
  int? StageNumber,
  decimal? BasicPay,
  EmployeeDocumentId? SupportingDocumentId,
  string? ExternalReferenceOrg,
  string? Reason,
  string? Remarks,
  Guid? ApprovedBy);

/// A move from the current post to another (transfer, promotion, demotion).
public sealed record MoveSpec(
  PostId NewPostId,
  DateOnly EffectiveFrom,
  HrActionType Kind,
  string? OrderNumber,
  EmployeeDocumentId? SupportingDocumentId,
  string? Reason,
  string? Remarks,
  Guid? ApprovedBy);

/// Leaving service. FromActionId: the HR action being applied (it is not closed with the other open actions).
public sealed record SeparationSpec(
  SeparationType Type,
  DateOnly SeparationDate,
  string? OrderNumber,
  string? Reason,
  int? NoticePeriodDays,
  EmploymentStatus? EmploymentStatus,
  ServiceEventTypeId? EventTypeId,
  EmployeeDocumentId? SupportingDocumentId,
  Guid? ApprovedBy,
  HrActionRequestId? FromActionId = null);

/// One thing a separation leaves for a person to do (or done). Key: account, assets, loans, gpFund, postsHeaded,
/// signingPosts, settlement, leave, appraisals, evaluations.
public sealed record SeparationChecklistItem(string Key, string Title, string Detail, bool Done, Guid? RefId = null);

/// What a separation closed by itself, what still needs a person, and the posted payroll it reaches.
public sealed record SeparationChecklistDto(Guid EmployeeId, IReadOnlyList<string> Closed, IReadOnlyList<SeparationChecklistItem> Items, IReadOnlyList<PayrollImpactDto> PayrollImpact);

/// Writes the service record: every change of post or service status is one service-history row plus the matching
/// assignment, pay and status changes, so the history always explains the current state. Nothing here saves; the
/// caller saves once, so each use case is one transaction.
public class ServiceRecordWriter(IApplicationDbContext context, ServiceRecordReader reader, PayService pay, HrmSettings settings,
  PayrollImpactFinder payroll, ICurrentUser currentUser, IClock clock)
{
  public async Task<EmployeeServiceHistory> AppointAsync(Employee employee, AppointmentSpec spec, CancellationToken cancellationToken)
  {
    employee.EnsureInService();
    var date = spec.EffectiveFrom;
    employee.EnsureAppointable(date, settings.RetirementAge);
    var post = await context.LoadPostAsync(spec.PostId.Value, cancellationToken);
    var version = post.VersionOn(date)
      ?? throw new DomainException($"Post {post.PostCode} does not exist on {date:yyyy-MM-dd}.");

    var eventType = await context.LoadEventTypeAsync(spec.EventName, cancellationToken);
    var history = EmployeeServiceHistory.Record(ServiceHistoryId.New(), employee.Id, eventType, date,
      new ServiceChange(null, post.Id, null, version.GradeId, null, version.OrgUnitId),
      spec.RecruitmentMethodId, spec.ExternalReferenceOrg, spec.OrderNumber, spec.SupportingDocumentId, spec.Reason, spec.Remarks, spec.ApprovedBy);
    context.ServiceHistory.Add(history);

    var assignment = PositionAssignment.Create(PositionAssignmentId.New(), post, employee, AssignmentType.Regular, date, null, history.Id,
      spec.OrderNumber, spec.SupportingDocumentId, spec.Remarks,
      await reader.LiveAssignmentsOfPostAsync(post.Id, cancellationToken),
      await reader.LiveAssignmentsOfEmployeeAsync(employee.Id, cancellationToken));
    context.PositionAssignments.Add(assignment);

    // the pay starts on the post's scale: the given stage (stage 0, the minimum, by default)
    var open = await pay.OpenRecordAsync(employee.Id, cancellationToken);
    if (open is null || spec.StageNumber.HasValue || spec.BasicPay.HasValue)
    {
      var scale = await pay.ScaleInForceAsync(version.GradeId, date, cancellationToken);
      var stage = scale.Stage(spec.StageNumber ?? 0)
        ?? throw new DomainException($"The scale in force has no stage {spec.StageNumber}.");
      await pay.StartAsync(employee.Id, stage, spec.BasicPay, date, PayChangeReasons.Appointment, spec.OrderNumber, null, null, cancellationToken);
    }

    employee.ChangeEmploymentStatus(EmploymentStatus.Active);
    return history;
  }

  public async Task<EmployeeServiceHistory> MoveAsync(Employee employee, CurrentPost current, MoveSpec spec, CancellationToken cancellationToken)
  {
    employee.EnsureInService();
    var date = spec.EffectiveFrom;
    var post = await context.LoadPostAsync(spec.NewPostId.Value, cancellationToken);
    var version = post.VersionOn(date)
      ?? throw new DomainException($"Post {post.PostCode} does not exist on {date:yyyy-MM-dd}.");

    if (post.Id == current.Post.Id)
      throw new DomainException("The new post is the employee's current post.");

    var grades = await context.PayScaleGrades.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.BpsNumber, cancellationToken);
    var oldBps = grades[current.Version.GradeId];
    var newBps = grades[version.GradeId];

    switch (spec.Kind)
    {
      case HrActionType.Promotion when newBps <= oldBps:
        throw new DomainException($"A promotion needs a higher grade: post {post.PostCode} is BPS-{newBps}, the current post BPS-{oldBps}.");
      case HrActionType.Demotion when newBps >= oldBps:
        throw new DomainException($"A demotion needs a lower grade: post {post.PostCode} is BPS-{newBps}, the current post BPS-{oldBps}.");
      case HrActionType.Transfer when newBps != oldBps:
        throw new DomainException($"Post {post.PostCode} is BPS-{newBps} and the current post BPS-{oldBps}; record a promotion or demotion instead of a transfer.");
    }

    if (date <= current.Assignment.EffectiveFrom)
      throw new DomainException($"The current post was taken on {current.Assignment.EffectiveFrom:yyyy-MM-dd}; the move must be dated after that.");

    var eventType = await context.LoadEventTypeAsync(ServiceEventNames.For(spec.Kind)!, cancellationToken);
    var history = EmployeeServiceHistory.Record(ServiceHistoryId.New(), employee.Id, eventType, date,
      new ServiceChange(current.Post.Id, post.Id, current.Version.GradeId, version.GradeId, current.Version.OrgUnitId, version.OrgUnitId),
      null, null, spec.OrderNumber, spec.SupportingDocumentId, spec.Reason, spec.Remarks, spec.ApprovedBy);
    context.ServiceHistory.Add(history);

    // relieved of the old post the day before, then the new one (the seat check sees the old one ended)
    current.Assignment.End(date.AddDays(-1));
    var assignment = PositionAssignment.Create(PositionAssignmentId.New(), post, employee, AssignmentType.Regular, date, null, history.Id,
      spec.OrderNumber, spec.SupportingDocumentId, spec.Remarks,
      await reader.LiveAssignmentsOfPostAsync(post.Id, cancellationToken),
      await reader.LiveAssignmentsOfEmployeeAsync(employee.Id, cancellationToken));
    context.PositionAssignments.Add(assignment);

    // pay fixation in the new grade: the stage next above the pay drawn (promotion) or next below it (demotion)
    if (spec.Kind is HrActionType.Promotion or HrActionType.Demotion)
    {
      var drawn = await pay.RecordOnAsync(employee.Id, date.AddDays(-1), cancellationToken)
        ?? throw new DomainException($"{employee.DisplayName} has no pay record before {date:yyyy-MM-dd} to fix the new pay from.");
      var scale = await pay.ScaleInForceAsync(version.GradeId, date, cancellationToken);
      var stage = spec.Kind == HrActionType.Promotion ? scale.StageAtOrAbove(drawn.BasicPay) : scale.StageAtOrBelow(drawn.BasicPay);
      await pay.StartAsync(employee.Id, stage, null, date,
        spec.Kind == HrActionType.Promotion ? PayChangeReasons.Promotion : PayChangeReasons.Demotion, spec.OrderNumber, null, null, cancellationToken);
    }

    return history;
  }

  /// Puts back on a post someone who held none (joining after deputation out, reinstatement after dismissal): the
  /// regular assignment from the date and the pay reopened on the post's scale - at the stage last drawn when it fits,
  /// so the person comes back on the pay they left on.
  public async Task<(PositionAssignment Assignment, EmployeePayRecord? Pay)> PlaceAsync(Employee employee, PostId postId, DateOnly date,
      ServiceHistoryId historyId, string? orderNumber, EmployeeDocumentId? orderDocumentId, string payReason, CancellationToken cancellationToken)
  {
    var post = await context.LoadPostAsync(postId.Value, cancellationToken);
    var version = post.VersionOn(date)
      ?? throw new DomainException($"Post {post.PostCode} does not exist on {date:yyyy-MM-dd}.");

    var assignment = PositionAssignment.Create(PositionAssignmentId.New(), post, employee, AssignmentType.Regular, date, null, historyId,
      orderNumber, orderDocumentId, null,
      await reader.LiveAssignmentsOfPostAsync(post.Id, cancellationToken),
      await reader.LiveAssignmentsOfEmployeeAsync(employee.Id, cancellationToken));
    context.PositionAssignments.Add(assignment);

    var open = await pay.OpenRecordAsync(employee.Id, cancellationToken);
    if (open is not null && open.EffectiveFrom < date)
      return (assignment, null);

    var last = await context.PayRecords.AsNoTracking().Where(p => p.EmployeeId == employee.Id && p.EffectiveFrom < date)
      .OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);
    var scale = await pay.ScaleInForceAsync(version.GradeId, date, cancellationToken);
    var stageNumber = last is null ? 0
      : await context.PayScaleStages.AsNoTracking().Where(s => s.Id == last.PayScaleStageId).Select(s => s.StageNumber).FirstAsync(cancellationToken);
    var stage = scale.Stage(stageNumber) ?? scale.Stages.OrderByDescending(s => s.StageNumber).First();
    var record = await pay.StartAsync(employee.Id, stage, null, date, payReason, orderNumber, last?.LastIncrementDate, null, cancellationToken);
    return (assignment, record);
  }

  /// A service event that changes no post (joining, LWOP, suspension, regularization ...), or one that only leaves a
  /// post (deputation out).
  public EmployeeServiceHistory RecordEvent(
      Employee employee,
      ServiceEventType eventType,
      DateOnly date,
      ServiceChange change,
      RecruitmentMethodId? recruitmentMethodId,
      string? externalReferenceOrg,
      string? orderNumber,
      EmployeeDocumentId? supportingDocumentId,
      string? reason,
      string? remarks,
      Guid? approvedBy)
  {
    var history = EmployeeServiceHistory.Record(ServiceHistoryId.New(), employee.Id, eventType, date, change, recruitmentMethodId,
      externalReferenceOrg, orderNumber, supportingDocumentId, reason, remarks, approvedBy);
    context.ServiceHistory.Add(history);
    return history;
  }

  /// The post an event happens on, unchanged (old = new).
  public static ServiceChange Unchanged(CurrentPost? current) => current is null
    ? ServiceChange.None
    : new ServiceChange(current.Post.Id, current.Post.Id, current.Version.GradeId, current.Version.GradeId, current.Version.OrgUnitId, current.Version.OrgUnitId);

  /// Ends the employee's service on the separation date (the last day of service): posts relieved, pay closed, shift
  /// ended, service status set, a service-history row and the separation record written. What waits on the person is
  /// closed (open HR actions, pending and future leave, open tasks, pending requests, empty draft appraisals) and the
  /// checklist of what still needs someone is returned.
  public async Task<(EmployeeServiceHistory History, EmployeeSeparation Separation, SeparationChecklistDto Checklist)> SeparateAsync(
      Employee employee, SeparationSpec spec, CancellationToken cancellationToken)
  {
    if (spec.Type == SeparationType.Death)
      employee.EnsureProfileActive();
    else
      employee.EnsureInService();
    if (employee.EmploymentStatus == EmploymentStatus.Deceased)
      throw new DomainException($"{employee.DisplayName} is already recorded as deceased.");
    if (spec.Type == SeparationType.Other)
      throw new DomainException("Choose the kind of separation (retirement, resignation, dismissal ...).");

    var date = spec.SeparationDate;
    if (await context.Separations.AnyAsync(s => s.EmployeeId == employee.Id, cancellationToken))
      throw new DomainException($"{employee.DisplayName}'s separation is already recorded.");

    var current = await reader.RegularPostOnAsync(employee.Id, date, cancellationToken);
    var eventType = spec.EventTypeId is { } eventTypeId
      ? await context.LoadEventTypeAsync(eventTypeId.Value, cancellationToken)
      : await context.LoadEventTypeAsync(ServiceEventNames.For(spec.Type)
          ?? throw new DomainException("Choose the service event type to record this separation under."), cancellationToken);

    var history = EmployeeServiceHistory.Record(ServiceHistoryId.New(), employee.Id, eventType, date,
      current is null ? ServiceChange.None : new ServiceChange(current.Post.Id, null, current.Version.GradeId, null, current.Version.OrgUnitId, null),
      null, null, spec.OrderNumber, spec.SupportingDocumentId, spec.Reason, null, spec.ApprovedBy);
    context.ServiceHistory.Add(history);

    // the posts held on the last day, for the checklist (heads of units, signing officers)
    var heldPosts = new List<PostId>();
    foreach (var assignment in await reader.LiveAssignmentsOfEmployeeAsync(employee.Id, cancellationToken))
    {
      if (assignment.EffectiveFrom > date)
        assignment.Cancel("Service ended before this assignment started.");
      else if (assignment.EffectiveTo is null || assignment.EffectiveTo > date)
      {
        heldPosts.Add(assignment.PostId);
        assignment.End(date);
      }
    }

    await pay.CloseAsync(employee.Id, date, cancellationToken);

    foreach (var shift in await context.EmployeeShifts.Where(s => s.EmployeeId == employee.Id && (s.EffectiveTo == null || s.EffectiveTo > date)).ToListAsync(cancellationToken))
    {
      if (shift.EffectiveFrom <= date)
        shift.EndOn(date);
    }

    employee.ChangeEmploymentStatus(spec.EmploymentStatus ?? ServiceEventNames.StatusAfter(spec.Type));

    var owed = await context.Loans.Where(l => l.EmployeeId == employee.Id && l.Status == LoanStatus.Active).SumAsync(l => l.RemainingBalance, cancellationToken);
    var separation = EmployeeSeparation.Record(SeparationId.New(), employee.Id, spec.Type, date, history.Id, spec.OrderNumber, spec.Reason,
      spec.NoticePeriodDays, owed > 0 ? owed : null);
    context.Separations.Add(separation);

    var closed = await CloseOpenItemsAsync(employee, spec, cancellationToken);
    var checklist = await ChecklistAsync(employee, separation, heldPosts, cancellationToken);
    return (history, separation, checklist with { Closed = closed });
  }

  /// Closes what waits on someone who has left: it can no longer be acted on.
  private async Task<IReadOnlyList<string>> CloseOpenItemsAsync(Employee employee, SeparationSpec spec, CancellationToken cancellationToken)
  {
    var date = spec.SeparationDate;
    var now = clock.UtcNow;
    var remark = $"Closed: {employee.DisplayName} left service on {date:dd MMM yyyy} ({EnumText.Words(spec.Type)}).";
    var closed = new List<string>();

    var actions = await context.HrActions.Where(a => a.EmployeeId == employee.Id && a.Id != spec.FromActionId
      && (a.Status == HrActionStatus.Draft || a.Status == HrActionStatus.Pending || a.Status == HrActionStatus.Approved)).ToListAsync(cancellationToken);
    foreach (var action in actions)
      action.Close(remark, now);
    if (actions.Count > 0)
      closed.Add($"{actions.Count} open HR action(s) cancelled.");

    var leave = await context.LeaveApplications.Where(a => a.EmployeeId == employee.Id
      && (a.Status == LeaveStatus.Pending || (a.Status == LeaveStatus.Approved && a.StartDate > date))).ToListAsync(cancellationToken);
    foreach (var application in leave)
    {
      var usage = await context.LeaveLedger.Where(l => l.LeaveApplicationId == application.Id).ToListAsync(cancellationToken);
      foreach (var entry in application.Cancel(usage, currentUser.UserId, now, remark))
        context.LeaveLedger.Add(entry);
    }
    if (leave.Count > 0)
      closed.Add($"{leave.Count} pending or later leave application(s) cancelled.");

    var tasks = await context.Tasks.Where(t => t.EmployeeId == employee.Id && t.Status != EmployeeTaskStatus.Completed
      && t.Status != EmployeeTaskStatus.Cancelled).ToListAsync(cancellationToken);
    foreach (var task in tasks)
      task.Cancel();
    if (tasks.Count > 0)
      closed.Add($"{tasks.Count} open task(s) cancelled.");

    var requests = await context.Requests.Where(r => r.EmployeeId == employee.Id && r.Status == EmployeeRequestStatus.Pending).ToListAsync(cancellationToken);
    foreach (var request in requests)
      request.Withdraw(currentUser.UserId, now, remark);
    if (requests.Count > 0)
      closed.Add($"{requests.Count} pending request(s) withdrawn.");

    // draft appraisals of the leaver with nothing written yet are removed; those with content stay (see the checklist)
    var drafts = await context.PerformanceReviews.Include(r => r.Goals).Include(r => r.Kpis).Include(r => r.Competencies)
      .Where(r => r.EmployeeId == employee.Id && r.Status == ReviewStatus.Draft).ToListAsync(cancellationToken);
    var empty = drafts.Where(r => r.Goals.Count == 0 && r.Kpis.Count == 0 && r.Competencies.Count == 0).ToList();
    foreach (var review in empty)
      context.PerformanceReviews.Remove(review);
    if (empty.Count > 0)
      closed.Add($"{empty.Count} empty draft appraisal(s) removed.");

    return closed;
  }

  /// What still needs a person after a separation, as it stands now (the record shows it until it is all done).
  public async Task<SeparationChecklistDto> ChecklistAsync(Employee employee, EmployeeSeparation separation, IReadOnlyCollection<PostId>? heldPosts, CancellationToken cancellationToken)
  {
    var date = separation.SeparationDate;
    var items = new List<SeparationChecklistItem>();

    items.Add(employee.UserId is { } userId
      ? new("account", "Deactivate the sign-in account", "The account linked to this employee can still sign in. Deactivate it in Administration › Users.", false, userId)
      : new("account", "Sign-in account", "No sign-in account is linked to this employee.", true));

    items.Add(new("assets", "Assets in custody", "Collect every asset in the employee's custody before the final settlement (Assets register).", false));

    var loans = await context.Loans.AsNoTracking().Where(l => l.EmployeeId == employee.Id && l.Status == LoanStatus.Active).ToListAsync(cancellationToken);
    var owed = loans.Sum(l => l.RemainingBalance);
    items.Add(loans.Count > 0
      ? new("loans", "Recover the loans", $"{loans.Count} loan(s) still owe Rs {owed:#,##0.##}: recover them from the final settlement or record the repayment.", false)
      : new("loans", "Loans", "No loan is outstanding.", true));

    var gpf = await context.GpFundAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.EmployeeId == employee.Id, cancellationToken);
    if (gpf is not null)
    {
      var balance = await context.GpFundBalances.AsNoTracking().Where(b => b.GpFundAccountId == gpf.Id.Value).Select(b => b.Balance).FirstOrDefaultAsync(cancellationToken);
      items.Add(gpf.IsOpen || balance > 0
        ? new("gpFund", "Pay the GP Fund final payment", $"The GP Fund balance is Rs {balance:#,##0.##}; pay it out and close the account.", false)
        : new("gpFund", "GP Fund", "The GP Fund account is closed.", true));
    }

    var posts = heldPosts is { Count: > 0 }
      ? heldPosts.ToList()
      : await context.PositionAssignments.AsNoTracking().Where(a => a.EmployeeId == employee.Id && a.Status == RecordStatus.Active && a.EffectiveTo == date)
          .Select(a => a.PostId).Distinct().ToListAsync(cancellationToken);
    if (posts.Count > 0)
    {
      var day = date.AddDays(1);
      var headed = await (
          from v in context.OrganizationUnitVersions.AsNoTracking()
          where v.HeadPostId != null && posts.Contains(v.HeadPostId!) && v.Status == RecordStatus.Active
            && v.EffectiveFrom <= day && (v.EffectiveTo == null || v.EffectiveTo >= day)
          join p in context.Posts.AsNoTracking() on v.HeadPostId equals p.Id
          select new { v.Name, p.PostCode, PostId = p.Id })
        .ToListAsync(cancellationToken);
      foreach (var unit in headed)
      {
        var filled = await context.PositionAssignments.AnyAsync(a => a.PostId == unit.PostId && a.Status == RecordStatus.Active
          && a.EffectiveFrom <= day && (a.EffectiveTo == null || a.EffectiveTo >= day), cancellationToken);
        items.Add(new("postsHeaded", $"{unit.Name} needs a head", $"Post {unit.PostCode} heads {unit.Name} and is vacant now: give its charge to someone.", filled, unit.PostId.Value));
      }

      var codes = await context.Posts.AsNoTracking().Where(p => posts.Contains(p.Id)).Select(p => p.PostCode).ToListAsync(cancellationToken);
      items.Add(new("signingPosts", "Signing officer posts", $"If post {string.Join(", ", codes)} signs tax notices or certificates, check its new holder in Taxation.", false));
    }

    items.Add(separation.SettlementPayrollTransactionId is not null || separation.FinalSettlementAmount is not null
      ? new("settlement", "Final settlement", "The final settlement is worked out.", true)
      : new("settlement", "Work out the final settlement", "Leave encashment, loans and GP Fund go into the final settlement payroll run.", false));

    var runningOn = await context.LeaveApplications.AsNoTracking().Where(a => a.EmployeeId == employee.Id && a.Status == LeaveStatus.Approved
      && a.StartDate <= date && a.EndDate > date).ToListAsync(cancellationToken);
    foreach (var leave in runningOn)
      items.Add(new("leave", "Leave runs past the last day", $"Approved leave from {leave.StartDate:dd MMM yyyy} to {leave.EndDate:dd MMM yyyy} runs past the last day of service.", false, leave.Id.Value));

    var appraisals = await context.PerformanceReviews.AsNoTracking().CountAsync(r => r.EmployeeId == employee.Id && r.Status == ReviewStatus.Draft, cancellationToken);
    if (appraisals > 0)
      items.Add(new("appraisals", "Finish the appraisal", $"{appraisals} draft appraisal(s) with content were kept: the reporting officer finishes them for the period served, or HR deletes them.", false));

    var evaluations = await context.PerformanceReviews.AsNoTracking().CountAsync(r => r.EvaluatorId == employee.Id && r.Status == ReviewStatus.Draft, cancellationToken);
    if (evaluations > 0)
      items.Add(new("evaluations", "Appraisals they were writing", $"{evaluations} appraisal(s) had this employee as reporting officer: give them another evaluator.", false));

    var impact = await payroll.RunsAsync(employee.Id, date.AddDays(1), null, PayrollImpactFinder.Recovery, cancellationToken);
    return new SeparationChecklistDto(employee.Id.Value, [], items, impact);
  }
}
