public sealed record AddAssetAttachmentRequest(AssetAttachmentInput Attachment);
public sealed record AddAssetAttachmentResponse(Guid Id);

public class AddAssetAttachment : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/attachments", async (Guid id, AddAssetAttachmentRequest request, ISender sender) =>
    {
      var result = await sender.Send(new AddAssetAttachmentCommand(id, request.Attachment));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = result.Value.Adapt<AddAssetAttachmentResponse>();
      return Results.Created($"/assets/{id}/attachments/{response!.Id}", response);
    })
      .WithName("AddAssetAttachment")
      .Produces<AddAssetAttachmentResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Add Asset Attachment")
      .WithDescription("Registers an image / document already stored in object storage; isPrimaryImage replaces the previous primary image.");
  }
}
