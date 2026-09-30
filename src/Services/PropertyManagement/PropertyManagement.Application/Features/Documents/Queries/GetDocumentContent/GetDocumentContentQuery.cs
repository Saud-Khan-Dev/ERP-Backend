public sealed record DocumentContent(Stream Content, string MimeType, string FileName);

public sealed record GetDocumentContentQuery(Guid Id) : IQuery<Result<DocumentContent>>;
