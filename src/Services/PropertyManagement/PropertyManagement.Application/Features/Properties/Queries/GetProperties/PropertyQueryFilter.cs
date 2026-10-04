using Microsoft.EntityFrameworkCore;

/// The register's filters and sort order, shared by the paged register (GET /properties) and the register
/// report (GET /property-reports/register), so a report always lists exactly what the register shows.
public static class PropertyQueryFilter
{
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
      var ownerships = context.Ownerships.AsNoTracking();
      properties = properties.Where(p => ownerships.Any(o => o.PropertyId == p.Id && o.OwnerId == ownerId && o.OwnershipStatus == OwnershipStatus.Active));
    }

    // open matters: see OpenMatters
    if (query.HasOpenEncroachment is { } hasOpenEncroachment)
    {
      var open = context.Encroachments.AsNoTracking().Where(OpenMatters.EncroachmentIsOpen);
      properties = hasOpenEncroachment
        ? properties.Where(p => open.Any(e => e.PropertyId == p.Id))
        : properties.Where(p => !open.Any(e => e.PropertyId == p.Id));
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

      // The code is a value-converted BusinessCode. Casting it through object makes EF read the column as plain
      // text (property_code::text LIKE '%…%'), so "prop-1" or "00012" match part of it; codes are stored upper-case.
      properties = properties.Where(p =>
          ((string)(object)p.PropertyCode).Contains(upper)
          || p.PropertyName.Value.ToLower().Contains(lower)
          || (p.KhasraSurveyNo != null && p.KhasraSurveyNo.ToLower().Contains(lower))
          || (p.AddressLine != null && p.AddressLine.ToLower().Contains(lower)));
    }

    return properties;
  }

  public static IOrderedQueryable<Property> Order(IApplicationDbContext context, IQueryable<Property> properties, GetPropertiesQuery query)
  {
    var measurements = context.PropertyMeasurements.AsNoTracking();
    var towns = context.Set<Town>().AsNoTracking();
    var statuses = context.Set<PropertyStatus>().AsNoTracking();
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
      _ => desc ? properties.OrderByDescending(p => p.PropertyCode) : properties.OrderBy(p => p.PropertyCode),
    };

    return ordered.ThenBy(p => p.PropertyCode);
  }
}
