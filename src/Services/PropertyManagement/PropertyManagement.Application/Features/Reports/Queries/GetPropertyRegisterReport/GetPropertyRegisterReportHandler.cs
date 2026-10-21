using Microsoft.EntityFrameworkCore;

public class GetPropertyRegisterReportHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyRegisterReportQuery, Result<GetPropertyRegisterReportQueryResult>>
{
  /// A report is a document someone reads or prints; beyond this it is an export job, not a report.
  public const int MaxRows = 5000;

  public async Task<Result<GetPropertyRegisterReportQueryResult>> Handle(GetPropertyRegisterReportQuery query, CancellationToken cancellationToken)
  {
    IQueryable<Property> properties;
    if (query.Ids is { Count: > 0 })
    {
      var ids = query.Ids.Distinct().Select(PropertyId.Of).ToList();
      properties = context.Properties.AsNoTracking().Where(p => ids.Contains(p.Id));
    }
    else
    {
      properties = await PropertyQueryFilter.ApplyAsync(context, query.Filter, cancellationToken);
    }

    var count = await properties.CountAsync(cancellationToken);
    if (count > MaxRows)
      return Result<GetPropertyRegisterReportQueryResult>.Failure($"The report would list {count} properties. Narrow the filters to {MaxRows} or fewer.");

    var page = await PropertyQueryFilter.Order(context, properties, query.Filter).ToListAsync(cancellationToken);
    var propertyIds = page.Select(p => p.Id).ToList();

    var refs = await masters.Refs()
        .Add<Town>(page.Select(p => p.TownId))
        .Add<PropertyType>(page.Select(p => p.PropertyTypeId))
        .Add<PropertyStatus>(page.Select(p => p.PropertyStatusId))
        .Add<PropertyClassification>(page.Select(p => p.PropertyClassificationId))
        .LoadAsync(cancellationToken);

    var areas = await read.AreaSummariesAsync(propertyIds, cancellationToken);
    // current holders: active or disputed shares, largest first
    var ownerships = (await PropertyQueryFilter.CurrentHoldings(context).Where(o => propertyIds.Contains(o.PropertyId)).ToListAsync(cancellationToken))
      .GroupBy(o => o.PropertyId)
      .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.OwnershipSharePct).ToList());
    var owners = await read.OwnersAsync(ownerships.Values.SelectMany(o => o).Select(o => (OwnerId?)o.OwnerId), cancellationToken);

    // custom-field values keyed by the field's code: every field that holds a value (one switched off since included -
    // its values stay readable)
    var definitions = await context.AttributeDefinitions.AsNoTracking()
        .ToDictionaryAsync(d => d.Id, d => d.Code.Value, cancellationToken);
    var definitionIds = definitions.Keys.ToList();
    var fields = (await context.PropertyAttributeValues.AsNoTracking()
        .Where(v => propertyIds.Contains(v.PropertyId) && definitionIds.Contains(v.AttributeDefinitionId) && v.Value != null)
        .ToListAsync(cancellationToken))
      .GroupBy(v => v.PropertyId)
      .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, string>)g
        .OrderBy(v => definitions[v.AttributeDefinitionId], StringComparer.Ordinal)
        .ToDictionary(v => definitions[v.AttributeDefinitionId], v => v.Value!));
    IReadOnlyDictionary<string, string> none = new Dictionary<string, string>();

    // the current boundary survey's slope (degrees)
    var slopes = await context.Boundaries.AsNoTracking()
        .Where(b => propertyIds.Contains(b.PropertyId) && b.IsCurrent && b.SlopeDegrees != null)
        .Select(b => new { b.PropertyId, b.SlopeDegrees })
        .ToDictionaryAsync(b => b.PropertyId, b => b.SlopeDegrees, cancellationToken);

    var rows = page.Select(p =>
    {
      var area = areas[p.Id];
      var current = (ownerships.GetValueOrDefault(p.Id) ?? [])
        .Select(o => owners.GetValueOrDefault(o.OwnerId) is { } owner
          ? new ReportOwnerDto(owner.Id.Value, owner.OwnerCode.Value, owner.OwnerName.Value, owner.Cnic?.Value, owner.Ntn, o.OwnershipSharePct,
              o.OwnershipStatus == OwnershipStatus.Disputed)
          : new ReportOwnerDto(o.OwnerId.Value, string.Empty, string.Empty, null, null, o.OwnershipSharePct, o.OwnershipStatus == OwnershipStatus.Disputed))
        .ToList();

      return new PropertyReportRowDto(
        p.Id.Value, p.PropertyCode.Value, p.PropertyName.Value,
        refs[p.TownId], refs[p.PropertyTypeId], refs[p.PropertyStatusId], refs[p.PropertyClassificationId],
        p.AddressLine, p.KhasraSurveyNo, p.Description, p.IsActive,
        area.TotalAreaSqFt, area.BuiltUpAreaSqFt, area.RegularizedAreaSqFt, area.EncroachedAreaSqFt,
        current.Select(o => o.OwnerId).Distinct().Count(), current, fields.GetValueOrDefault(p.Id) ?? none, p.CreatedAt,
        slopes.GetValueOrDefault(p.Id));
    }).ToList();

    var totals = new PropertyReportTotalsDto(rows.Count, rows.Sum(r => r.TotalAreaSqFt ?? 0));

    return Result<GetPropertyRegisterReportQueryResult>.Success(new GetPropertyRegisterReportQueryResult(rows.Count, rows, totals));
  }
}
