using Microsoft.EntityFrameworkCore;

public class UploadDocumentHandler(IApplicationDbContext context, MasterLookup masters, DocumentService documents, ICurrentUser currentUser)
  : ICommandHandler<UploadDocumentCommand, Result<UploadDocumentCommandResult>>
{
  public async Task<Result<UploadDocumentCommandResult>> Handle(UploadDocumentCommand command, CancellationToken cancellationToken)
  {
    var documentType = await masters.GetAsync<DocumentType>(command.DocumentTypeId, cancellationToken);

    // filing an owner's papers edits the owner (PROPERTY.EDIT); whoever may register owners (PROPERTY.CREATE) may also
    // file a new owner's first CNIC copy, so registration is complete in one sitting
    if (command.OwnerId is { } paperOwner && !currentUser.HasPermission(PermissionCatalog.Property.Edit))
      await EnsureFirstCnicCopyAsync(OwnerId.Of(paperOwner), documentType, cancellationToken);

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

  /// Without PROPERTY.EDIT (the route lets PROPERTY.CREATE through): only a CNIC copy, and only for an owner who has
  /// none on file yet - replacing one, or filing any other paper, corrects the owner's record. 403 otherwise.
  private async Task EnsureFirstCnicCopyAsync(OwnerId ownerId, DocumentType documentType, CancellationToken cancellationToken)
  {
    if (!documentType.Is(SystemMasterCodes.DocumentCnicCopy))
      throw new PermissionRequiredException(
        $"Filing an owner's {documentType.Name.Value} needs Property: edit. With Property: create you can file only the CNIC copy of an owner who has none on file yet.");

    var owner = await context.Owners.AsNoTracking().Where(o => o.Id == ownerId)
        .Select(o => new { Name = o.OwnerName.Value, o.CnicDocumentId }).FirstOrDefaultAsync(cancellationToken)
      ?? throw new OwnerNotFoundException($"Owner {ownerId.Value} was not found.");

    if (owner.CnicDocumentId is not null)
      throw new PermissionRequiredException(
        $"{owner.Name} already has a CNIC copy on file; replacing it needs Property: edit.");
  }
}
