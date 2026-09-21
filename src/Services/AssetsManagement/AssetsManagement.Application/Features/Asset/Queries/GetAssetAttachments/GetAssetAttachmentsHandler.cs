using Microsoft.EntityFrameworkCore;

public class GetAssetAttachmentsHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetAttachmentsQuery, Result<GetAssetAttachmentsQueryResult>>
{
  public async Task<Result<GetAssetAttachmentsQueryResult>> Handle(GetAssetAttachmentsQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    if (!await context.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {query.AssetId} was not found.");

    var attachments = context.AssetAttachments.AsNoTracking().Where(a => a.AssetId == assetId);

    if (query.AttachmentType.HasValue)
      attachments = attachments.Where(a => a.AttachmentType == query.AttachmentType.Value);

    var data = await attachments.OrderByDescending(a => a.IsPrimaryImage).ThenBy(a => a.CreatedAt).ToListAsync(cancellationToken);

    return Result<GetAssetAttachmentsQueryResult>.Success(new GetAssetAttachmentsQueryResult(data.Select(a => a.ToDto()).ToList()));
  }
}
