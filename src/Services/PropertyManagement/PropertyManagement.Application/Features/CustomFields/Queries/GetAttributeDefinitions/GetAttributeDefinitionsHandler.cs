using Microsoft.EntityFrameworkCore;

public class GetAttributeDefinitionsHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetAttributeDefinitionsQuery, Result<GetAttributeDefinitionsQueryResult>>
{
  public async Task<Result<GetAttributeDefinitionsQueryResult>> Handle(GetAttributeDefinitionsQuery query, CancellationToken cancellationToken)
  {
    var definitions = context.AttributeDefinitions.AsNoTracking();

    if (!query.IncludeInactive)
      definitions = definitions.Where(d => d.IsActive);

    if (query.AttributeGroupId is { } groupId)
      definitions = definitions.Where(d => d.AttributeGroupId == MasterId.Of(groupId));

    var rows = await definitions.OrderBy(d => d.DisplayOrder).ThenBy(d => d.Code).ToListAsync(cancellationToken);
    var activeGroups = (await context.Set<AttributeGroup>().AsNoTracking().Where(g => g.IsActive).Select(g => g.Id).ToListAsync(cancellationToken)).ToHashSet();

    if (query.PropertyTypeId is { } typeId && query.ClassificationId is { } classificationId)
      rows = rows.Where(d => d.IsActive && activeGroups.Contains(d.AttributeGroupId) && d.AppliesTo(typeId, classificationId)).ToList();

    var refs = await masters.Refs().Add<AttributeGroup>(rows.Select(d => d.AttributeGroupId)).LoadAsync(cancellationToken);
    var ids = rows.Select(d => d.Id).ToList();
    var counts = await context.PropertyAttributeValues.AsNoTracking()
        .Where(v => ids.Contains(v.AttributeDefinitionId) && v.Value != null)
        .GroupBy(v => v.AttributeDefinitionId)
        .Select(g => new { Id = g.Key, Count = g.Count() })
        .ToDictionaryAsync(g => g.Id, g => g.Count, cancellationToken);

    return Result<GetAttributeDefinitionsQueryResult>.Success(new GetAttributeDefinitionsQueryResult(
      rows.Select(d => d.ToDto(refs, counts.GetValueOrDefault(d.Id), activeGroups.Contains(d.AttributeGroupId))).ToList()));
  }
}
