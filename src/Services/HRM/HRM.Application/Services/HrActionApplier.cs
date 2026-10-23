using Microsoft.EntityFrameworkCore;

/// What applying an action may need beyond the draft. Everything is captured in the draft now; a value given here
/// replaces the draft's (kept so older callers still work).
public sealed record HrActionApplyInput(
  string? ExternalReferenceOrg,
  Guid? RecruitmentMethodId,
  Guid? EmploymentMethodId,
  Guid? EventTypeId,
  int? StageNumber,
  decimal? BasicPay,
  int? NoticePeriodDays,
  string? Remarks)
{
  public static readonly HrActionApplyInput None = new(null, null, null, null, null, null, null, null);
}

/// The result of applying an action: the service-history row, the separation checklist when the action ended service,
/// and the posted payroll a back-dated action reaches (to recover or pay as arrears).
public sealed record HrActionApplied(EmployeeServiceHistory History, SeparationChecklistDto? Checklist, IReadOnlyList<PayrollImpactDto> PayrollImpact);

/// Carries out an approved HR action: one service-history row, plus what the action changes - a post (appointment,
/// transfer, promotion, demotion, deputation in, joining or reinstatement on a post), the pay (fixation, reopening),
/// leave without pay (an approved leave of the unpaid type, which stops the pay for its days), the service status
/// (suspension, deputation out, reinstatement, joining) or the end of service. The rules are checked again first
/// (HrActionRules). The action is marked applied with the history row. Nothing is saved here.
public class HrActionApplier(
  IApplicationDbContext context,
  ServiceRecordReader reader,
  ServiceRecordWriter writer,
  HrActionChecks checks,
  LeaveService leave,
  PayrollImpactFinder payroll,
  ICurrentUser currentUser,
  IClock clock)
{
  public async Task<HrActionApplied> ApplyAsync(HrActionRequest action, HrActionApplyInput input, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(action.EmployeeId.Value, cancellationToken);
    var date = action.EffectiveDate;
    var current = await reader.RegularPostOnAsync(employee.Id, date, cancellationToken);
    action.EnsureStillCurrent(current?.AsOld() ?? ServiceChange.None, clock.Today);

    // the draft's extras, unless the apply call gives them
    var details = action.Details with
    {
      ExternalReferenceOrg = input.ExternalReferenceOrg ?? action.ExternalReferenceOrg,
      EmploymentMethodId = input.EmploymentMethodId is { } chosen ? global::EmploymentMethodId.Of(chosen) : action.EmploymentMethodId,
      RecruitmentMethodId = input.RecruitmentMethodId is { } rm ? RecruitmentMethodId.Of(rm) : action.RecruitmentMethodId,
      EventTypeId = input.EventTypeId is { } et ? ServiceEventTypeId.Of(et) : action.EventTypeId,
      StageNumber = input.StageNumber ?? action.StageNumber,
      BasicPay = input.BasicPay ?? action.BasicPay,
      NoticePeriodDays = input.NoticePeriodDays ?? action.NoticePeriodDays
    };
    await checks.EnsureAllowedAsync(employee, details, cancellationToken);

    if (details.RecruitmentMethodId is { } recruitmentMethod)
    {
      var method = await context.RecruitmentMethods.FirstOrDefaultAsync(m => m.Id == recruitmentMethod, cancellationToken)
        ?? throw new RecruitmentMethodNotFoundException($"Recruitment method {recruitmentMethod.Value} was not found.");
      method.EnsureActive();
    }

    EmployeeServiceHistory history;
    SeparationChecklistDto? checklist = null;
    IReadOnlyList<PayrollImpactDto> impact = [];
    var approvedBy = action.ApprovedBy;

    switch (action.ActionType)
    {
      case HrActionType.Appointment:
      case HrActionType.DeputationIn:
        if (action.ActionType == HrActionType.DeputationIn)
          employee.TakeOnDeputation(await TypeForAsync(EmploymentBasis.Deputation, cancellationToken), details.ExternalReferenceOrg!, date, details.EndDate);
        history = await writer.AppointAsync(employee, new AppointmentSpec(
          action.NewPostId!, date, ServiceEventNames.For(action.ActionType)!, action.OrderNumber, details.RecruitmentMethodId,
          details.StageNumber, details.BasicPay, action.SupportingDocumentId, details.ExternalReferenceOrg, action.Reason, input.Remarks, approvedBy), cancellationToken);
        impact = await payroll.RunsAsync(employee.Id, date, null, PayrollImpactFinder.Arrears, cancellationToken);
        break;

      case HrActionType.Transfer:
      case HrActionType.Promotion:
      case HrActionType.Demotion:
        history = await writer.MoveAsync(employee, current!, new MoveSpec(
          action.NewPostId!, date, action.ActionType, action.OrderNumber, action.SupportingDocumentId, action.Reason, input.Remarks, approvedBy), cancellationToken);
        if (action.ActionType != HrActionType.Transfer)
          impact = await payroll.RunsAsync(employee.Id, date, null, action.ActionType == HrActionType.Promotion ? PayrollImpactFinder.Arrears : PayrollImpactFinder.Recovery, cancellationToken);
        break;

      default:
        if (HrActionRequest.SeparationFor(action.ActionType) is { } separationType)
        {
          var separated = await writer.SeparateAsync(employee, new SeparationSpec(separationType, date, action.OrderNumber, action.Reason,
            details.NoticePeriodDays, null, null, action.SupportingDocumentId, approvedBy, action.Id), cancellationToken);
          history = separated.History;
          checklist = separated.Checklist;
          impact = separated.Checklist.PayrollImpact;
        }
        else
        {
          (history, impact) = await StatusEventAsync(action, details, employee, current, input, cancellationToken);
        }
        break;
    }

    action.MarkApplied(history.Id, currentUser.UserId, clock.UtcNow);
    return new HrActionApplied(history, checklist, impact);
  }

  /// Actions that change no post (or put back on one): deputation out, regularization, LWOP, suspension, reinstatement,
  /// joining, a custom event.
  private async Task<(EmployeeServiceHistory, IReadOnlyList<PayrollImpactDto>)> StatusEventAsync(
      HrActionRequest action,
      HrActionDetails details,
      Employee employee,
      CurrentPost? current,
      HrActionApplyInput input,
      CancellationToken cancellationToken)
  {
    var date = action.EffectiveDate;
    var eventType = action.ActionType == HrActionType.Other
      ? await context.LoadEventTypeAsync(details.EventTypeId?.Value ?? throw new DomainException("Choose the service event to record."), cancellationToken)
      : await context.LoadEventTypeAsync(ServiceEventNames.For(action.ActionType)!, cancellationToken);
    var change = ServiceRecordWriter.Unchanged(current);
    IReadOnlyList<PayrollImpactDto> impact = [];
    PostId? placeOn = null;

    switch (action.ActionType)
    {
      case HrActionType.DeputationOut:
        // the seat is released for the deputation; the post is the lien the employee comes back to (the history row
        // keeps it as the old post)
        if (current is not null)
          current.Assignment.End(date.AddDays(-1) < current.Assignment.EffectiveFrom ? current.Assignment.EffectiveFrom : date.AddDays(-1));
        employee.ChangeEmploymentStatus(EmploymentStatus.DeputedOut);
        change = current?.AsOld() ?? ServiceChange.None;
        impact = await payroll.RunsAsync(employee.Id, date, null, PayrollImpactFinder.Recovery, cancellationToken);
        break;

      case HrActionType.Regularization:
        employee.Regularize(await TypeForAsync(EmploymentBasis.Regular, cancellationToken), await context.MethodAsync(details.EmploymentMethodId!.Value, cancellationToken) ?? details.EmploymentMethodId);
        break;

      case HrActionType.Lwop:
        var leaveId = await RecordUnpaidLeaveAsync(action, employee, details, cancellationToken);
        action.LinkLeave(leaveId);
        impact = await payroll.RunsAsync(employee.Id, date, details.EndDate, PayrollImpactFinder.Recovery, cancellationToken);
        break;

      case HrActionType.Suspension:
        employee.ChangeEmploymentStatus(EmploymentStatus.Suspended);
        break;

      case HrActionType.Reinstatement:
        if (employee.EmploymentStatus != EmploymentStatus.Suspended)
        {
          // an order setting aside a termination, dismissal or removal: the separation no longer stands (its history
          // row stays) and the person returns to a post, on the pay they left on
          var separation = await context.Separations.FirstOrDefaultAsync(s => s.EmployeeId == employee.Id, cancellationToken);
          if (separation is not null)
            context.Separations.Remove(separation);
          placeOn = action.NewPostId;
          impact = await payroll.RunsAsync(employee.Id, date, null, PayrollImpactFinder.Arrears, cancellationToken);
        }
        employee.ChangeEmploymentStatus(EmploymentStatus.Active);
        break;

      case HrActionType.Joining:
        employee.ChangeEmploymentStatus(EmploymentStatus.Active);
        if (current is null)
          placeOn = action.NewPostId;
        break;
    }

    if (placeOn is not null)
    {
      var post = await context.LoadPostAsync(placeOn.Value, cancellationToken);
      var version = post.VersionOn(date)!;
      change = new ServiceChange(null, post.Id, null, version.GradeId, null, version.OrgUnitId);
    }

    var history = writer.RecordEvent(employee, eventType, date, change, details.RecruitmentMethodId, details.ExternalReferenceOrg, action.OrderNumber,
      action.SupportingDocumentId, action.Reason, input.Remarks, action.ApprovedBy);

    if (placeOn is not null)
      await writer.PlaceAsync(employee, placeOn, date, history.Id, action.OrderNumber, action.SupportingDocumentId,
        action.ActionType == HrActionType.Reinstatement ? PayChangeReasons.Reinstatement : PayChangeReasons.Joining, cancellationToken);

    return (history, impact);
  }

  /// The employment type a Regularization or a Deputation (in) gives the person (the rules check one exists first).
  private async Task<EmploymentType> TypeForAsync(EmploymentBasis basis, CancellationToken cancellationToken) =>
      await checks.ActiveTypeAsync(basis, cancellationToken)
      ?? throw new DomainException($"No active employment type follows the {EnumText.Words(basis)} rules. Add one under Configuration › People & service first.");

  /// Leave without pay is a dated leave of the unpaid type (payroll leaves its days unpaid): a pending application of
  /// exactly that leave is taken over, otherwise one is recorded, approved by the action's approver.
  private async Task<LeaveApplicationId> RecordUnpaidLeaveAsync(HrActionRequest action, Employee employee, HrActionDetails details, CancellationToken cancellationToken)
  {
    var type = await checks.UnpaidLeaveTypeAsync(cancellationToken)
      ?? throw new DomainException("There is no leave type that is deducted from pay. Add one under Configuration › Leave first.");
    var from = action.EffectiveDate;
    var to = details.EndDate ?? throw new DomainException("Give the last day of the leave without pay.");

    var pending = await context.LeaveApplications.FirstOrDefaultAsync(a => a.EmployeeId == employee.Id && a.Status == LeaveStatus.Pending
      && a.LeaveTypeId == type.Id && a.StartDate == from && a.EndDate == to, cancellationToken);
    if (pending is not null)
    {
      pending.ApproveForHrAction(action.Id, action.ApprovedBy ?? currentUser.UserId, clock.UtcNow);
      return pending.Id;
    }

    var counted = (await leave.CountAsync(employee.Id, from, to, cancellationToken)).Sum(d => d.Days);
    // a leave without pay over off days only still stops the pay for those days; it is charged as one day at least
    var days = Math.Max(counted, 1);
    var others = await context.LeaveApplications.AsNoTracking()
      .Where(a => a.EmployeeId == employee.Id && (a.Status == LeaveStatus.Pending || a.Status == LeaveStatus.Approved)).ToListAsync(cancellationToken);
    var application = LeaveApplication.RecordForHrAction(LeaveApplicationId.New(), employee, type, from, to, Math.Min(days, to.DayNumber - from.DayNumber + 1),
      action.Reason ?? $"Leave without pay (HR action {action.OrderNumber})".Trim(), action.Id, action.ApprovedBy ?? currentUser.UserId, clock.UtcNow, others);
    context.LeaveApplications.Add(application);
    return application.Id;
  }
}
