public class GetSecuritySettingsHandler(ISecuritySettingsProvider settings)
  : IQueryHandler<GetSecuritySettingsQuery, Result<GetSecuritySettingsQueryResult>>
{
  public async Task<Result<GetSecuritySettingsQueryResult>> Handle(GetSecuritySettingsQuery query, CancellationToken cancellationToken)
  {
    var current = await settings.GetAsync(cancellationToken);
    return Result<GetSecuritySettingsQueryResult>.Success(new GetSecuritySettingsQueryResult(current.ToDto()));
  }
}
