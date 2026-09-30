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
    var refs = await masters.Refs().Add<AttributeGroup>(rows.Select(d => d.AttributeGroupId)).LoadAsync(cancellationToken);

    return Result<GetAttributeDefinitionsQueryResult>.Success(
      new GetAttributeDefinitionsQueryResult(rows.Select(d => d.ToDto(refs)).ToList()));
  }
}
