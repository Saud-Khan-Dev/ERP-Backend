using Microsoft.EntityFrameworkCore;

public class GetDocumentHandler(IApplicationDbContext context, MasterLookup masters, DocumentService documents)
  : IQueryHandler<GetDocumentQuery, Result<GetDocumentQueryResult>>
{
  public async Task<Result<GetDocumentQueryResult>> Handle(GetDocumentQuery query, CancellationToken cancellationToken)
  {
    var document = await context.LoadDocumentAsync(query.Id, cancellationToken);

    // a confidential document is reported as missing to callers who may not see it
    if (document.IsConfidential && !documents.CanSeeConfidential())
      throw new DocumentNotFoundException($"Document {query.Id} was not found.");

    var isLatest = !await context.Documents.AnyAsync(d => d.SupersedesDocumentId == document.Id, cancellationToken);
    var refs = await masters.Refs().Add<DocumentType>(document.DocumentTypeId).LoadAsync(cancellationToken);

    return Result<GetDocumentQueryResult>.Success(new GetDocumentQueryResult(document.ToDto(refs, isLatest)));
  }
}
