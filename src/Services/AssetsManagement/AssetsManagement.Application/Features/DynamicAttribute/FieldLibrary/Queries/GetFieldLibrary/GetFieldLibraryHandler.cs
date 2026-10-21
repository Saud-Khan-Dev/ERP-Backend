using Microsoft.EntityFrameworkCore;

public class GetFieldLibraryHandler(IApplicationDbContext context, IAssetDataQueries queries)
  : IQueryHandler<GetFieldLibraryQuery, Result<GetFieldLibraryQueryResult>>
{
  public async Task<Result<GetFieldLibraryQueryResult>> Handle(GetFieldLibraryQuery query, CancellationToken cancellationToken)
  {
    var definitions = await context.AttributeDefinitions.AsNoTracking().ToListAsync(cancellationToken);
    var assignments = await context.AttributeAssignments.AsNoTracking().Where(a => a.AssetId == null).ToListAsync(cancellationToken);
    var optionSets = await context.OptionSets.AsNoTracking().Include(o => o.Values).ToDictionaryAsync(o => o.Id, cancellationToken);
    var holding = await queries.AssetsHoldingAsync(definitions.Select(d => d.Code.Value).ToList(), cancellationToken);

    var fields = definitions
      .OrderBy(d => d.Name.Value, StringComparer.OrdinalIgnoreCase)
      .Select(d =>
      {
        var count = holding.GetValueOrDefault(d.Code.Value);
        var choices = d.OptionSetId is { } setId && optionSets.TryGetValue(setId, out var set)
          ? set.Values.OrderBy(v => v.DisplayOrder ?? int.MaxValue).ThenBy(v => v.Label).Select(v => v.ToDto()).ToList()
          : [];
        var placements = assignments.Where(a => a.AttributeDefinitionId == d.Id)
          .Select(a => new FieldPlacementDto(
            a.Id.Value, a.Scope, (a.AssetClassId?.Value ?? a.AssetTypeId?.Value ?? a.CategoryId?.Value) ?? Guid.Empty,
            a.IsActive, a.IsHidden, a.IsRequired, a.IsFilterable, a.IsSearchable, a.IsVisibleInList, a.InheritToChildren, a.LabelOverride))
          .ToList();
        return new FieldLibraryItemDto(d.ToDto(), choices, placements, count, count == 0 && !d.IsSystem);
      })
      .ToList();

    return Result<GetFieldLibraryQueryResult>.Success(new GetFieldLibraryQueryResult(fields));
  }
}
