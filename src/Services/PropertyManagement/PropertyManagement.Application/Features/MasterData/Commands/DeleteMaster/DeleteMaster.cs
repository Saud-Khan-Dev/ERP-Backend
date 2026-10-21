public sealed record DeleteMasterCommandResult(bool IsSuccess);

/// Deletes a value nothing refers to (entered by mistake, a leftover). A system value, or one any record uses, is refused:
/// deactivate it instead, so old records keep it.
public sealed record DeleteMasterCommand(string Type, Guid Id) : ICommand<Result<DeleteMasterCommandResult>>;

public class DeleteMasterHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteMasterCommand, Result<DeleteMasterCommandResult>>
{
  public async Task<Result<DeleteMasterCommandResult>> Handle(DeleteMasterCommand command, CancellationToken cancellationToken)
  {
    var descriptor = MasterRegistry.Get(command.Type);
    var master = await descriptor.FindAsync(context, MasterId.Of(command.Id), cancellationToken)
      ?? throw new MasterDataNotFoundException($"{descriptor.Label} {command.Id} was not found.");

    if (SystemMasterCodes.IsRequired(descriptor.ClrType, master.Code.Value))
      return Result<DeleteMasterCommandResult>.Failure($"'{master.Name.Value}' is used by the system - it can be renamed but not deleted.");

    var used = (await context.ReferenceCountsAsync(descriptor.ClrType, new[] { command.Id }, cancellationToken)).GetValueOrDefault(command.Id);
    if (used > 0)
      return Result<DeleteMasterCommandResult>.Failure(
        $"'{master.Name.Value}' is used by {used} record(s), so it cannot be deleted. Switch it off instead: old records keep it, new ones cannot choose it.");

    descriptor.Remove(context, master);
    await context.SaveChangesAsync(cancellationToken);
    return Result<DeleteMasterCommandResult>.Success(new DeleteMasterCommandResult(true));
  }
}
