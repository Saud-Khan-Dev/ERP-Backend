using Microsoft.EntityFrameworkCore;

public class GetAssetHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : IQueryHandler<GetAssetQuery, Result<GetAssetQueryResult>>
{
  public async Task<Result<GetAssetQueryResult>> Handle(GetAssetQuery query, CancellationToken cancellationToken)
  {
    var id = AssetId.Of(query.Id);
    var asset = await AssetRecordGuard.LoadAsync(context, id, track: false, cancellationToken);

    var schema = await schemaService.ResolveAsync(asset.AssetClassId, asset.AssetTypeId, asset.CategoryId, asset.Id, cancellationToken);

    // values of fields that are not on the form any more - with the field's name, unit and choices to show them by
    var retiredCodes = asset.ExtraAttributes.Keys.Where(code => !schema.Contains(code)).ToList();
    var retiredDefinitions = new List<AttributeDefinition>();
    if (retiredCodes.Count > 0)
    {
      var codes = retiredCodes.Select(AttributeCode.Of).ToList();
      retiredDefinitions = await context.AttributeDefinitions.AsNoTracking().Where(d => codes.Contains(d.Code)).ToListAsync(cancellationToken);
    }

    var optionSets = await schemaService.LoadOptionSetsAsync(
      schema.Attributes.Select(a => a.Definition.OptionSetId).Concat(retiredDefinitions.Select(d => d.OptionSetId)).Where(o => o is not null).Select(o => o!),
      cancellationToken);

    var attributes = schema.Attributes
      .Select(a => a.ToDto(a.Definition.OptionSetId is { } setId && optionSets.TryGetValue(setId, out var set) ? set : null))
      .ToList();

    var retired = retiredCodes
      .Select(code =>
      {
        var definition = retiredDefinitions.FirstOrDefault(d => d.Code.Value == code);
        var options = definition?.OptionSetId is { } setId && optionSets.TryGetValue(setId, out var set)
          ? set.Values.Select(v => v.ToDto()).ToList()
          : [];
        return new RetiredAttributeDto(code, definition?.Name.Value ?? code, definition?.DataType, definition?.Unit, options);
      })
      .OrderBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
      .ToList();

    return Result<GetAssetQueryResult>.Success(new GetAssetQueryResult(asset.ToDto(), attributes, retired));
  }
}
