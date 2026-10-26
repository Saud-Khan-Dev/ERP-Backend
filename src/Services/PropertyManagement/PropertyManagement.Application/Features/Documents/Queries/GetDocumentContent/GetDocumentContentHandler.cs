public class GetDocumentContentHandler(IApplicationDbContext context, IFileStorage storage, DocumentService documents, IAuditTrail audit)
  : IQueryHandler<GetDocumentContentQuery, Result<DocumentContent>>
{
  public async Task<Result<DocumentContent>> Handle(GetDocumentContentQuery query, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(query.Id, cancellationToken);
    documents.EnsureMaySee(document);

    var stream = await storage.OpenReadAsync(document.RelativePath, cancellationToken)
      ?? throw new DocumentNotFoundException($"The file for document {query.Id} is missing from storage.");

    // who opened which paper (a CNIC copy, a deed) is kept: a sensitive read - looked at, or taken away
    audit.Record(query.Download ? AuditActions.Downloaded : AuditActions.Viewed, document, document.OriginalFileName);
    await audit.SaveAsync(cancellationToken);

    return Result<DocumentContent>.Success(new DocumentContent(stream, document.MimeType, document.OriginalFileName));
  }
}
