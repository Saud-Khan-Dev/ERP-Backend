using System.Globalization;
using Microsoft.EntityFrameworkCore;

public class GetAssetsHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : IQueryHandler<GetAssetsQuery, Result<GetAssetsQueryResult>>
{
  public async Task<Result<GetAssetsQueryResult>> Handle(GetAssetsQuery query, CancellationToken cancellationToken)
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
      assets = assets.Where(a => a.CurrentLocationId == locationId);
    }

    if (query.CustodianId.HasValue)
      assets = assets.Where(a => a.CustodianId == query.CustodianId.Value);

    if (query.DepartmentId.HasValue)
      assets = assets.Where(a => a.DepartmentId == query.DepartmentId.Value);

    if (query.ParentAssetId.HasValue)
    {
      var parentId = AssetId.Of(query.ParentAssetId.Value);
      assets = assets.Where(a => a.ParentAssetId == parentId);
    }

    if (!query.IncludeInactive)
      assets = assets.Where(a => a.IsActive);

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var upper = query.Search.Trim().ToUpperInvariant();

      // asset codes are normalized upper-case and value-converted, so they only support an exact match
      AssetCode? exactCode = null;
      try { exactCode = AssetCode.Of(upper); } catch (DomainException) { }

      assets = assets.Where(a =>
          (exactCode != null && a.AssetCode == exactCode)
          || a.Name.Value.ToUpper().Contains(upper)
          || (a.SerialNumber != null && a.SerialNumber.ToUpper().Contains(upper))
          || (a.Barcode != null && a.Barcode.ToUpper().Contains(upper))
          || (a.RfidTag != null && a.RfidTag.ToUpper().Contains(upper)));
    }

    foreach (var filter in query.AttributeFilters ?? Array.Empty<AttributeFilter>())
      assets = await ApplyAttributeFilterAsync(assets, filter, cancellationToken);

    var totalCount = await assets.LongCountAsync(cancellationToken);

    var page = await assets
      .OrderBy(a => a.AssetCode)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
      .Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    // grid columns: attributes flagged is_visible_in_list for each (class, type, category) combination on the page
    var listCodes = new Dictionary<(AssetClassId, AssetTypeId, AssetCategoryId), HashSet<string>>();
    var data = new List<AssetListItemDto>(page.Count);
    foreach (var asset in page)
    {
      var key = (asset.AssetClassId, asset.AssetTypeId, asset.CategoryId);
      if (!listCodes.TryGetValue(key, out var codes))
      {
        var schema = await schemaService.ResolveAsync(asset.AssetClassId, asset.AssetTypeId, asset.CategoryId, null, cancellationToken);
        codes = schema.Attributes.Where(a => a.Assignment.IsVisibleInList).Select(a => a.Code).ToHashSet(StringComparer.Ordinal);
        listCodes[key] = codes;
      }

      data.Add(asset.ToListItemDto(codes));
    }

    return Result<GetAssetsQueryResult>.Success(new GetAssetsQueryResult(
      new PaginatedResult<AssetListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, totalCount, data)));
  }

  private async Task<IQueryable<Asset>> ApplyAttributeFilterAsync(IQueryable<Asset> assets, AttributeFilter filter, CancellationToken cancellationToken)
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
