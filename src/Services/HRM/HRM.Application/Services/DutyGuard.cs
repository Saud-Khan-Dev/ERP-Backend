/// The separation-of-duty rules HRM enforces (Administration › Configuration › Separation of duties; every rule is on
/// unless a Super Administrator switched it off). Each check runs before anything changes; a refusal is kept in the
/// audit trail as a "Refused" line and the act fails with the rule's sentence (400).
public sealed class DutyGuard(IDutyRules rules, ICurrentUser currentUser, IAuditTrail audit)
{
  /// Nobody decides on their own employee record. `act` completes "You cannot ..." ("approve leave").
  public async Task EnsureNotOwnRecordAsync(EmployeeId employeeId, string act, object? record, CancellationToken cancellationToken)
  {
    if (currentUser.EmployeeId is not { } me || me != employeeId.Value)
      return;
    var refusal = await rules.SeparationRefusalAsync(DutyRules.OwnRecord, employeeId.Value, me, $"{act} on your own record", cancellationToken);
    if (refusal is not null)
      await RefuseAsync(refusal, record, cancellationToken);
  }

  /// The person acting now did the first step (`makerId`): refused while `rule` is on.
  public async Task EnsureSeparateAsync(string rule, Guid? makerId, string act, object? record, CancellationToken cancellationToken)
  {
    var refusal = await rules.SeparationRefusalAsync(rule, makerId, currentUser.UserId, act, cancellationToken);
    if (refusal is not null)
      await RefuseAsync(refusal, record, cancellationToken);
  }

  /// Several people did the first steps (a payroll run's preparer, reviewer and adjusters).
  public async Task EnsureSeparateAsync(string rule, IEnumerable<Guid?> makerIds, string act, object? record, CancellationToken cancellationToken)
  {
    if (currentUser.UserId is not { } me || !makerIds.Any(m => m == me))
      return;
    await EnsureSeparateAsync(rule, me, act, record, cancellationToken);
  }

  /// The rule is on (for screens and for steps that change behaviour with it).
  public Task<bool> IsOnAsync(string rule, CancellationToken cancellationToken) => rules.IsOnAsync(rule, cancellationToken);

  private async Task RefuseAsync(string refusal, object? record, CancellationToken cancellationToken)
  {
    if (record is not null)
      audit.Record(AuditActions.Refused, record, refusal);
    else
      audit.Record(AuditActions.Refused, "Act", detail: refusal);
    await audit.SaveAsync(cancellationToken);
    throw new DomainException(refusal);
  }
}
