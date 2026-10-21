/// Who holds an asset and for which unit: the HR ids plus the name and number shown for them, snapshotted when the
/// asset is registered or moved so its history reads right after HR renames or moves people.
public sealed record CustodyRef(
  Guid? DepartmentId,
  string? DepartmentName,
  Guid? CustodianId,
  string? CustodianName,
  string? CustodianNumber)
{
  public static CustodyRef None { get; } = new(null, null, null, null, null);

  public bool IsEmpty => DepartmentId is null && CustodianId is null;
}
