/// One move of an asset: a transfer, or a loan (a move with a date it must come back). The "to" side is copied onto
/// the asset. The custodian's and the unit's names are kept as they were on the day, so the history reads right after
/// HR renames or moves people.
public class AssetAssignment : Aggregate<AssetAssignmentId>
{
  public AssetId AssetId { get; private set; } = default!;
  public Guid? FromDepartmentId { get; private set; }
  public string? FromDepartmentName { get; private set; }
  public Guid? ToDepartmentId { get; private set; }
  public string? ToDepartmentName { get; private set; }
  public Guid? FromCustodianId { get; private set; }
  public string? FromCustodianName { get; private set; }
  public string? FromCustodianNumber { get; private set; }
  public Guid? ToCustodianId { get; private set; }
  public string? ToCustodianName { get; private set; }
  public string? ToCustodianNumber { get; private set; }
  public LocationId? FromLocationId { get; private set; }
  public LocationId? ToLocationId { get; private set; }
  public DateTime AssignmentDate { get; private set; }
  /// Temporary issue / loan
  public DateOnly? ExpectedReturnDate { get; private set; }
  public DateOnly? ActualReturnDate { get; private set; }
  public string? Reason { get; private set; }
  /// Who recorded the move (the signed-in user).
  public Guid? RecordedBy { get; private set; }
  public string? RecordedByName { get; private set; }

  public bool IsOpenLoan => ExpectedReturnDate.HasValue && !ActualReturnDate.HasValue;

  public CustodyRef From => new(FromDepartmentId, FromDepartmentName, FromCustodianId, FromCustodianName, FromCustodianNumber);

  public static AssetAssignment Create(
      AssetAssignmentId id,
      Asset asset,
      CustodyRef to,
      LocationId? toLocationId,
      DateTime assignmentDate,
      DateOnly? expectedReturnDate,
      string? reason,
      ActorRef recordedBy,
      DateTime now)
  {
    ArgumentNullException.ThrowIfNull(asset);
    ArgumentNullException.ThrowIfNull(to);
    ArgumentNullException.ThrowIfNull(recordedBy);

    if (to.DepartmentId is null && to.CustodianId is null && toLocationId is null)
      throw new DomainException("Say where the asset goes: a location, a person or a department.");

    // a few minutes of clock difference between the browser and the server are not "the future"
    if (assignmentDate > now.AddMinutes(5))
      throw new DomainException("A move cannot be dated in the future.");

    if (expectedReturnDate.HasValue && expectedReturnDate.Value < DateOnly.FromDateTime(assignmentDate))
      throw new DomainException("The date it is due back cannot be before the day it was issued.");

    return new AssetAssignment
    {
      Id = id,
      AssetId = asset.Id,
      FromDepartmentId = asset.DepartmentId,
      FromDepartmentName = asset.DepartmentName,
      ToDepartmentId = to.DepartmentId,
      ToDepartmentName = to.DepartmentId is null ? null : to.DepartmentName,
      FromCustodianId = asset.CustodianId,
      FromCustodianName = asset.CustodianName,
      FromCustodianNumber = asset.CustodianNumber,
      ToCustodianId = to.CustodianId,
      ToCustodianName = to.CustodianId is null ? null : to.CustodianName,
      ToCustodianNumber = to.CustodianId is null ? null : to.CustodianNumber,
      FromLocationId = asset.CurrentLocationId,
      ToLocationId = toLocationId,
      AssignmentDate = assignmentDate,
      ExpectedReturnDate = expectedReturnDate,
      Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
      RecordedBy = recordedBy.Id,
      RecordedByName = recordedBy.ShortName
    };
  }

  public void MarkReturned(DateOnly actualReturnDate, DateOnly today)
  {
    if (!ExpectedReturnDate.HasValue)
      throw new DomainException("Only an asset issued on loan (with a date it is due back) can be returned.");

    if (ActualReturnDate.HasValue)
      throw new DomainException("This loan has already been returned.");

    if (actualReturnDate < DateOnly.FromDateTime(AssignmentDate))
      throw new DomainException("The return cannot be dated before the day the asset was issued.");

    if (actualReturnDate > today)
      throw new DomainException("A return cannot be dated in the future.");

    ActualReturnDate = actualReturnDate;
  }
}
