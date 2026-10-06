using Microsoft.EntityFrameworkCore;

/// What applying an action may need beyond the action itself (the hr_action_request table has no column for these).
public sealed record HrActionApplyInput(
  string? ExternalReferenceOrg,
  Guid? RecruitmentMethodId,
  EmploymentMethod? EmploymentMethod,
  Guid? EventTypeId,
  int? StageNumber,
  decimal? BasicPay,
  int? NoticePeriodDays,
  string? Remarks);

/// Carries out an approved HR action: one service-history row, plus what the action changes - a post (appointment,
/// transfer, promotion, demotion, deputation in), the pay (promotion / demotion fixation, appointment), the service
/// status (suspension, LWOP, deputation out, reinstatement, joining) or the end of service (retirement, resignation,
/// termination, death). The action is marked applied with the history row. Nothing is saved here.
public class HrActionApplier(IApplicationDbContext context, ServiceRecordReader reader, ServiceRecordWriter writer)
{
  public async Task<EmployeeServiceHistory> ApplyAsync(HrActionRequest action, HrActionApplyInput input, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(action.EmployeeId.Value, cancellationToken);
    var date = action.EffectiveDate;
    var current = await reader.RegularPostOnAsync(employee.Id, date, cancellationToken);
    action.EnsureStillCurrent(current?.AsOld() ?? ServiceChange.None);

    RecruitmentMethodId? recruitmentMethod = null;
    if (input.RecruitmentMethodId is { } methodId)
    {
      recruitmentMethod = RecruitmentMethodId.Of(methodId);
      var method = await context.RecruitmentMethods.FirstOrDefaultAsync(m => m.Id == recruitmentMethod, cancellationToken)
        ?? throw new RecruitmentMethodNotFoundException($"Recruitment method {methodId} was not found.");
      method.EnsureActive();
    }

    EmployeeServiceHistory history;
    switch (action.ActionType)
    {
      case HrActionType.Appointment:
      case HrActionType.DeputationIn:
        history = await writer.AppointAsync(employee, new AppointmentSpec(
          action.NewPostId!, date, ServiceEventNames.For(action.ActionType)!, action.OrderNumber, recruitmentMethod,
          input.StageNumber, input.BasicPay, action.SupportingDocumentId, input.ExternalReferenceOrg, action.Reason, input.Remarks, action.ApprovedBy), cancellationToken);
        break;

      case HrActionType.Transfer:
      case HrActionType.Promotion:
      case HrActionType.Demotion:
        history = await writer.MoveAsync(employee, current!, new MoveSpec(
          action.NewPostId!, date, action.ActionType, action.OrderNumber, action.SupportingDocumentId, action.Reason, input.Remarks, action.ApprovedBy), cancellationToken);
        break;

      case HrActionType.Retirement:
      case HrActionType.Resignation:
      case HrActionType.Termination:
      case HrActionType.Death:
        var type = action.ActionType switch
        {
          HrActionType.Retirement => SeparationType.Retirement,
          HrActionType.Resignation => SeparationType.Resignation,
          HrActionType.Death => SeparationType.Death,
          _ => SeparationType.Termination
        };
        history = (await writer.SeparateAsync(employee, new SeparationSpec(type, date, action.OrderNumber, action.Reason, input.NoticePeriodDays,
          null, null, action.SupportingDocumentId, action.ApprovedBy), cancellationToken)).History;
        break;

      default:
        history = await StatusEventAsync(action, employee, current, input, recruitmentMethod, cancellationToken);
        break;
    }

    action.MarkApplied(history.Id);
    return history;
  }

  /// Actions that change no post: deputation out, regularization, LWOP, suspension, reinstatement, joining, other.
  private async Task<EmployeeServiceHistory> StatusEventAsync(
      HrActionRequest action,
      Employee employee,
      CurrentPost? current,
      HrActionApplyInput input,
      RecruitmentMethodId? recruitmentMethod,
      CancellationToken cancellationToken)
  {
    var date = action.EffectiveDate;
    var eventType = action.ActionType == HrActionType.Other
      ? await context.LoadEventTypeAsync(input.EventTypeId ?? throw new DomainException("Choose the service event type to record this action under."), cancellationToken)
      : await context.LoadEventTypeAsync(ServiceEventNames.For(action.ActionType)!, cancellationToken);

    switch (action.ActionType)
    {
      case HrActionType.DeputationOut:
        employee.EnsureInService();
        if (string.IsNullOrWhiteSpace(input.ExternalReferenceOrg))
          throw new DomainException("Name the department the employee is deputed to.");
        // the seat is released for the deputation; the employee keeps a lien and comes back through a joining / appointment
        if (current is not null)
          current.Assignment.End(date.AddDays(-1) < current.Assignment.EffectiveFrom ? current.Assignment.EffectiveFrom : date.AddDays(-1));
        employee.ChangeEmploymentStatus(EmploymentStatus.DeputedOut);
        break;

      case HrActionType.Regularization:
        employee.EnsureInService();
        if (employee.EmploymentType == EmploymentType.Regular)
          throw new DomainException($"{employee.DisplayName} is already a regular employee.");
        employee.ChangeEmployment(EmploymentType.Regular, input.EmploymentMethod
          ?? throw new DomainException("A regularized employee needs an employment method (scheduled seat or GDA personal)."), employee.ProjectId);
        break;

      case HrActionType.Lwop:
        employee.EnsureInService();
        employee.ChangeEmploymentStatus(EmploymentStatus.OnLeave);
        break;

      case HrActionType.Suspension:
        employee.EnsureInService();
        if (employee.EmploymentStatus == EmploymentStatus.Suspended)
          throw new DomainException($"{employee.DisplayName} is already suspended.");
        employee.ChangeEmploymentStatus(EmploymentStatus.Suspended);
        break;

      case HrActionType.Reinstatement:
        if (employee.EmploymentStatus is not (EmploymentStatus.Suspended or EmploymentStatus.Terminated))
          throw new DomainException($"Only a suspended or terminated employee is reinstated; {employee.DisplayName} is {EnumText.Words(employee.EmploymentStatus)}.");
        if (employee.EmploymentStatus == EmploymentStatus.Terminated)
        {
          // a court-ordered reinstatement after dismissal: the separation no longer stands (its history row stays)
          var separation = await context.Separations.FirstOrDefaultAsync(s => s.EmployeeId == employee.Id, cancellationToken);
          if (separation?.SettlementPayrollTransactionId is not null)
            throw new DomainException("The final settlement has been paid; record a fresh appointment instead of a reinstatement.");
          if (separation is not null)
            context.Separations.Remove(separation);
        }
        employee.ChangeEmploymentStatus(EmploymentStatus.Active);
        break;

      case HrActionType.Joining:
        employee.EnsureProfileActive();
        if (employee.HasLeftService)
          throw new DomainException($"{employee.DisplayName} has left service; record an appointment or reinstatement instead.");
        employee.ChangeEmploymentStatus(EmploymentStatus.Active);
        break;
    }

    var change = action.ActionType == HrActionType.DeputationOut
      ? current?.AsOld() ?? ServiceChange.None
      : ServiceRecordWriter.Unchanged(current);
    return writer.RecordEvent(employee, eventType, date, change, recruitmentMethod, input.ExternalReferenceOrg, action.OrderNumber,
      action.SupportingDocumentId, action.Reason, input.Remarks, action.ApprovedBy);
  }
}
