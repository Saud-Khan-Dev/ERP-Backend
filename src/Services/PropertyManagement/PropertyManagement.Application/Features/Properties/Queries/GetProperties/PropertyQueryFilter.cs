using Microsoft.EntityFrameworkCore;

/// The register's filters and sort order, shared by the paged register (GET /properties) and the register
/// report (GET /property-reports/register), so a report always lists exactly what the register shows.
public static class PropertyQueryFilter
{
  /// A current share: ACTIVE or DISPUTED (a disputed share is still held until the dispute is resolved).
  public static IQueryable<PropertyOwnership> CurrentHoldings(IApplicationDbContext context) =>
      context.Ownerships.AsNoTracking().Where(o => o.OwnershipStatus == OwnershipStatus.Active || o.OwnershipStatus == OwnershipStatus.Disputed);

  public static async Task<IQueryable<Property>> ApplyAsync(IApplicationDbContext context, GetPropertiesQuery query, CancellationToken cancellationToken)
  {
    var properties = context.Properties.AsNoTracking();

    if (!query.IncludeInactive)
      properties = properties.Where(p => p.IsActive);

    if (query.TownId is { } townId)
      properties = properties.Where(p => p.TownId == MasterId.Of(townId));

    if (query.PropertyTypeId is { } typeId)
      properties = properties.Where(p => p.PropertyTypeId == MasterId.Of(typeId));

    if (query.PropertyStatusId is { } statusId)
      properties = properties.Where(p => p.PropertyStatusId == MasterId.Of(statusId));

    if (query.PropertyClassificationId is { } classificationId)
      properties = properties.Where(p => p.PropertyClassificationId == MasterId.Of(classificationId));

    // when the property was registered (audit created_at), both ends inclusive
    if (query.RegisteredFrom is { } registeredFrom)
    {
      var from = DateTime.SpecifyKind(registeredFrom, DateTimeKind.Utc);
      properties = properties.Where(p => p.CreatedAt >= from);
    }

    if (query.RegisteredTo is { } registeredTo)
    {
      var to = DateTime.SpecifyKind(registeredTo, DateTimeKind.Utc);
      properties = properties.Where(p => p.CreatedAt <= to);
    }

    // area: the current measurement's total, in square feet (the base unit)
    var measurements = context.PropertyMeasurements.AsNoTracking();
    if (query.AreaMin is { } areaMin)
      properties = properties.Where(p => measurements.Any(m => m.PropertyId == p.Id && m.IsCurrent && m.TotalAreaBase >= areaMin));

    if (query.AreaMax is { } areaMax)
      properties = properties.Where(p => measurements.Any(m => m.PropertyId == p.Id && m.IsCurrent && m.TotalAreaBase <= areaMax));

    if (query.OwnerId is { } ownerGuid)
    {
      var ownerId = OwnerId.Of(ownerGuid);
      var holdings = CurrentHoldings(context);
      properties = properties.Where(p => holdings.Any(o => o.PropertyId == p.Id && o.OwnerId == ownerId));
    }

    // open matters: see OpenMatters
    if (query.HasOpenEncroachment is { } hasOpenEncroachment)
    {
      var open = context.Encroachments.AsNoTracking().Where(OpenMatters.EncroachmentIsOpen);
      properties = hasOpenEncroachment
        ? properties.Where(p => open.Any(e => e.PropertyId == p.Id))
        : properties.Where(p => !open.Any(e => e.PropertyId == p.Id));
    }

    if (query.SteepSlope is { } steep)
    {
      var steepBoundaries = context.Boundaries.AsNoTracking()
        .Where(b => b.IsCurrent && b.SlopeDegrees > PropertyBoundary.SteepSlopeDegrees);
      properties = steep
        ? properties.Where(p => steepBoundaries.Any(b => b.PropertyId == p.Id))
        : properties.Where(p => !steepBoundaries.Any(b => b.PropertyId == p.Id));
    }

    if (query.TransferPending is { } pending)
    {
      var waiting = context.Transfers.AsNoTracking().Where(t => t.TransferStatus == TransferStatus.Initiated || t.TransferStatus == TransferStatus.Approved);
      properties = pending ? properties.Where(p => waiting.Any(t => t.PropertyId == p.Id)) : properties.Where(p => !waiting.Any(t => t.PropertyId == p.Id));
    }

    var today = PakistanDate.Today;
    if (query.Allotted is { } allotted || query.AgreementInForce is not null)
    {
      var current = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);
      var allotments = context.Allotments.AsNoTracking()
        .Where(a => a.IsActive && current.Contains(a.AllotmentStatusId) && (a.EffectiveDate ?? a.AllotmentDate) <= today);

      if (query.Allotted is { } isAllotted)
        properties = isAllotted ? properties.Where(p => allotments.Any(a => a.PropertyId == p.Id)) : properties.Where(p => !allotments.Any(a => a.PropertyId == p.Id));

      if (query.AgreementInForce is { } inForce)
      {
        var lease = await InForce.StatusIdsAsync<LeaseStatus>(context, InForce.LeaseStatuses, cancellationToken);
        var rental = await InForce.StatusIdsAsync<RentalStatus>(context, InForce.RentalStatuses, cancellationToken);
        var contract = await InForce.StatusIdsAsync<ContractStatus>(context, InForce.ContractStatuses, cancellationToken);
        var leases = context.Leases.AsNoTracking().Where(l => lease.Contains(l.LeaseStatusId) && l.LeaseStartDate <= today);
        var rentals = context.Rentals.AsNoTracking().Where(r => rental.Contains(r.RentalStatusId) && r.RentalStartDate <= today);
        var contracts = context.Outsourcings.AsNoTracking().Where(c => contract.Contains(c.ContractStatusId) && c.ContractStartDate <= today);
        properties = inForce
          ? properties.Where(p => allotments.Any(a => a.PropertyId == p.Id) || leases.Any(l => l.PropertyId == p.Id) || rentals.Any(r => r.PropertyId == p.Id) || contracts.Any(c => c.PropertyId == p.Id))
          : properties.Where(p => !allotments.Any(a => a.PropertyId == p.Id) && !leases.Any(l => l.PropertyId == p.Id) && !rentals.Any(r => r.PropertyId == p.Id) && !contracts.Any(c => c.PropertyId == p.Id));
      }
    }

    if (query.HasHolder is { } hasHolder)
    {
      var held = CurrentHoldings(context);
      properties = hasHolder ? properties.Where(p => held.Any(h => h.PropertyId == p.Id)) : properties.Where(p => !held.Any(h => h.PropertyId == p.Id));
    }

    if (query.Charged is { } charged)
    {
      var charges = context.Encumbrances.AsNoTracking().Where(e => e.Status == EncumbranceStatus.Active || e.Status == EncumbranceStatus.Enforced);
      properties = charged ? properties.Where(p => charges.Any(e => e.PropertyId == p.Id)) : properties.Where(p => !charges.Any(e => e.PropertyId == p.Id));
    }

    if (query.Disputed is { } disputed)
    {
      var disputes = context.Ownerships.AsNoTracking().Where(o => o.OwnershipStatus == OwnershipStatus.Disputed);
      properties = disputed ? properties.Where(p => disputes.Any(o => o.PropertyId == p.Id)) : properties.Where(p => !disputes.Any(o => o.PropertyId == p.Id));
    }

    if (query.Measured is { } measured)
      properties = measured
        ? properties.Where(p => measurements.Any(m => m.PropertyId == p.Id && m.IsCurrent))
        : properties.Where(p => !measurements.Any(m => m.PropertyId == p.Id && m.IsCurrent));

    if (query.HasBoundary is { } hasBoundary)
    {
      var surveys = context.Boundaries.AsNoTracking().Where(b => b.IsCurrent);
      properties = hasBoundary ? properties.Where(p => surveys.Any(b => b.PropertyId == p.Id)) : properties.Where(p => !surveys.Any(b => b.PropertyId == p.Id));
    }

    if (query.HasOpenCase is { } hasOpenCase)
    {
      var closed = await OpenMatters.ClosedCaseStatusIdsAsync(context, cancellationToken);
      var open = context.Litigations.AsNoTracking().Where(OpenMatters.CaseIsOpen(closed));
      properties = hasOpenCase
        ? properties.Where(p => open.Any(l => l.PropertyId == p.Id))
        : properties.Where(p => !open.Any(l => l.PropertyId == p.Id));
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var upper = query.Search.Trim().ToUpperInvariant();
      var lower = query.Search.Trim().ToLowerInvariant();

      // parties holding it now whose name contains the term, or whose CNIC starts with it (5 or more digits, dashes
      // optional): current holders, and the allottee, lessee, tenant or contractor in force
      var cnicPrefix = Cnic.NormalizePrefix(query.Search);
      var holders = context.Owners.AsNoTracking()
        .Where(o => o.OwnerName.Value.ToLower().Contains(lower) || (cnicPrefix != null && o.Cnic != null && ((string)(object)o.Cnic).StartsWith(cnicPrefix)))
        .Select(o => o.Id);
      var holdings = CurrentHoldings(context);
      var currentAllotment = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);
      var activeLease = await InForce.StatusIdsAsync<LeaseStatus>(context, InForce.LeaseStatuses, cancellationToken);
      var activeRental = await InForce.StatusIdsAsync<RentalStatus>(context, InForce.RentalStatuses, cancellationToken);
      var activeContract = await InForce.StatusIdsAsync<ContractStatus>(context, InForce.ContractStatuses, cancellationToken);
      var allottees = context.Allotments.AsNoTracking().Where(a => a.IsActive && currentAllotment.Contains(a.AllotmentStatusId) && holders.Contains(a.AllotteeOwnerId)).Select(a => a.PropertyId);
      var lessees = context.Leases.AsNoTracking().Where(l => activeLease.Contains(l.LeaseStatusId) && holders.Contains(l.LesseeOwnerId)).Select(l => l.PropertyId);
      var tenants = context.Rentals.AsNoTracking().Where(r => activeRental.Contains(r.RentalStatusId) && holders.Contains(r.TenantOwnerId)).Select(r => r.PropertyId);
      var contractors = context.Outsourcings.AsNoTracking().Where(c => activeContract.Contains(c.ContractStatusId) && holders.Contains(c.OutsourcedPartyOwnerId)).Select(c => c.PropertyId);

      // The code is a value-converted BusinessCode. Casting it through object makes EF read the column as plain
      // text (property_code::text LIKE '%…%'), so "prop-1" or "00012" match part of it; codes are stored upper-case.
      properties = properties.Where(p =>
          ((string)(object)p.PropertyCode).Contains(upper)
          || p.PropertyName.Value.ToLower().Contains(lower)
          || (p.KhasraSurveyNo != null && p.KhasraSurveyNo.ToLower().Contains(lower))
          || (p.AddressLine != null && p.AddressLine.ToLower().Contains(lower))
          || holdings.Any(h => h.PropertyId == p.Id && holders.Contains(h.OwnerId))
          || allottees.Contains(p.Id) || lessees.Contains(p.Id) || tenants.Contains(p.Id) || contractors.Contains(p.Id));
    }

    return properties;
  }

  public static IOrderedQueryable<Property> Order(IApplicationDbContext context, IQueryable<Property> properties, GetPropertiesQuery query)
  {
    var measurements = context.PropertyMeasurements.AsNoTracking();
    var towns = context.Set<Town>().AsNoTracking();
    var statuses = context.Set<PropertyStatus>().AsNoTracking();
    var types = context.Set<PropertyType>().AsNoTracking();
    var holdings = CurrentHoldings(context);
    var owners = context.Owners.AsNoTracking();
    var desc = query.SortDescending;

    IOrderedQueryable<Property> ordered = query.SortBy switch
    {
      PropertySort.Name => desc ? properties.OrderByDescending(p => p.PropertyName.Value) : properties.OrderBy(p => p.PropertyName.Value),
      PropertySort.Registered => desc ? properties.OrderByDescending(p => p.CreatedAt) : properties.OrderBy(p => p.CreatedAt),
      // properties never measured always come last, whichever direction
      PropertySort.Area => desc
        ? properties.OrderBy(p => !measurements.Any(m => m.PropertyId == p.Id && m.IsCurrent))
            .ThenByDescending(p => measurements.Where(m => m.PropertyId == p.Id && m.IsCurrent).Max(m => (decimal?)m.TotalAreaBase))
        : properties.OrderBy(p => !measurements.Any(m => m.PropertyId == p.Id && m.IsCurrent))
            .ThenBy(p => measurements.Where(m => m.PropertyId == p.Id && m.IsCurrent).Max(m => (decimal?)m.TotalAreaBase)),
      PropertySort.Town => desc
        ? properties.OrderByDescending(p => towns.Where(t => t.Id == p.TownId).Select(t => t.Name).FirstOrDefault())
        : properties.OrderBy(p => towns.Where(t => t.Id == p.TownId).Select(t => t.Name).FirstOrDefault()),
      PropertySort.Status => desc
        ? properties.OrderByDescending(p => statuses.Where(s => s.Id == p.PropertyStatusId).Select(s => s.Name).FirstOrDefault())
        : properties.OrderBy(p => statuses.Where(s => s.Id == p.PropertyStatusId).Select(s => s.Name).FirstOrDefault()),
      PropertySort.Type => desc
        ? properties.OrderByDescending(p => types.Where(t => t.Id == p.PropertyTypeId).Select(t => t.Name).FirstOrDefault())
        : properties.OrderBy(p => types.Where(t => t.Id == p.PropertyTypeId).Select(t => t.Name).FirstOrDefault()),
      // how many owners hold a current share
      PropertySort.Owners => desc
        ? properties.OrderByDescending(p => holdings.Where(h => h.PropertyId == p.Id).Select(h => h.OwnerId).Distinct().Count())
        : properties.OrderBy(p => holdings.Where(h => h.PropertyId == p.Id).Select(h => h.OwnerId).Distinct().Count()),
      // the "Held by" name: the first current holder by name; nobody holding it last, whichever direction
      PropertySort.Holder => desc
        ? properties.OrderBy(p => !holdings.Any(h => h.PropertyId == p.Id))
            .ThenByDescending(p => owners.Where(o => holdings.Any(h => h.PropertyId == p.Id && h.OwnerId == o.Id)).Min(o => o.OwnerName.Value))
        : properties.OrderBy(p => !holdings.Any(h => h.PropertyId == p.Id))
            .ThenBy(p => owners.Where(o => holdings.Any(h => h.PropertyId == p.Id && h.OwnerId == o.Id)).Min(o => o.OwnerName.Value)),
      _ => desc ? properties.OrderByDescending(p => p.PropertyCode) : properties.OrderBy(p => p.PropertyCode),
    };

    return ordered.ThenBy(p => p.PropertyCode);
  }
}
