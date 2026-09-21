using Microsoft.EntityFrameworkCore;

public class ResolveAttributeSchemaHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : IQueryHandler<ResolveAttributeSchemaQuery, Result<ResolveAttributeSchemaQueryResult>>
{
  public async Task<Result<ResolveAttributeSchemaQueryResult>> Handle(ResolveAttributeSchemaQuery query, CancellationToken cancellationToken)
  {
    AssetClassId classId;
    AssetTypeId? typeId;
    AssetCategoryId? categoryId;
    AssetId? assetId = null;

    if (query.AssetId.HasValue)
    {
      assetId = AssetId.Of(query.AssetId.Value);
      var asset = await context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
        ?? throw new AssetNotFoundException($"Asset {query.AssetId} was not found.");

      classId = asset.AssetClassId;
      typeId = asset.AssetTypeId;
      categoryId = asset.CategoryId;
    }
    else
    {
      categoryId = query.CategoryId.HasValue ? AssetCategoryId.Of(query.CategoryId.Value) : null;
      typeId = query.AssetTypeId.HasValue ? AssetTypeId.Of(query.AssetTypeId.Value) : null;

      if (categoryId is not null)
      {
        var category = await context.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
          ?? throw new AssetCategoryNotFoundException($"Asset category {query.CategoryId} was not found.");

        classId = category.AssetClassId;
        typeId ??= category.AssetTypeId;
      }
      else
      {
        classId = AssetClassId.Of(query.AssetClassId!.Value);
      }
    }

    var schema = await schemaService.ResolveAsync(classId, typeId, categoryId, assetId, cancellationToken);
    var optionSets = await schemaService.LoadOptionSetsAsync(schema, cancellationToken);

    var attributes = schema.Attributes
      .Select(a => a.ToDto(a.Definition.OptionSetId is { } setId && optionSets.TryGetValue(setId, out var set) ? set : null))
      .ToList();

    return Result<ResolveAttributeSchemaQueryResult>.Success(new ResolveAttributeSchemaQueryResult(attributes));
  }
}
