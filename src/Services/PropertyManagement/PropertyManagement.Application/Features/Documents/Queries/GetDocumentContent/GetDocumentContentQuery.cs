public sealed record DocumentContent(Stream Content, string MimeType, string FileName);

/// Download: saved to the person's computer (audit "Downloaded"); otherwise opened in the viewer ("Viewed").
public sealed record GetDocumentContentQuery(Guid Id, bool Download = true) : IQuery<Result<DocumentContent>>;
