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

/// Leaving service.
public sealed record SeparationSpec(
  SeparationType Type,
  DateOnly SeparationDate,
  string? OrderNumber,
  string? Reason,
  int? NoticePeriodDays,
  EmploymentStatus? EmploymentStatus,
  ServiceEventTypeId? EventTypeId,
  EmployeeDocumentId? SupportingDocumentId,
  Guid? ApprovedBy);

/// Writes the service record: every change of post or service status is one service-history row plus the matching
/// assignment, pay and status changes, so the history always explains the current state. Nothing here saves; the
/// caller saves once, so each use case is one transaction.
public class ServiceRecordWriter(IApplicationDbContext context, ServiceRecordReader reader, PayService pay)
{
  public async Task<EmployeeServiceHistory> AppointAsync(Employee employee, AppointmentSpec spec, CancellationToken cancellationToken)
  {
    employee.EnsureInService();
    var date = spec.EffectiveFrom;
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
  /// ended, service status set, a service-history row and the separation record written. Loans still owed are noted.
  public async Task<(EmployeeServiceHistory History, EmployeeSeparation Separation)> SeparateAsync(Employee employee, SeparationSpec spec, CancellationToken cancellationToken)
  {
    employee.EnsureInService();
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

    foreach (var assignment in await reader.LiveAssignmentsOfEmployeeAsync(employee.Id, cancellationToken))
    {
      if (assignment.EffectiveFrom > date)
        assignment.Cancel("Service ended before this assignment started.");
      else if (assignment.EffectiveTo is null || assignment.EffectiveTo > date)
        assignment.End(date);
    }

    await pay.CloseAsync(employee.Id, date, cancellationToken);

    foreach (var shift in await context.EmployeeShifts.Where(s => s.EmployeeId == employee.Id && (s.EffectiveTo == null || s.EffectiveTo > date)).ToListAsync(cancellationToken))
    {
      if (shift.EffectiveFrom <= date)
        shift.EndOn(date);
    }

    employee.ChangeEmploymentStatus(spec.EmploymentStatus ?? StatusAfter(spec.Type));

    var owed = await context.Loans.Where(l => l.EmployeeId == employee.Id && l.Status == LoanStatus.Active).SumAsync(l => l.RemainingBalance, cancellationToken);
    var separation = EmployeeSeparation.Record(SeparationId.New(), employee.Id, spec.Type, date, history.Id, spec.OrderNumber, spec.Reason,
      spec.NoticePeriodDays, owed > 0 ? owed : null);
    context.Separations.Add(separation);

    return (history, separation);
  }

  /// The service status a separation leaves the employee in. A deputationist going back, a contract ending or any
  /// other separation ends GDA service like a termination.
  public static EmploymentStatus StatusAfter(SeparationType type) => type switch
  {
    SeparationType.Retirement => EmploymentStatus.Retired,
    SeparationType.Resignation => EmploymentStatus.Resigned,
    SeparationType.Death => EmploymentStatus.Deceased,
    _ => EmploymentStatus.Terminated
  };
}
