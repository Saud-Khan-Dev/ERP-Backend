public class SetDocumentActivationHandler(IApplicationDbContext context, DocumentService documents)
  : ICommandHandler<SetDocumentActivationCommand, Result<SetDocumentActivationCommandResult>>
{
  public async Task<Result<SetDocumentActivationCommandResult>> Handle(SetDocumentActivationCommand command, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(command.Id, cancellationToken);

    if (command.IsActive)
      document.Activate();
    else
      document.Deactivate();

    // a CNIC copy switched off (or back on) moves the owner's CNIC pointer
    await documents.RefreshOwnerCnicAsync(document, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetDocumentActivationCommandResult>.Success(new SetDocumentActivationCommandResult(document.IsActive));
  }
}
