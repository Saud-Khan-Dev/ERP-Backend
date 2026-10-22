/// Where a person works - the towns (Property and Taxation records) or HRM offices (employees, sub-units included) they
/// may see and act on. No scope of a kind means every town / every office. Set by an administrator on the account and
/// carried in the access token (ErpClaimTypes.TownScope / OfficeScope); every service filters by it.
public class UserScope : Entity<Guid>
{
  public UserId UserId { get; private set; } = default!;
  public ScopeKind Kind { get; private set; }
  /// The town's or the office's id in its own service.
  public Guid RefId { get; private set; }
  /// Its name when it was given ("Nathia Gali", "Finance Directorate"), so the account reads without a call to the service.
  public string Label { get; private set; } = default!;

  public static UserScope Create(UserId userId, ScopeKind kind, Guid refId, string label)
  {
    ArgumentNullException.ThrowIfNull(userId);
    if (refId == Guid.Empty)
      throw new DomainException("Choose the town or office.");
    if (string.IsNullOrWhiteSpace(label))
      throw new DomainException("The town or office needs its name.");
    return new UserScope { Id = Guid.NewGuid(), UserId = userId, Kind = kind, RefId = refId, Label = label.Trim()[..Math.Min(label.Trim().Length, 200)] };
  }
}

public enum ScopeKind
{
  Town,
  Office
}
