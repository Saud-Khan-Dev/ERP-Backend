/// The Property service, read on behalf of the signed-in user (their token is passed on). Taxation copies what it uses
/// into its own tables at the moment of use, so an approved demand never changes when Property data changes later.
public interface IPropertyDirectory
{
  /// The property with everything Taxation keeps of it: town, type, status, classification, measured and regularized
  /// area, the approved building plan and completion plan, who pays (owners, lessee, tenant, allottee) and the rent and
  /// lease agreements. Throws NotFound when Property has no such property.
  Task<PropertySnapshot> SnapshotAsync(Guid propertyId, CancellationToken cancellationToken);

  /// A town of Property's town master; null when it does not exist.
  Task<ExternalRef?> TownAsync(Guid townId, CancellationToken cancellationToken);

  /// A property type of Property's master; null when it does not exist.
  Task<ExternalRef?> PropertyTypeAsync(Guid propertyTypeId, CancellationToken cancellationToken);
}

/// The HRM service: the holiday calendar for notice periods and who holds a post on a date (the officer who signs a
/// notice, the Tax Collector who serves it). Both reads are open to any signed-in person (HRM's staff directory).
public interface IHrmDirectory
{
  /// False when no HRM address is configured: notice periods then skip weekends only.
  bool IsConfigured { get; }

  Task<IReadOnlySet<DateOnly>> HolidaysAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

  /// The post and who holds it on the date (no holder when vacant); null when HRM has no such post.
  Task<PostHolderInfo?> PostHolderAsync(Guid postId, DateOnly asOf, CancellationToken cancellationToken);
}

/// A post of HRM and the employee holding it on a date (EmployeeId null when vacant).
public sealed record PostHolderInfo(Guid PostId, string? PostCode, string? Designation, Guid? EmployeeId, string? EmployeeNumber, string? Name,
  string? HolderDesignation);

public sealed record ExternalRef(Guid Id, string Code, string Name);

/// Running numbers from database sequences (references, notice numbers, demand, fee, clearance and receipt numbers).
public interface INumberSource
{
  Task<long> NextAsync(NumberSequence sequence, CancellationToken cancellationToken);
}

public enum NumberSequence
{
  Psid,
  NoticeRef,
  DemandNo,
  FeeNo,
  ClearanceNo,
  ReceiptNo
}

/// Where uploaded letterhead logos are kept. The database stores only the relative path.
public interface IFileStorage
{
  Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken cancellationToken);

  /// Null when the file is missing from storage.
  Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken);
}
