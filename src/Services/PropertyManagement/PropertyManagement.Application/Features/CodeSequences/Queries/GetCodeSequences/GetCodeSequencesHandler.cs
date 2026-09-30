using Microsoft.EntityFrameworkCore;

public class GetCodeSequencesHandler(IApplicationDbContext context)
  : IQueryHandler<GetCodeSequencesQuery, Result<GetCodeSequencesQueryResult>>
{
  public async Task<Result<GetCodeSequencesQueryResult>> Handle(GetCodeSequencesQuery query, CancellationToken cancellationToken)
  {
    var stored = await context.CodeSequences.AsNoTracking().ToListAsync(cancellationToken);

    // a sequence the seeder has not created yet shows the defaults it will start with
    var missing = CodeSequenceKeys.Defaults
        .Where(d => stored.All(s => s.Key.Value != d.Key))
        .Select(d => CodeSequence.Create(CodeSequenceId.New(), MasterCode.Of(d.Key), d.Prefix, d.Separator, d.MinimumDigits, 1));

    var sequences = stored.Concat(missing).OrderBy(s => s.Key.Value).Select(s => s.ToDto()).ToList();
    return Result<GetCodeSequencesQueryResult>.Success(new GetCodeSequencesQueryResult(sequences));
  }
}
