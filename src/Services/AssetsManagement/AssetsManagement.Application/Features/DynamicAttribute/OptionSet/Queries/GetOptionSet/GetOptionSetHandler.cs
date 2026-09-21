using Microsoft.EntityFrameworkCore;

public class GetOptionSetHandler(IApplicationDbContext context)
  : IQueryHandler<GetOptionSetQuery, Result<GetOptionSetQueryResult>>
{
  public async Task<Result<GetOptionSetQueryResult>> Handle(GetOptionSetQuery query, CancellationToken cancellationToken)
  {
    var id = OptionSetId.Of(query.Id);
    var optionSet = await context.OptionSets.AsNoTracking().Include(o => o.Values).FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
      ?? throw new OptionSetNotFoundException($"Option set {query.Id} was not found.");

    return Result<GetOptionSetQueryResult>.Success(new GetOptionSetQueryResult(optionSet.ToDto()));
  }
}
