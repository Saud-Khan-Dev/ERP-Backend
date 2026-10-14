public class UpdateDocumentDetailsHandler(IApplicationDbContext context, MasterLookup masters, DocumentService documents)
  : ICommandHandler<UpdateDocumentDetailsCommand, Result<UpdateDocumentDetailsCommandResult>>
{
  public async Task<Result<UpdateDocumentDetailsCommandResult>> Handle(UpdateDocumentDetailsCommand command, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(command.Id, cancellationToken);
    var documentType = await masters.GetAsync<DocumentType>(command.DocumentTypeId, cancellationToken);

    document.UpdateDetails(documentType, command.Details.ToDetails());

    // re-typed to or from CNIC Copy: the owner's CNIC pointer follows
    await documents.RefreshOwnerCnicAsync(document, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateDocumentDetailsCommandResult>.Success(new UpdateDocumentDetailsCommandResult(true));
  }
}
