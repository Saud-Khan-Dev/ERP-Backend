public class SetDocumentActivationHandler(IApplicationDbContext context)
  : ICommandHandler<SetDocumentActivationCommand, Result<SetDocumentActivationCommandResult>>
{
  public async Task<Result<SetDocumentActivationCommandResult>> Handle(SetDocumentActivationCommand command, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(command.Id, cancellationToken);

    if (command.IsActive)
      document.Activate();
    else
      document.Deactivate();

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetDocumentActivationCommandResult>.Success(new SetDocumentActivationCommandResult(document.IsActive));
  }
}
