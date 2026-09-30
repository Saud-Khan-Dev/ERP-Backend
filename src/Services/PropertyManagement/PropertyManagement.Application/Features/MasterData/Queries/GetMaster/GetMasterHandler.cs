public class GetMasterHandler(IApplicationDbContext context)
  : IQueryHandler<GetMasterQuery, Result<GetMasterQueryResult>>
{
  public async Task<Result<GetMasterQueryResult>> Handle(GetMasterQuery query, CancellationToken cancellationToken)
  {
    var descriptor = MasterRegistry.Get(query.Type);
    var master = await descriptor.FindAsync(context, MasterId.Of(query.Id), cancellationToken)
      ?? throw new MasterDataNotFoundException($"{descriptor.Label} {query.Id} was not found.");

    return Result<GetMasterQueryResult>.Success(new GetMasterQueryResult(master.ToDto()));
  }
}
