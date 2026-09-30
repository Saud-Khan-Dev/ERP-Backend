using Microsoft.EntityFrameworkCore;

/// Read-side assembly shared by several queries: area summaries and ownership / owner / property refs.
public class PropertyReadService(IApplicationDbContext context, MasterLookup masters)
{
  /// Schema guide, "How to read areas": the current measurement gives total and built-up area; active,
  /// in-force regularizations add to it. Encroached area joins in phase 3.
  public async Task<AreaSummaryDto> AreaSummaryAsync(PropertyId propertyId, CancellationToken cancellationToken)
  {
    var current = await context.PropertyMeasurements.AsNoTracking()
        .Where(m => m.PropertyId == propertyId && m.IsCurrent)
        .OrderByDescending(m => m.CreatedAt)
        .FirstOrDefaultAsync(cancellationToken);

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var regularizations = await context.AreaRegularizations.AsNoTracking()
        .Where(r => r.PropertyId == propertyId && r.IsActive && r.RegularizationStatus == RegularizationStatus.Regularized)
        .ToListAsync(cancellationToken);

    var regularized = regularizations.Where(r => r.CountsTowardArea(today)).Sum(r => r.AdditionalAreaBase);

    return new AreaSummaryDto(
      current?.Id.Value,
      current?.TotalAreaBase,
      current?.BuiltUpAreaBase,
      regularized,
      current is null ? null : current.TotalAreaBase + regularized);
  }

  public async Task<List<OwnershipDto>> OwnershipDtosAsync(IReadOnlyCollection<PropertyOwnership> ownerships, CancellationToken cancellationToken)
  {
    var refs = await masters.Refs()
        .Add<TenureType>(ownerships.Select(o => o.TenureTypeId))
        .Add<TransferType>(ownerships.Select(o => o.AcquisitionTransferTypeId))
        .LoadAsync(cancellationToken);

    var properties = await PropertyRefsAsync(ownerships.Select(o => o.PropertyId), cancellationToken);
    var owners = await OwnerRefsAsync(ownerships.Select(o => o.OwnerId), cancellationToken);

    return ownerships.Select(o => o.ToDto(refs, properties, owners)).ToList();
  }

  public async Task<Dictionary<OwnerId, OwnerRef>> OwnerRefsAsync(IEnumerable<OwnerId?> ids, CancellationToken cancellationToken)
  {
    var wanted = ids.Where(id => id is not null).Select(id => id!).Distinct().ToList();
    if (wanted.Count == 0)
      return new Dictionary<OwnerId, OwnerRef>();

    return (await context.Owners.AsNoTracking().Where(o => wanted.Contains(o.Id)).ToListAsync(cancellationToken))
        .ToDictionary(o => o.Id, o => o.ToRef());
  }

  public async Task<Dictionary<PropertyId, PropertyRef>> PropertyRefsAsync(IEnumerable<PropertyId?> ids, CancellationToken cancellationToken)
  {
    var wanted = ids.Where(id => id is not null).Select(id => id!).Distinct().ToList();
    if (wanted.Count == 0)
      return new Dictionary<PropertyId, PropertyRef>();

    return (await context.Properties.AsNoTracking().Where(p => wanted.Contains(p.Id)).ToListAsync(cancellationToken))
        .ToDictionary(p => p.Id, p => p.ToRef());
  }
}
