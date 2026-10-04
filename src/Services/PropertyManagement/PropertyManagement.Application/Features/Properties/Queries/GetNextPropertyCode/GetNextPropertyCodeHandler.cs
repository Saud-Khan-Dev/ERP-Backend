public class GetNextPropertyCodeHandler(CodeGenerator codes)
  : IQueryHandler<GetNextPropertyCodeQuery, Result<GetNextPropertyCodeQueryResult>>
{
  public async Task<Result<GetNextPropertyCodeQueryResult>> Handle(GetNextPropertyCodeQuery query, CancellationToken cancellationToken) =>
      Result<GetNextPropertyCodeQueryResult>.Success(
        new GetNextPropertyCodeQueryResult((await codes.PreviewAsync(CodeSequenceKeys.Property, cancellationToken)).Value));
}
