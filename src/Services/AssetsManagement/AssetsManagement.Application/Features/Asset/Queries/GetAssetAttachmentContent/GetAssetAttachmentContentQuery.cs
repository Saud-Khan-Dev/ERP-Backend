public sealed record AttachmentContent(Stream Content, string MimeType, string FileName);

public sealed record GetAssetAttachmentContentQuery(Guid AssetId, Guid AttachmentId) : IQuery<Result<AttachmentContent>>;
