public class GetDocumentContentHandler(IApplicationDbContext context, IFileStorage storage, DocumentService documents)
  : IQueryHandler<GetDocumentContentQuery, Result<DocumentContent>>
{
  public async Task<Result<DocumentContent>> Handle(GetDocumentContentQuery query, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(query.Id, cancellationToken);

    if (document.IsConfidential && !documents.CanSeeConfidential())
      throw new DocumentNotFoundException($"Document {query.Id} was not found.");

    var stream = await storage.OpenReadAsync(document.RelativePath, cancellationToken)
      ?? throw new DocumentNotFoundException($"The file for document {query.Id} is missing from storage.");

    return Result<DocumentContent>.Success(new DocumentContent(stream, document.MimeType, document.OriginalFileName));
  }
}
