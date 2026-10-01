using System.Globalization;
using Microsoft.EntityFrameworkCore;

/// The register's filters and sort order, shared by the paged register (GET /assets) and the reports
/// (GET /asset-reports/register) so a report always lists exactly what the register shows for the same filters.
public static class AssetQueryFilter
{
  public static async Task<IQueryable<Asset>> ApplyAsync(IApplicationDbContext context, GetAssetsQuery query, CancellationToken cancellationToken)
  {
    var assets = context.Assets.AsNoTracking();

    if (query.AssetClassId.HasValue)
    {
      var classId = AssetClassId.Of(query.AssetClassId.Value);
      assets = assets.Where(a => a.AssetClassId == classId);
    }

    if (query.AssetTypeId.HasValue)
    {
      var typeId = AssetTypeId.Of(query.AssetTypeId.Value);
      assets = assets.Where(a => a.AssetTypeId == typeId);
    }

    if (query.CategoryId.HasValue)
    {
      var categoryId = AssetCategoryId.Of(query.CategoryId.Value);
      if (query.IncludeSubCategories)
      {
        var root = await context.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
          ?? throw new AssetCategoryNotFoundException($"Asset category {query.CategoryId} was not found.");

        // subtree = the node plus everything under its materialized path
        var subtree = (await context.AssetCategories.AsNoTracking()
            .Where(c => c.AssetClassId == root.AssetClassId)
            .ToListAsync(cancellationToken))
          .Where(c => c.Id == root.Id || root.IsAncestorOf(c))
          .Select(c => c.Id)
          .ToList();

        assets = assets.Where(a => subtree.Contains(a.CategoryId));
      }
      else
      {
        assets = assets.Where(a => a.CategoryId == categoryId);
      }
    }

    if (query.StatusId.HasValue)
    {
      var statusId = AssetStatusId.Of(query.StatusId.Value);
      assets = assets.Where(a => a.StatusId == statusId);
    }

    if (query.LocationId.HasValue)
    {
      var locationId = LocationId.Of(query.LocationId.Value);
      if (query.IncludeSubLocations)
      {
        // the location and every location under it (materialized path)
        var root = await context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken)
          ?? throw new LocationNotFoundException($"Location {query.LocationId} was not found.");
        var prefix = root.Path + Location.PathSeparator;
        var subtree = (await context.Locations.AsNoTracking().ToListAsync(cancellationToken))
          .Where(l => l.Id == root.Id || (root.Path != null && l.Path != null && l.Path.StartsWith(prefix, StringComparison.Ordinal)))
          .Select(l => (LocationId?)l.Id)
          .ToList();
        assets = assets.Where(a => subtree.Contains(a.CurrentLocationId));
      }
      else
      {
        assets = assets.Where(a => a.CurrentLocationId == locationId);
      }
    }

    if (query.CustodianId.HasValue)
      assets = assets.Where(a => a.CustodianId == query.CustodianId.Value);

    if (query.DepartmentId.HasValue)
      assets = assets.Where(a => a.DepartmentId == query.DepartmentId.Value);

    if (query.Ownership.HasValue)
      assets = assets.Where(a => a.Ownership == query.Ownership.Value);

    // "by date and time": when the asset was registered
    if (query.RegisteredFrom.HasValue)
    {
      var from = DateTime.SpecifyKind(query.RegisteredFrom.Value, DateTimeKind.Utc);
      assets = assets.Where(a => a.CreatedAt >= from);
    }

    if (query.RegisteredTo.HasValue)
    {
      var to = DateTime.SpecifyKind(query.RegisteredTo.Value, DateTimeKind.Utc);
      assets = assets.Where(a => a.CreatedAt <= to);
    }

    var acquisitions = context.AssetAcquisitions.AsNoTracking();
    if (query.AcquiredFrom.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionDate >= query.AcquiredFrom.Value));
    if (query.AcquiredTo.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionDate <= query.AcquiredTo.Value));
    if (query.CostMin.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionCost >= query.CostMin.Value));
    if (query.CostMax.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionCost <= query.CostMax.Value));

    var disposals = context.AssetDisposals.AsNoTracking();
    if (query.Disposed == DisposalFilter.Exclude)
      assets = assets.Where(a => !disposals.Any(d => d.AssetId == a.Id));
    else if (query.Disposed == DisposalFilter.Only)
      assets = assets.Where(a => disposals.Any(d => d.AssetId == a.Id));

    if (!query.IncludeInactive)
      assets = assets.Where(a => a.IsActive);

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var upper = query.Search.Trim().ToUpperInvariant();
      var lower = query.Search.Trim().ToLowerInvariant();

      // asset codes are normalized upper-case and value-converted, so they only support an exact match
      AssetCode? exactCode = null;
      try { exactCode = AssetCode.Of(upper); } catch (DomainException) { }

      assets = assets.Where(a =>
          (exactCode != null && a.AssetCode == exactCode)
          || a.Name.Value.ToUpper().Contains(upper)
          || (a.Barcode != null && a.Barcode.ToUpper().Contains(upper))
          || (a.Description != null && a.Description.ToLower().Contains(lower)));
    }

    foreach (var filter in query.AttributeFilters ?? Array.Empty<AttributeFilter>())
      assets = await ApplyAttributeFilterAsync(context, assets, filter, cancellationToken);

    return assets;
  }

  public static IOrderedQueryable<Asset> Order(IApplicationDbContext context, IQueryable<Asset> assets, GetAssetsQuery query)
  {
    var acquisitions = context.AssetAcquisitions.AsNoTracking();
    var desc = query.SortDescending;
    IOrderedQueryable<Asset> ordered = query.SortBy switch
    {
      AssetSort.Name => desc ? assets.OrderByDescending(a => a.Name.Value) : assets.OrderBy(a => a.Name.Value),
      AssetSort.Registered => desc ? assets.OrderByDescending(a => a.CreatedAt) : assets.OrderBy(a => a.CreatedAt),
      // assets without a purchase record always come last, whichever direction
      AssetSort.Acquired => desc
        ? assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id)).ThenByDescending(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => (DateOnly?)q.AcquisitionDate).FirstOrDefault())
        : assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id)).ThenBy(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => (DateOnly?)q.AcquisitionDate).FirstOrDefault()),
      AssetSort.Cost => desc
        ? assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id)).ThenByDescending(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => (decimal?)q.AcquisitionCost).FirstOrDefault())
        : assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id)).ThenBy(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => (decimal?)q.AcquisitionCost).FirstOrDefault()),
      _ => desc ? assets.OrderByDescending(a => a.AssetCode) : assets.OrderBy(a => a.AssetCode),
    };

    return ordered.ThenBy(a => a.AssetCode);
  }

  private static async Task<IQueryable<Asset>> ApplyAttributeFilterAsync(IApplicationDbContext context, IQueryable<Asset> assets, AttributeFilter filter, CancellationToken cancellationToken)
  {
    var code = AttributeCode.Of(filter.Code);
    var definition = await context.AttributeDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException($"Attribute '{filter.Code}' was not found.");

    var definitionId = definition.Id;
    var values = context.AssetAttributeValues.Where(v => v.AttributeDefinitionId == definitionId);
    var op = filter.Operator;

    switch (definition.DataType)
    {
      case AttributeDataType.Integer:
      case AttributeDataType.Decimal:
      {
        if (!decimal.TryParse(filter.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
          throw new DomainException($"Filter value for '{filter.Code}' must be a number.");

        values = op switch
        {
          AttributeFilterOperator.Eq => values.Where(v => v.ValueNumber == number),
          AttributeFilterOperator.Neq => values.Where(v => v.ValueNumber != number),
          AttributeFilterOperator.Gt => values.Where(v => v.ValueNumber > number),
          AttributeFilterOperator.Gte => values.Where(v => v.ValueNumber >= number),
          AttributeFilterOperator.Lt => values.Where(v => v.ValueNumber < number),
          AttributeFilterOperator.Lte => values.Where(v => v.ValueNumber <= number),
          _ => throw new DomainException($"Operator {op} is not supported for numeric attributes.")
        };
        break;
      }

      case AttributeDataType.Date:
      case AttributeDataType.DateTime:
      {
        if (!DateTime.TryParse(filter.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment))
          throw new DomainException($"Filter value for '{filter.Code}' must be a date.");

        var date = DateOnly.FromDateTime(moment);
        var isDate = definition.DataType == AttributeDataType.Date;

        values = op switch
        {
          AttributeFilterOperator.Eq => isDate ? values.Where(v => v.ValueDate == date) : values.Where(v => v.ValueDatetime == moment),
          AttributeFilterOperator.Neq => isDate ? values.Where(v => v.ValueDate != date) : values.Where(v => v.ValueDatetime != moment),
          AttributeFilterOperator.Gt => isDate ? values.Where(v => v.ValueDate > date) : values.Where(v => v.ValueDatetime > moment),
          AttributeFilterOperator.Gte => isDate ? values.Where(v => v.ValueDate >= date) : values.Where(v => v.ValueDatetime >= moment),
          AttributeFilterOperator.Lt => isDate ? values.Where(v => v.ValueDate < date) : values.Where(v => v.ValueDatetime < moment),
          AttributeFilterOperator.Lte => isDate ? values.Where(v => v.ValueDate <= date) : values.Where(v => v.ValueDatetime <= moment),
          _ => throw new DomainException($"Operator {op} is not supported for date attributes.")
        };
        break;
      }

      case AttributeDataType.Boolean:
      {
        if (!bool.TryParse(filter.Value, out var flag))
          throw new DomainException($"Filter value for '{filter.Code}' must be true or false.");

        values = op switch
        {
          AttributeFilterOperator.Eq => values.Where(v => v.ValueBoolean == flag),
          AttributeFilterOperator.Neq => values.Where(v => v.ValueBoolean != flag),
          _ => throw new DomainException($"Operator {op} is not supported for boolean attributes.")
        };
        break;
      }

      default:
      {
        var text = definition.DataType is AttributeDataType.Select or AttributeDataType.MultiSelect
          ? filter.Value.Trim().ToUpperInvariant()
          : filter.Value.Trim();

        values = op switch
        {
          AttributeFilterOperator.Eq => values.Where(v => v.ValueText == text),
          AttributeFilterOperator.Neq => values.Where(v => v.ValueText != text),
          AttributeFilterOperator.Contains => values.Where(v => v.ValueText != null && v.ValueText.ToUpper().Contains(text.ToUpperInvariant())),
          _ => throw new DomainException($"Operator {op} is not supported for text attributes.")
        };
        break;
      }
    }

    return assets.Where(a => values.Any(v => v.AssetId == a.Id));
  }
}
