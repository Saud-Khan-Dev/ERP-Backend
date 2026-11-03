public sealed record DocumentContent(Stream Content, string MimeType, string FileName);

/// Download: a copy is taken (audit "Downloaded"); otherwise it is looked at in the viewer ("Viewed").
public sealed record GetDocumentContentQuery(Guid Id, bool Download = true) : IQuery<Result<DocumentContent>>;
