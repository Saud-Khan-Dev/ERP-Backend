/// Assignment / transfer history row. The "to" side is copied onto asset.department_id / custodian_id / current_location_id.
public class AssetAssignment : Aggregate<AssetAssignmentId>
{
  public AssetId AssetId { get; private set; } = default!;
  public Guid? FromDepartmentId { get; private set; }
  public Guid? ToDepartmentId { get; private set; }
  public Guid? FromCustodianId { get; private set; }
  public Guid? ToCustodianId { get; private set; }
  public LocationId? FromLocationId { get; private set; }
  public LocationId? ToLocationId { get; private set; }
  public DateTime AssignmentDate { get; private set; }
  /// Temporary issue / loan
  public DateOnly? ExpectedReturnDate { get; private set; }
  public DateOnly? ActualReturnDate { get; private set; }
  public string? Reason { get; private set; }
  public Guid? ApprovedBy { get; private set; }
  public DateTime? ApprovedAt { get; private set; }

  public bool IsOpenLoan => ExpectedReturnDate.HasValue && !ActualReturnDate.HasValue;

  public static AssetAssignment Create(
      AssetAssignmentId id,
      Asset asset,
      Guid? toDepartmentId,
      Guid? toCustodianId,
      LocationId? toLocationId,
      DateTime assignmentDate,
      DateOnly? expectedReturnDate,
      string? reason,
      Guid? approvedBy,
      DateTime? approvedAt)
  {
    ArgumentNullException.ThrowIfNull(asset);

    if (toDepartmentId is null && toCustodianId is null && toLocationId is null)
      throw new DomainException("An assignment must target at least a department, a custodian or a location.");

    if (expectedReturnDate.HasValue && expectedReturnDate.Value < DateOnly.FromDateTime(assignmentDate))
      throw new DomainException("Expected return date cannot be before the assignment date.");

    if (approvedBy is not null && approvedAt is null)
      approvedAt = DateTime.UtcNow;

    return new AssetAssignment
    {
      Id = id,
      AssetId = asset.Id,
      FromDepartmentId = asset.DepartmentId,
      ToDepartmentId = toDepartmentId,
      FromCustodianId = asset.CustodianId,
      ToCustodianId = toCustodianId,
      FromLocationId = asset.CurrentLocationId,
      ToLocationId = toLocationId,
      AssignmentDate = assignmentDate,
      ExpectedReturnDate = expectedReturnDate,
      Reason = reason,
      ApprovedBy = approvedBy,
      ApprovedAt = approvedAt
    };
  }

  public void MarkReturned(DateOnly actualReturnDate)
  {
    if (!ExpectedReturnDate.HasValue)
      throw new DomainException("Only temporary assignments (with an expected return date) can be returned.");

    if (ActualReturnDate.HasValue)
      throw new DomainException("This assignment has already been returned.");

    if (actualReturnDate < DateOnly.FromDateTime(AssignmentDate))
      throw new DomainException("Actual return date cannot be before the assignment date.");

    ActualReturnDate = actualReturnDate;
  }

  public void Approve(Guid approvedBy, DateTime approvedAt)
  {
    if (approvedBy == Guid.Empty)
      throw new DomainException("approved_by is required.");

    ApprovedBy = approvedBy;
    ApprovedAt = approvedAt;
  }
}
