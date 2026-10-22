public sealed record AttachmentContent(Stream Content, string MimeType, string FileName);

/// The file of an attachment. `Download`: the person saves it (the Download button, not a thumbnail on the page) - the
/// audit trail then notes who took the file out.
public sealed record GetAssetAttachmentContentQuery(Guid AssetId, Guid AttachmentId, bool Download = false) : IQuery<Result<AttachmentContent>>;
