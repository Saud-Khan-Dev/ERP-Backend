using Microsoft.EntityFrameworkCore;

public class GetAttributeDefinitionHandler(IApplicationDbContext context)
  : IQueryHandler<GetAttributeDefinitionQuery, Result<GetAttributeDefinitionQueryResult>>
{
  public async Task<Result<GetAttributeDefinitionQueryResult>> Handle(GetAttributeDefinitionQuery query, CancellationToken cancellationToken)
  {
    var id = AttributeDefinitionId.Of(query.Id);
    var definition = await context.AttributeDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException($"Attribute definition {query.Id} was not found.");

    var assignments = await context.AttributeAssignments.AsNoTracking()
      .Where(a => a.AttributeDefinitionId == id)
      .OrderBy(a => a.Scope)
      .ToListAsync(cancellationToken);

    return Result<GetAttributeDefinitionQueryResult>.Success(
      new GetAttributeDefinitionQueryResult(definition.ToDto(), assignments.Select(a => a.ToDto()).ToList()));
  }
}
