public class GetAssetCategoryHandler(IAttributeSchemaService schemaService)
  : IQueryHandler<GetAssetCategoryQuery, Result<GetAssetCategoryQueryResult>>
{
  public async Task<Result<GetAssetCategoryQueryResult>> Handle(GetAssetCategoryQuery query, CancellationToken cancellationToken)
  {
    var chain = await schemaService.GetCategoryChainAsync(AssetCategoryId.Of(query.Id), cancellationToken);

    if (chain.Count == 0)
      throw new AssetCategoryNotFoundException($"Asset category {query.Id} was not found.");

    var category = chain[^1];
    var ancestors = chain.Take(chain.Count - 1).Select(c => c.ToDto()).ToList();

    return Result<GetAssetCategoryQueryResult>.Success(new GetAssetCategoryQueryResult(category.ToDto(), ancestors));
  }
}
