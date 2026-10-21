/// One person as HRM's staff directory shows them - who they are and where they sit today.
public sealed record StaffMember(
  Guid Id,
  string EmployeeNumber,
  string Name,
  string? Designation,
  string? OrgUnit,
  Guid? OrgUnitId,
  string Status,
  /// Active, on leave, suspended or deputed out - not retired, resigned, terminated or deceased.
  bool InService);

public sealed record OrgUnitRef(Guid Id, string Code, string Name, Guid? ParentId);

/// HRM's staff directory, read with the signed-in user's own token (any signed-in person may read it). A failure to
/// reach HRM is an ExternalServiceException (503) - custody is never recorded unchecked.
public interface IHrmDirectory
{
  /// False when no HRM address is configured (Services:HrmBaseUrl).
  bool IsConfigured { get; }

  /// The person, including people who left service; null when HRM has no such person.
  Task<StaffMember?> EmployeeAsync(Guid employeeId, CancellationToken cancellationToken);

  /// Every unit of the organisation today, with its parent.
  Task<IReadOnlyList<OrgUnitRef>> OrgUnitsAsync(CancellationToken cancellationToken);
}
