public sealed record DeleteAssetAttachmentResponse(bool IsSuccess);

public class DeleteAssetAttachment : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapDelete("/assets/{id}/attachments/{attachmentId}", async (Guid id, Guid attachmentId, ISender sender) =>
    {
      var result = await sender.Send(new DeleteAssetAttachmentCommand(id, attachmentId));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(result.Value.Adapt<DeleteAssetAttachmentResponse>());
    })
      .WithName("DeleteAssetAttachment")
      .Produces<DeleteAssetAttachmentResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Asset Attachment")
      .WithDescription("Delete Asset Attachment");
  }
}
