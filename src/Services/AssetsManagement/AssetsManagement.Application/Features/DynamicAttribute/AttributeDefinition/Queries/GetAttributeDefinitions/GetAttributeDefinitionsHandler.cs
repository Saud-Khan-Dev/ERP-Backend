using Microsoft.EntityFrameworkCore;

public class GetAttributeDefinitionsHandler(IApplicationDbContext context)
  : IQueryHandler<GetAttributeDefinitionsQuery, Result<GetAttributeDefinitionsQueryResult>>
{
  public async Task<Result<GetAttributeDefinitionsQueryResult>> Handle(GetAttributeDefinitionsQuery query, CancellationToken cancellationToken)
  {
    var definitions = context.AttributeDefinitions.AsNoTracking();

    if (query.DataType.HasValue)
      definitions = definitions.Where(d => d.DataType == query.DataType.Value);

    if (!query.IncludeInactive)
      definitions = definitions.Where(d => d.IsActive);

    var data = await definitions.ToListAsync(cancellationToken);

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();
      data = data.Where(d =>
          d.Code.Value.Contains(term, StringComparison.OrdinalIgnoreCase)
          || d.Name.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
        .ToList();
    }

    var ordered = data.OrderBy(d => d.Name.Value, StringComparer.OrdinalIgnoreCase).Select(d => d.ToDto()).ToList();

    return Result<GetAttributeDefinitionsQueryResult>.Success(new GetAttributeDefinitionsQueryResult(ordered));
  }
}
