public class UpdateDocumentDetailsHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateDocumentDetailsCommand, Result<UpdateDocumentDetailsCommandResult>>
{
  public async Task<Result<UpdateDocumentDetailsCommandResult>> Handle(UpdateDocumentDetailsCommand command, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(command.Id, cancellationToken);
    var documentType = await masters.GetAsync<DocumentType>(command.DocumentTypeId, cancellationToken);

    document.UpdateDetails(documentType, command.Details.ToDetails());

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateDocumentDetailsCommandResult>.Success(new UpdateDocumentDetailsCommandResult(true));
  }
}
