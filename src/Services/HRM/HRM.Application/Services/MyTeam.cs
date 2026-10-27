using Microsoft.EntityFrameworkCore;

/// A supervisor's team: the people whose post reports to a post the supervisor holds today (as its regular holder,
/// acting, on additional charge or looking after it) - the same reporting line appraisals follow. A supervisor gives
/// them tasks and follows their progress from the portal; HR still sees and manages every task.
public class MyTeam(IApplicationDbContext context, IClock clock, EmployeeScope scope)
{
  public async Task<List<EmployeeId>> MembersAsync(EmployeeId supervisor, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    using (scope.Unrestricted())
    {
      var held = context.PositionAssignments.Where(a => a.EmployeeId == supervisor && a.Status == RecordStatus.Active
        && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today)).Select(a => a.PostId);
      var reporting = context.PostVersions.Where(v => v.ReportingPostId != null && held.Contains(v.ReportingPostId)
        && v.EffectiveFrom <= today && (v.EffectiveTo == null || v.EffectiveTo >= today)).Select(v => v.PostId);
      return await context.PositionAssignments.Where(a => reporting.Contains(a.PostId) && a.Status == RecordStatus.Active
          && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today) && a.EmployeeId != supervisor)
        .Select(a => a.EmployeeId).Distinct().ToListAsync(cancellationToken);
    }
  }

  /// Refuses someone outside the team in words (a task of theirs reads as not found to the supervisor).
  public async Task EnsureMemberAsync(EmployeeId supervisor, EmployeeId member, CancellationToken cancellationToken)
  {
    if (!(await MembersAsync(supervisor, cancellationToken)).Contains(member))
      throw new DomainException("This person is not in your team: only people whose post reports to yours can be given tasks here. Ask HR.");
  }
}
