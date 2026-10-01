using Microsoft.AspNetCore.Mvc;

/// multipart/form-data: file (required), attachmentType (Image | Document | Other, optional), title, isPrimaryImage.
public sealed class UploadAssetAttachmentForm
{
  public IFormFile? File { get; set; }
  public AttachmentType? AttachmentType { get; set; }
  public string? Title { get; set; }
  public bool? IsPrimaryImage { get; set; }
}

public sealed record AddAssetAttachmentResponse(Guid Id, string OriginalFileName, long FileSize);

public class AddAssetAttachment : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/assets/{id}/attachments", async (Guid id, [FromForm] UploadAssetAttachmentForm form, ISender sender) =>
    {
      if (form.File is null || form.File.Length == 0)
        throw new BadHttpRequestException("Attach the file in the 'file' form field.");

      await using var content = form.File.OpenReadStream();
      var result = await sender.Send(new UploadAssetAttachmentCommand(
        id, content, form.File.FileName, form.File.ContentType, form.File.Length, form.AttachmentType, form.Title, form.IsPrimaryImage ?? false));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      var response = new AddAssetAttachmentResponse(result.Value!.Id, result.Value.OriginalFileName, result.Value.FileSize);
      return Results.Created($"/assets/{id}/attachments/{response.Id}", response);
    })
      .DisableAntiforgery()
      .Accepts<UploadAssetAttachmentForm>("multipart/form-data")
      .WithName("AddAssetAttachment")
      .Produces<AddAssetAttachmentResponse>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Upload Asset Attachment")
      .WithDescription("Uploads a document or photo of the asset (multipart/form-data, field 'file'). Stored under /asset-files/{asset code}/; the first photo becomes the main picture, isPrimaryImage=true makes this one the main picture.");

    app.MapGet("/assets/{id}/attachments/{attachmentId}/content", async (Guid id, Guid attachmentId, ISender sender) =>
    {
      var result = await sender.Send(new GetAssetAttachmentContentQuery(id, attachmentId));
      var file = result.Value!;
      return Results.File(file.Content, file.MimeType, file.FileName);
    })
      .WithName("GetAssetAttachmentContent")
      .Produces(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Download Asset Attachment")
      .WithDescription("The file itself, with its original name.");
  }
}
