public sealed record GetDocumentQueryResult(DocumentDto Document);

public sealed record GetDocumentQuery(Guid Id) : IQuery<Result<GetDocumentQueryResult>>;
