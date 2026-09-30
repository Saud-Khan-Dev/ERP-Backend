public class SetAttributeDefinitionActivationHandler(IApplicationDbContext context)
  : ICommandHandler<SetAttributeDefinitionActivationCommand, Result<SetAttributeDefinitionActivationCommandResult>>
{
  public async Task<Result<SetAttributeDefinitionActivationCommandResult>> Handle(SetAttributeDefinitionActivationCommand command, CancellationToken cancellationToken)
  {
    var definition = await context.LoadAttributeDefinitionAsync(command.Id, cancellationToken);

    if (command.IsActive)
      definition.Activate();
    else
      definition.Deactivate();

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetAttributeDefinitionActivationCommandResult>.Success(new SetAttributeDefinitionActivationCommandResult(definition.IsActive));
  }
}
