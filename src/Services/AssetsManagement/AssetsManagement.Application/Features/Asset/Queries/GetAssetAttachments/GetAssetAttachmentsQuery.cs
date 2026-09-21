public sealed record GetAssetAttachmentsQueryResult(IReadOnlyList<AssetAttachmentDto> Attachments);

public sealed record GetAssetAttachmentsQuery(Guid AssetId, AttachmentType? AttachmentType) : IQuery<Result<GetAssetAttachmentsQueryResult>>;
