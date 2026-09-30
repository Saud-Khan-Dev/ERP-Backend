/// Edits a numbering scheme. Codes already issued are never rewritten; the next number may not point
/// at a code that is already in use.
public class UpdateCodeSequenceHandler(IApplicationDbContext context, CodeGenerator codes)
  : ICommandHandler<UpdateCodeSequenceCommand, Result<UpdateCodeSequenceCommandResult>>
{
  public async Task<Result<UpdateCodeSequenceCommandResult>> Handle(UpdateCodeSequenceCommand command, CancellationToken cancellationToken)
  {
    var key = MasterCode.Of(command.Key).Value;
    var input = command.Sequence;

    var sequence = await codes.GetSequenceAsync(key, cancellationToken);
    sequence.Update(input.Prefix, input.Separator, input.MinimumDigits, input.NextNumber);

    long? highest = null;
    foreach (var code in await codes.UsedCodesAsync(key, cancellationToken))
      if (sequence.TryReadNumber(code, out var number) && (highest is null || number > highest))
        highest = number;

    if (highest is not null && highest >= sequence.NextNumber)
      return Result<UpdateCodeSequenceCommandResult>.Failure(
        $"{sequence.Format(highest.Value).Value} is already in use; the next number must be at least {highest + 1}.");

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateCodeSequenceCommandResult>.Success(new UpdateCodeSequenceCommandResult(sequence.ToDto()));
  }
}
