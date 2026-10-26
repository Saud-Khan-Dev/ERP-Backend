using Microsoft.AspNetCore.Mvc;

/// multipart/form-data body of an upload.
public sealed class UploadDocumentForm
{
  public IFormFile? File { get; set; }
  /// Not used: an owner's paper belongs to no property, so an owner upload that sends it is refused (400).
  public Guid? PropertyId { get; set; }
  public Guid DocumentTypeId { get; set; }
  /// PROPERTY (default), OWNERSHIP, TRANSFER, ENCUMBRANCE, REGULARIZATION ... on a property upload.
  public DocumentEntityType? EntityType { get; set; }
  public Guid? EntityId { get; set; }
  public string? Title { get; set; }
  public DateOnly? DocumentDate { get; set; }
  /// The number on the paper: notesheet / letter no., or the CNIC / passport / NTN / certificate no. of an owner's paper.
  public string? ReferenceNo { get; set; }
  public string? Description { get; set; }
  public bool IsConfidential { get; set; }

  public DocumentDetailsInput Details() => new(Title, DocumentDate, ReferenceNo, Description, IsConfidential);
}

public sealed class UploadVersionForm
{
  public IFormFile? File { get; set; }
}

public sealed record UpdateDocumentRequest(Guid DocumentTypeId, DocumentDetailsInput Details);

/// Scanned files. Bytes go to the file server; the database keeps metadata, checksum and version chain.
public class DocumentEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/documents", async (Guid id, [FromForm] UploadDocumentForm form, ISender sender) =>
    {
      await using var content = OpenUpload(form.File, out var upload);
      return (await sender.Send(new UploadDocumentCommand(
        id, null, form.DocumentTypeId, form.EntityType ?? DocumentEntityType.Property, form.EntityId, form.Details(), upload)))
        .ToCreated(r => $"/documents/{r.Id}");
    })
      .DisableAntiforgery()
      .Accepts<UploadDocumentForm>("multipart/form-data")
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Documents")
      .WithName("UploadPropertyDocument")
      .Produces<UploadDocumentCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Upload Property Document")
      .WithDescription("Files a document under the property, or under one of its records via entityType + entityId (checked to exist on this property). "
        + "The document type must apply to PROPERTY; entityType OWNER is refused (owner papers go to POST /owners/{id}/documents). "
        + "Stored at /property-documents/{code}/{document type folder}/.");

    app.MapGet("/properties/{id:guid}/documents", async (
        Guid id, DocumentEntityType? entityType, Guid? entityId, Guid? documentTypeId, bool? includeSuperseded, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetDocumentsQuery(id, null, entityType, entityId, documentTypeId, includeSuperseded ?? false, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Documents")
      .WithName("GetPropertyDocuments")
      .Produces<GetDocumentsQueryResult>()
      .WithSummary("Get Property Documents")
      .WithDescription("Every document of the property, latest versions only unless includeSuperseded=true.");

    app.MapPost("/owners/{id:guid}/documents", async (Guid id, [FromForm] UploadDocumentForm form, ISender sender) =>
    {
      await using var content = OpenUpload(form.File, out var upload);
      return (await sender.Send(new UploadDocumentCommand(
        form.PropertyId, id, form.DocumentTypeId, DocumentEntityType.Owner, id, form.Details(), upload)))
        .ToCreated(r => $"/documents/{r.Id}");
    })
      .DisableAntiforgery()
      .Accepts<UploadDocumentForm>("multipart/form-data")
      .RequireAnyPermission(PermissionCatalog.Property.Edit, PermissionCatalog.Property.Create)
      .WithTags("Documents")
      .WithName("UploadOwnerDocument")
      .Produces<UploadDocumentCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status403Forbidden)
      .WithSummary("Upload Owner Document")
      .WithDescription("An owner's own paper (CNIC copy, NTN certificate ...). It belongs to no property: sending propertyId is refused. The document type must "
        + "apply to OWNER and the owner must be active; both are checked before the file is stored. Stored at /owner-documents/{OWN code}/{document type folder}/. "
        + "The owner's latest active CNIC Copy becomes the owner's cnicDocumentId. PROPERTY.EDIT files any owner paper; PROPERTY.CREATE (who registers owners) "
        + "may file only a CNIC copy, and only for an owner with none on file yet (403 otherwise).");

    app.MapGet("/owners/{id:guid}/documents", async (Guid id, bool? includeSuperseded, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetDocumentsQuery(null, id, null, null, null, includeSuperseded ?? false, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Documents")
      .WithName("GetOwnerDocuments")
      .Produces<GetDocumentsQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Owner Documents")
      .WithDescription("The owner's own papers, latest versions only unless includeSuperseded=true; inactive ones only with includeInactive=true.");

    var documents = app.MapGroup("/documents").WithTags("Documents");

    documents.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetDocumentQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetDocument")
      .Produces<GetDocumentQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Document");

    documents.MapGet("/{id:guid}/content", async (Guid id, ISender sender) =>
    {
      var result = await sender.Send(new GetDocumentContentQuery(id));
      var file = result.Value!;
      return Results.File(file.Content, file.MimeType, file.FileName);
    })
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("DownloadDocument")
      .Produces(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Download Document")
      .WithDescription("Streams the file with its original name.");

    documents.MapPut("/{id:guid}", async (Guid id, UpdateDocumentRequest request, ISender sender) =>
        (await sender.Send(new UpdateDocumentDetailsCommand(id, request.DocumentTypeId, request.Details))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateDocument")
      .Produces<UpdateDocumentDetailsCommandResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Document Details")
      .WithDescription("Metadata only. A different file is uploaded as a new version. The document type can change only to another of the same scope "
        + "(PROPERTY or OWNER).");

    documents.MapPost("/{id:guid}/versions", async (Guid id, [FromForm] UploadVersionForm form, ISender sender) =>
    {
      await using var content = OpenUpload(form.File, out var upload);
      return (await sender.Send(new UploadDocumentVersionCommand(id, upload))).ToCreated(r => $"/documents/{r.Id}");
    })
      .DisableAntiforgery()
      .Accepts<UploadVersionForm>("multipart/form-data")
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UploadDocumentVersion")
      .Produces<UploadDocumentVersionCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Upload New Version")
      .WithDescription("Adds version n+1 that supersedes this document; the older version stays on record.");

    documents.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetDocumentActivationCommand(id, true))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ActivateDocument")
      .Produces<SetDocumentActivationCommandResult>()
      .WithSummary("Activate Document");

    documents.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetDocumentActivationCommand(id, false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("DeactivateDocument")
      .Produces<SetDocumentActivationCommandResult>()
      .WithSummary("Deactivate Document")
      .WithDescription("Documents are never deleted; one filed in error is deactivated.");
  }

  internal static Stream OpenUpload(IFormFile? file, out DocumentService.Upload upload)
  {
    if (file is null || file.Length == 0)
      throw new BadHttpRequestException("Attach the file in the 'file' form field.");

    var stream = file.OpenReadStream();
    upload = new DocumentService.Upload(stream, file.FileName, file.ContentType, file.Length);
    return stream;
  }
}
