public class UploadDocumentHandler(IApplicationDbContext context, MasterLookup masters, DocumentService documents)
  : ICommandHandler<UploadDocumentCommand, Result<UploadDocumentCommandResult>>
{
  public async Task<Result<UploadDocumentCommandResult>> Handle(UploadDocumentCommand command, CancellationToken cancellationToken)
  {
    var documentType = await masters.GetAsync<DocumentType>(command.DocumentTypeId, cancellationToken);

    // every check (owner active, record on this property, document type scope) runs before the file is stored
    var target = command.OwnerId is { } ownerId
      ? await documents.OwnerTargetAsync(OwnerId.Of(ownerId), cancellationToken)
      : await documents.PropertyTargetAsync(PropertyId.Of(command.PropertyId ?? Guid.Empty), command.EntityType, command.EntityId, cancellationToken);

    var document = await documents.CreateAsync(target, documentType, command.File, command.Details.ToDetails(), cancellationToken);

    // a CNIC copy filed on an owner becomes the owner's CNIC document (property_owner.cnic_document_id)
    await documents.RefreshOwnerCnicAsync(document, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UploadDocumentCommandResult>.Success(new UploadDocumentCommandResult(document.Id.Value, document.VersionNo));
  }
}
