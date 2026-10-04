using Microsoft.EntityFrameworkCore;

/// Read-side assembly shared by several queries: area summaries and ownership / owner / property refs.
public class PropertyReadService(IApplicationDbContext context, MasterLookup masters)
{
  /// Schema guide, "How to read areas": the current measurement gives total and built-up area; active,
  /// in-force regularizations add to it; encroached area is the sum of unresolved encroachments.
  public async Task<AreaSummaryDto> AreaSummaryAsync(PropertyId propertyId, CancellationToken cancellationToken) =>
      (await AreaSummariesAsync(new[] { propertyId }, cancellationToken))[propertyId];

  /// The same summary for many properties at once (three queries in all), one entry per requested id.
  public async Task<Dictionary<PropertyId, AreaSummaryDto>> AreaSummariesAsync(IReadOnlyCollection<PropertyId> propertyIds, CancellationToken cancellationToken)
  {
    var ids = propertyIds.Distinct().ToList();

    var current = (await context.PropertyMeasurements.AsNoTracking()
        .Where(m => ids.Contains(m.PropertyId) && m.IsCurrent)
        .ToListAsync(cancellationToken))
      .GroupBy(m => m.PropertyId)
      .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First());

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var regularized = (await context.AreaRegularizations.AsNoTracking()
        .Where(r => ids.Contains(r.PropertyId) && r.IsActive && r.RegularizationStatus == RegularizationStatus.Regularized)
        .ToListAsync(cancellationToken))
      .Where(r => r.CountsTowardArea(today))
      .GroupBy(r => r.PropertyId)
      .ToDictionary(g => g.Key, g => g.Sum(r => r.AdditionalAreaBase));

    var encroached = (await context.Encroachments.AsNoTracking()
        .Where(e => ids.Contains(e.PropertyId) && e.ResolutionDate == null)
        .Select(e => new { e.PropertyId, e.EncroachmentAreaBase })
        .ToListAsync(cancellationToken))
      .GroupBy(e => e.PropertyId)
      .ToDictionary(g => g.Key, g => g.Sum(e => e.EncroachmentAreaBase));

    return ids.ToDictionary(id => id, id =>
    {
      var measurement = current.GetValueOrDefault(id);
      var extra = regularized.GetValueOrDefault(id);
      return new AreaSummaryDto(
        measurement?.Id.Value,
        measurement?.TotalAreaBase,
        measurement?.BuiltUpAreaBase,
        extra,
        measurement is null ? null : measurement.TotalAreaBase + extra,
        encroached.GetValueOrDefault(id));
    });
  }

  /// Current total area in square feet (the current measurement), for the properties that have one.
  public async Task<Dictionary<PropertyId, decimal>> CurrentTotalAreasAsync(IReadOnlyCollection<PropertyId> propertyIds, CancellationToken cancellationToken)
  {
    var ids = propertyIds.Distinct().ToList();
    return (await context.PropertyMeasurements.AsNoTracking()
        .Where(m => ids.Contains(m.PropertyId) && m.IsCurrent)
        .Select(m => new { m.PropertyId, m.TotalAreaBase, m.CreatedAt })
        .ToListAsync(cancellationToken))
      .GroupBy(m => m.PropertyId)
      .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First().TotalAreaBase);
  }

  /// Current (ACTIVE) ownership rows per property.
  public async Task<Dictionary<PropertyId, List<PropertyOwnership>>> CurrentOwnershipsAsync(IReadOnlyCollection<PropertyId> propertyIds, CancellationToken cancellationToken)
  {
    var ids = propertyIds.Distinct().ToList();
    return (await context.Ownerships.AsNoTracking()
        .Where(o => ids.Contains(o.PropertyId) && o.OwnershipStatus == OwnershipStatus.Active)
        .ToListAsync(cancellationToken))
      .GroupBy(o => o.PropertyId)
      .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.OwnershipSharePct).ToList());
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

  /// Just the code of each property: enough to order a merged list before its page is resolved.
  public async Task<Dictionary<PropertyId, string>> PropertyCodesAsync(IEnumerable<PropertyId> ids, CancellationToken cancellationToken)
  {
    var wanted = ids.Distinct().ToList();
    if (wanted.Count == 0)
      return new Dictionary<PropertyId, string>();

    return (await context.Properties.AsNoTracking().Where(p => wanted.Contains(p.Id)).Select(p => new { p.Id, p.PropertyCode }).ToListAsync(cancellationToken))
        .ToDictionary(p => p.Id, p => p.PropertyCode.Value);
  }

  /// Code, name and town of each property, for the cross-property lists.
  public async Task<Dictionary<PropertyId, PropertyHeader>> PropertyHeadersAsync(IEnumerable<PropertyId> ids, CancellationToken cancellationToken)
  {
    var wanted = ids.Distinct().ToList();
    if (wanted.Count == 0)
      return new Dictionary<PropertyId, PropertyHeader>();

    var properties = await context.Properties.AsNoTracking().Where(p => wanted.Contains(p.Id)).ToListAsync(cancellationToken);
    var towns = await masters.Refs().Add<Town>(properties.Select(p => p.TownId)).LoadAsync(cancellationToken);

    return properties.ToDictionary(p => p.Id, p => new PropertyHeader(p.Id.Value, p.PropertyCode.Value, p.PropertyName.Value, towns[p.TownId]));
  }

  /// The owners themselves (code, name, type, CNIC ...), for rows that show more than a reference.
  public async Task<Dictionary<OwnerId, PropertyOwner>> OwnersAsync(IEnumerable<OwnerId?> ids, CancellationToken cancellationToken)
  {
    var wanted = ids.Where(id => id is not null).Select(id => id!).Distinct().ToList();
    if (wanted.Count == 0)
      return new Dictionary<OwnerId, PropertyOwner>();

    return await context.Owners.AsNoTracking().Where(o => wanted.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);
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
