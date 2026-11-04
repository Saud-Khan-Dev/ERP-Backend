public sealed record AttachmentContent(Stream Content, string MimeType, string FileName);

/// The file of an attachment. `Download`: true = a copy is taken (audit "Downloaded"), false = a look in the viewer
/// ("Viewed"), left out = a thumbnail on the page, which writes nothing.
public sealed record GetAssetAttachmentContentQuery(Guid AssetId, Guid AttachmentId, bool? Download = null) : IQuery<Result<AttachmentContent>>;
