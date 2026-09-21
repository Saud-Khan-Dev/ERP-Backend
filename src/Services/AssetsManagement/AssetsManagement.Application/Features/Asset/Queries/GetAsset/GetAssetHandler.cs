using Microsoft.EntityFrameworkCore;

public class GetAssetHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : IQueryHandler<GetAssetQuery, Result<GetAssetQueryResult>>
{
  public async Task<Result<GetAssetQueryResult>> Handle(GetAssetQuery query, CancellationToken cancellationToken)
  {
    var id = AssetId.Of(query.Id);
    var asset = await context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {query.Id} was not found.");

    var schema = await schemaService.ResolveAsync(asset.AssetClassId, asset.AssetTypeId, asset.CategoryId, asset.Id, cancellationToken);
    var optionSets = await schemaService.LoadOptionSetsAsync(schema, cancellationToken);

    var attributes = schema.Attributes
      .Select(a => a.ToDto(a.Definition.OptionSetId is { } setId && optionSets.TryGetValue(setId, out var set) ? set : null))
      .ToList();

    return Result<GetAssetQueryResult>.Success(new GetAssetQueryResult(asset.ToDto(), attributes));
  }
}
