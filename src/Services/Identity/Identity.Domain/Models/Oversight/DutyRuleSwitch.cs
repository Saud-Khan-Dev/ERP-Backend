/// A separation-of-duty rule's switch (DutyRules in the building block). A rule with no row is ON. Only a Super
/// Administrator switches a rule, always with a reason; every switch is in the activity trail.
public class DutyRuleSwitch : Entity<string>
{
  public bool IsOn { get; private set; }
  public Guid? ChangedById { get; private set; }
  public string ChangedByName { get; private set; } = default!;
  public DateTime ChangedAt { get; private set; }
  public string Reason { get; private set; } = default!;

  public static DutyRuleSwitch Create(string code, bool isOn, Guid? changedById, string changedByName, string reason, DateTime now)
  {
    var rule = new DutyRuleSwitch { Id = code };
    rule.Set(isOn, changedById, changedByName, reason, now);
    return rule;
  }

  public void Set(bool isOn, Guid? changedById, string changedByName, string reason, DateTime now)
  {
    if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
      throw new DomainException("Say why the rule is being switched (at least a few words).");
    IsOn = isOn;
    ChangedById = changedById;
    ChangedByName = changedByName;
    ChangedAt = now;
    Reason = reason.Trim().Length > 500 ? reason.Trim()[..500] : reason.Trim();
  }
}
