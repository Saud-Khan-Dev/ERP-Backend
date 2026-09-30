public class SetMasterActivationHandler(IApplicationDbContext context)
  : ICommandHandler<SetMasterActivationCommand, Result<SetMasterActivationCommandResult>>
{
  public async Task<Result<SetMasterActivationCommandResult>> Handle(SetMasterActivationCommand command, CancellationToken cancellationToken)
  {
    var descriptor = MasterRegistry.Get(command.Type);
    var master = await descriptor.FindAsync(context, MasterId.Of(command.Id), cancellationToken)
      ?? throw new MasterDataNotFoundException($"{descriptor.Label} {command.Id} was not found.");

    if (command.IsActive)
    {
      master.Activate();
    }
    else
    {
      MasterRules.EnsureCanDeactivate(descriptor, master);
      master.Deactivate();
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetMasterActivationCommandResult>.Success(new SetMasterActivationCommandResult(master.IsActive));
  }
}
