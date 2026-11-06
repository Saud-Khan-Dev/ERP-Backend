using Microsoft.EntityFrameworkCore;

/// How the signed-in taxpayer holds a property today: Owner (a current ownership), Tenant (a rental in force), Lessee (a
/// lease in force) or Allottee (an allotment in force).
public enum MyHolding { Owner, Tenant, Lessee, Allottee }

/// One property the taxpayer holds, as the portal shows it. Areas in sq ft (the base unit); Share only for an owner;
/// AgreementNo, Amount and Frequency for a rental or lease.
public sealed record MyPropertyDto(
  Guid PropertyId, string PropertyCode, string PropertyName, string? AddressLine, string? Town, string? PropertyType, string? Classification,
  decimal? LandAreaSqft, decimal? CoveredAreaSqft, MyHolding Holding, decimal? SharePct, DateOnly Since, string? AgreementNo, decimal? Amount,
  string? Frequency, DateOnly? Until);

/// Linked: the account is a taxpayer's (the token carries `own`); Name is their owner record's name.
public sealed record MyPropertiesDto(bool Linked, string? Name, string? OwnerCode, IReadOnlyList<MyPropertyDto> Properties);

/// The taxpayer portal's view of Property: the properties the signed-in person holds today. The owner comes from the
/// token, never from the request; read only; not narrowed by a town scope (a taxpayer has none).
public sealed record GetMyPropertiesQuery : IQuery<Result<MyPropertiesDto>>;

public class GetMyPropertiesHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : IQueryHandler<GetMyPropertiesQuery, Result<MyPropertiesDto>>
{
  public async Task<Result<MyPropertiesDto>> Handle(GetMyPropertiesQuery query, CancellationToken cancellationToken)
  {
    if (currentUser.OwnerId is not { } ownerGuid)
      return Result<MyPropertiesDto>.Success(new(false, null, null, []));

    var ownerId = OwnerId.Of(ownerGuid);
    var today = PropertyWriteGuard.Today();
    var owner = await context.Owners.IgnoreQueryFilters().AsNoTracking().Where(o => o.Id == ownerId)
      .Select(o => new { Name = o.OwnerName.Value, Code = o.OwnerCode.Value }).FirstOrDefaultAsync(cancellationToken);
    if (owner is null)
      return Result<MyPropertiesDto>.Success(new(true, currentUser.DisplayName, null, []));

    var rows = new List<(PropertyId Property, MyHolding Holding, decimal? Share, DateOnly Since, string? No, decimal? Amount, string? Frequency, DateOnly? Until)>();

    var ownerships = await context.Ownerships.IgnoreQueryFilters().AsNoTracking()
      .Where(o => o.OwnerId == ownerId && (o.EffectiveTo == null || o.EffectiveTo >= today) && o.EffectiveFrom <= today).ToListAsync(cancellationToken);
    rows.AddRange(ownerships.Where(o => o.IsCurrent).Select(o => (o.PropertyId, MyHolding.Owner, (decimal?)o.OwnershipSharePct, o.EffectiveFrom, (string?)null, (decimal?)null, (string?)null, o.EffectiveTo)));

    var rentals = await context.Rentals.IgnoreQueryFilters().AsNoTracking().Where(r => r.TenantOwnerId == ownerId && r.RentalStartDate <= today).ToListAsync(cancellationToken);
    var rentalStatuses = await context.Set<RentalStatus>().AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);
    rows.AddRange(rentals.Where(r => rentalStatuses.TryGetValue(r.RentalStatusId, out var s) && r.IsInForce(s))
      .Select(r => (r.PropertyId, MyHolding.Tenant, (decimal?)null, r.RentalStartDate, (string?)r.RentalNo.Value, (decimal?)r.RentAmount, (string?)r.RentFrequency.ToString(), r.RentalEndDate)));

    var leases = await context.Leases.IgnoreQueryFilters().AsNoTracking().Where(l => l.LesseeOwnerId == ownerId && l.LeaseStartDate <= today).ToListAsync(cancellationToken);
    var leaseStatuses = await context.Set<LeaseStatus>().AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);
    rows.AddRange(leases.Where(l => leaseStatuses.TryGetValue(l.LeaseStatusId, out var s) && l.IsInForce(s))
      .Select(l => (l.PropertyId, MyHolding.Lessee, (decimal?)null, l.LeaseStartDate, (string?)l.LeaseNo.Value, l.LeaseAmount, l.AmountFrequency?.ToString(), (DateOnly?)l.LeaseEndDate)));

    var allotments = await context.Allotments.IgnoreQueryFilters().AsNoTracking().Where(a => a.AllotteeOwnerId == ownerId).ToListAsync(cancellationToken);
    var allotmentStatuses = await context.Set<AllotmentStatus>().AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);
    rows.AddRange(allotments.Where(a => allotmentStatuses.TryGetValue(a.AllotmentStatusId, out var s) && a.IsInForceOn(s, today))
      .Select(a => (a.PropertyId, MyHolding.Allottee, (decimal?)null, a.TakesEffectOn, (string?)a.AllotmentNo.Value, (decimal?)null, (string?)null, a.ExpiryDate)));

    var ids = rows.Select(r => r.Property).Distinct().ToList();
    var properties = await context.Properties.IgnoreQueryFilters().AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
    var areas = await context.PropertyMeasurements.IgnoreQueryFilters().AsNoTracking().Where(m => ids.Contains(m.PropertyId) && m.IsCurrent)
      .ToDictionaryAsync(m => m.PropertyId, cancellationToken);
    var refs = await masters.Refs()
      .Add<Town>(properties.Values.Select(p => (MasterId?)p.TownId))
      .Add<PropertyType>(properties.Values.Select(p => (MasterId?)p.PropertyTypeId))
      .Add<PropertyClassification>(properties.Values.Select(p => (MasterId?)p.PropertyClassificationId))
      .LoadAsync(cancellationToken);

    var list = rows.Where(r => properties.ContainsKey(r.Property)).Select(r =>
      {
        var p = properties[r.Property];
        var area = areas.GetValueOrDefault(r.Property);
        return new MyPropertyDto(p.Id.Value, p.PropertyCode.Value, p.PropertyName.Value, p.AddressLine, refs[p.TownId]?.Name, refs[p.PropertyTypeId]?.Name,
          refs[p.PropertyClassificationId]?.Name, area?.TotalAreaBase, area?.BuiltUpAreaBase, r.Holding, r.Share, r.Since, r.No, r.Amount, r.Frequency, r.Until);
      })
      .OrderBy(p => p.PropertyCode, StringComparer.Ordinal).ThenBy(p => p.Holding).ToList();

    return Result<MyPropertiesDto>.Success(new(true, owner.Name, owner.Code, list));
  }
}
