using Microsoft.EntityFrameworkCore;

/// Loads what the HR action rules need for one employee on one date (HrActionRules) and works out what an action
/// would do (the consequence line of the dialog). The same checks run when an action is drafted, changed, submitted,
/// approved and applied, so a draft that would fail at apply is refused up front.
public class HrActionChecks(IApplicationDbContext context, ServiceRecordReader reader, HrmSettings settings)
{
  public async Task<HrActionFacts> FactsAsync(Employee employee, HrActionDetails details, CancellationToken cancellationToken)
  {
    var date = details.EffectiveDate;
    var held = await HeldAsync(employee.Id, date, cancellationToken);
    var target = details.NewPostId is { } postId ? await TargetAsync(postId, date, employee.Id, cancellationToken) : null;
    var separation = await context.Separations.AsNoTracking().FirstOrDefaultAsync(s => s.EmployeeId == employee.Id, cancellationToken);
    var eventType = details.EventTypeId is { } eventTypeId
      ? await context.ServiceEventTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == eventTypeId, cancellationToken)
      : null;
    var unpaidType = await UnpaidLeaveTypeAsync(cancellationToken);
    var clash = details.ActionType == HrActionType.Lwop && details.EndDate is { } end
      ? await LeaveClashAsync(employee.Id, date, end, unpaidType?.Id, cancellationToken)
      : null;
    var dayBefore = date.AddDays(-1);
    var payBefore = await context.PayRecords.AnyAsync(p => p.EmployeeId == employee.Id && p.EffectiveFrom <= dayBefore
      && (p.EffectiveTo == null || p.EffectiveTo >= dayBefore), cancellationToken);

    return new HrActionFacts(employee, settings.RetirementAge, held, target, separation is not null,
      separation?.SettlementPayrollTransactionId is not null, eventType, unpaidType is not null, clash, payBefore);
  }

  /// Refuses the action when it cannot be carried out on its date.
  public async Task EnsureAllowedAsync(Employee employee, HrActionDetails details, CancellationToken cancellationToken) =>
      HrActionRules.Check(details, await FactsAsync(employee, details, cancellationToken));

  /// The regular post held on a date.
  public async Task<HeldPost?> HeldAsync(EmployeeId employeeId, DateOnly date, CancellationToken cancellationToken)
  {
    var current = await reader.RegularPostOnAsync(employeeId, date, cancellationToken);
    if (current is null)
      return null;

    var bps = await context.PayScaleGrades.AsNoTracking().Where(g => g.Id == current.Version.GradeId).Select(g => g.BpsNumber).FirstAsync(cancellationToken);
    return new HeldPost(current.Post.Id, current.Post.PostCode, bps, current.Assignment.EffectiveFrom);
  }

  /// A post as it is on a date, with the most holders it has from then on (the employee not counted).
  public async Task<TargetPost?> TargetAsync(PostId postId, DateOnly date, EmployeeId? employeeId, CancellationToken cancellationToken)
  {
    var post = await context.Posts.AsNoTracking().Include(p => p.Versions).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
    if (post?.VersionOn(date) is not { } version)
      return null;

    var bps = await context.PayScaleGrades.AsNoTracking().Where(g => g.Id == version.GradeId).Select(g => g.BpsNumber).FirstAsync(cancellationToken);
    var holders = await context.PositionAssignments.AsNoTracking()
      .Where(a => a.PostId == postId && a.Status == RecordStatus.Active && a.AssignmentType == AssignmentType.Regular
        && (a.EffectiveTo == null || a.EffectiveTo >= date))
      .ToListAsync(cancellationToken);
    var taken = PostCapacity.MostHeld(holders, new DateRange(date, null), null, employeeId);
    var hasScale = await ScaleInForceAsync(version.GradeId, date, cancellationToken);
    return new TargetPost(post.Id, post.PostCode, bps, version.LifecycleStatus, version.SanctionedCount, taken, hasScale);
  }

  public Task<bool> ScaleInForceAsync(PayScaleGradeId gradeId, DateOnly date, CancellationToken cancellationToken) =>
      context.PayScaleVersions.AnyAsync(v => v.GradeId == gradeId && v.Status == RecordStatus.Active && v.EffectiveFrom <= date
        && (v.EffectiveTo == null || v.EffectiveTo >= date), cancellationToken);

  /// The leave type leave without pay is recorded under: the active type deducted from pay (the first by name when
  /// there are several).
  public Task<LeaveType?> UnpaidLeaveTypeAsync(CancellationToken cancellationToken) =>
      context.LeaveTypes.Where(t => t.IsActive && t.AffectsPayroll).OrderBy(t => t.Name).FirstOrDefaultAsync(cancellationToken);

  /// A live leave application overlapping the range, described (a pending leave without pay of exactly that range is
  /// taken over by the action, so it is no clash).
  private async Task<string?> LeaveClashAsync(EmployeeId employeeId, DateOnly from, DateOnly to, LeaveTypeId? unpaidTypeId, CancellationToken cancellationToken)
  {
    var clash = await context.LeaveApplications.AsNoTracking()
      .Where(a => a.EmployeeId == employeeId && (a.Status == LeaveStatus.Pending || a.Status == LeaveStatus.Approved)
        && a.StartDate <= to && a.EndDate >= from
        && !(a.Status == LeaveStatus.Pending && a.LeaveTypeId == unpaidTypeId && a.StartDate == from && a.EndDate == to))
      .OrderBy(a => a.StartDate)
      .FirstOrDefaultAsync(cancellationToken);
    return clash is null
      ? null
      : $"The leave without pay overlaps {EnumText.WithArticle(clash.Status)} leave from {clash.StartDate:dd MMM yyyy} to {clash.EndDate:dd MMM yyyy}. Cancel or change it first.";
  }
}
