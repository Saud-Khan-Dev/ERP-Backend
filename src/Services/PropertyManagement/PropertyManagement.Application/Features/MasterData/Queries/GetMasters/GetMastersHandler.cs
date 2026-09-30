public class GetMastersHandler(IApplicationDbContext context)
  : IQueryHandler<GetMastersQuery, Result<GetMastersQueryResult>>
{
  public async Task<Result<GetMastersQueryResult>> Handle(GetMastersQuery query, CancellationToken cancellationToken)
  {
    var descriptor = MasterRegistry.Get(query.Type);
    var items = await descriptor.ListAsync(context, query.IncludeInactive, cancellationToken);

    return Result<GetMastersQueryResult>.Success(
      new GetMastersQueryResult(descriptor.Slug, items.Select(m => m.ToDto()).ToList()));
  }
}
