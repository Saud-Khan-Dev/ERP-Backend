public class SetPropertyActivationHandler(IApplicationDbContext context)
  : ICommandHandler<SetPropertyActivationCommand, Result<SetPropertyActivationCommandResult>>
{
  public async Task<Result<SetPropertyActivationCommandResult>> Handle(SetPropertyActivationCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.Id, cancellationToken);

    if (command.IsActive)
      property.Activate();
    else
      property.Deactivate();

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetPropertyActivationCommandResult>.Success(new SetPropertyActivationCommandResult(property.IsActive));
  }
}
