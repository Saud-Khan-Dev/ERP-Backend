public class UploadDocumentHandler(IApplicationDbContext context, MasterLookup masters, DocumentService documents)
  : ICommandHandler<UploadDocumentCommand, Result<UploadDocumentCommandResult>>
{
  public async Task<Result<UploadDocumentCommandResult>> Handle(UploadDocumentCommand command, CancellationToken cancellationToken)
  {
    var documentType = await masters.GetAsync<DocumentType>(command.DocumentTypeId, cancellationToken);
    var propertyId = command.PropertyId is { } p ? PropertyId.Of(p) : null;

    // an owner route always files against the owner
    var (entityType, entityId) = command.OwnerId is { } ownerId
      ? (DocumentEntityType.Owner, (Guid?)ownerId)
      : (command.EntityType, command.EntityId);

    var target = await documents.ResolveTargetAsync(propertyId, entityType, entityId, cancellationToken);
    var document = await documents.CreateAsync(target, documentType, command.File, command.Details.ToDetails(), cancellationToken);

    // the owner's CNIC copy is a document row too: property_owner.cnic_document_id points at it
    if (target.EntityType == DocumentEntityType.Owner && documentType.Code.Value == SystemMasterCodes.DocumentCnicCopy)
    {
      var owner = await context.LoadOwnerAsync(target.EntityId, cancellationToken);
      owner.AttachCnicDocument(document.Id);
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<UploadDocumentCommandResult>.Success(new UploadDocumentCommandResult(document.Id.Value, document.VersionNo));
  }
}
