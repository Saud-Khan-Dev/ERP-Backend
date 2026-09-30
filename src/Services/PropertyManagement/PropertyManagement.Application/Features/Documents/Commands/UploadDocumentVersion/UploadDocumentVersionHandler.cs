using Microsoft.EntityFrameworkCore;

public class UploadDocumentVersionHandler(IApplicationDbContext context, DocumentService documents)
  : ICommandHandler<UploadDocumentVersionCommand, Result<UploadDocumentVersionCommandResult>>
{
  public async Task<Result<UploadDocumentVersionCommandResult>> Handle(UploadDocumentVersionCommand command, CancellationToken cancellationToken)
  {
    var current = await context.LoadDocumentAsync(command.DocumentId, cancellationToken);

    if (await context.Documents.AnyAsync(d => d.SupersedesDocumentId == current.Id, cancellationToken))
      return Result<UploadDocumentVersionCommandResult>.Failure(
        "This is not the latest version of the document. Upload the new version against the latest one.");

    var documentType = await context.Set<DocumentType>().AsNoTracking().FirstAsync(t => t.Id == current.DocumentTypeId, cancellationToken);
    var version = await documents.CreateVersionAsync(current, documentType, command.File, cancellationToken);

    // keep an owner's CNIC pointer on the newest copy
    var owner = await context.Owners.FirstOrDefaultAsync(o => o.CnicDocumentId == current.Id, cancellationToken);
    owner?.AttachCnicDocument(version.Id);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UploadDocumentVersionCommandResult>.Success(
      new UploadDocumentVersionCommandResult(version.Id.Value, version.VersionNo, current.Id.Value));
  }
}
