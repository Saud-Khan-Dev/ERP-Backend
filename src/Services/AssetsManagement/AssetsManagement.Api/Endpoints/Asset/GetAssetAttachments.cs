public sealed record GetAssetAttachmentsResponse(IReadOnlyList<AssetAttachmentDto> Attachments);

public class GetAssetAttachments : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/assets/{id}/attachments", async (Guid id, ISender sender, AttachmentType? attachmentType) =>
    {
      var result = await sender.Send(new GetAssetAttachmentsQuery(id, attachmentType));
      return Results.Ok(new GetAssetAttachmentsResponse(result.Value!.Attachments));
    })
      .WithName("GetAssetAttachments")
      .Produces<GetAssetAttachmentsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Asset Attachments")
      .WithDescription("Get Asset Attachments");
  }
}
