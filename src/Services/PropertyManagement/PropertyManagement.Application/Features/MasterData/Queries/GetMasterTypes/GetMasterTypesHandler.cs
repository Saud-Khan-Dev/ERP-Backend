public class GetMasterTypesHandler : IQueryHandler<GetMasterTypesQuery, Result<GetMasterTypesQueryResult>>
{
  public Task<Result<GetMasterTypesQueryResult>> Handle(GetMasterTypesQuery query, CancellationToken cancellationToken)
  {
    var types = MasterRegistry.All
        .Select(d => new MasterTypeDto(d.Slug, d.Table, d.Label, d.ExtraFieldNames()))
        .ToList();

    return Task.FromResult(Result<GetMasterTypesQueryResult>.Success(new GetMasterTypesQueryResult(types)));
  }
}
