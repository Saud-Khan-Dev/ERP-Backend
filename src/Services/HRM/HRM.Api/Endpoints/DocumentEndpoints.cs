using Microsoft.AspNetCore.Mvc;

/// multipart/form-data body of a document upload.
public sealed class DocumentUploadForm
{
  public IFormFile? File { get; set; }
  public Guid DocumentTypeId { get; set; }
  public string? DocumentNumber { get; set; }
  public DateOnly? IssueDate { get; set; }
  public DateOnly? ExpiryDate { get; set; }

  public DocumentDetailsInput Details() => new(DocumentTypeId, DocumentNumber, IssueDate, ExpiryDate);
}

/// Employee papers (CNIC, domicile, orders, certificates) with their verification, and education records.
public class DocumentEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/employees/{id:guid}/documents", async (Guid id, ISender sender) => (await sender.Send(new GetEmployeeDocumentsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithTags("Documents")
      .WithName("GetEmployeeDocuments")
      .Produces<GetEmployeeDocumentsQueryResult>()
      .WithSummary("Get Employee Documents");

    app.MapPost("/employees/{id:guid}/documents", async (Guid id, [FromForm] DocumentUploadForm form, ISender sender) =>
    {
      await using var content = Uploads.Open(form.File, out var file);
      return (await sender.Send(new UploadEmployeeDocumentCommand(id, form.Details(), file))).ToCreated(r => $"/documents/{r.Id}");
    })
      .DisableAntiforgery()
      .Accepts<DocumentUploadForm>("multipart/form-data")
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithTags("Documents")
      .WithName("UploadEmployeeDocument")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Upload Document")
      .WithDescription("Files a paper on the employee (form fields: file, documentTypeId, documentNumber, issueDate, expiryDate). Types that expire need the expiry date. Stored at /hrm-files/{employee number}/documents/.");

    var documents = app.MapGroup("/documents").WithTags("Documents");

    documents.MapGet("/expiring", async (int? withinDays, bool? includeExpired, Guid? documentTypeId, ISender sender) =>
        (await sender.Send(new GetExpiringDocumentsQuery(withinDays ?? 60, includeExpired ?? false, documentTypeId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetExpiringDocuments")
      .Produces<GetExpiringDocumentsQueryResult>()
      .WithSummary("Get Expiring Documents")
      .WithDescription("Papers of employees in service that expire within withinDays (default 60), soonest first; includeExpired=true adds those already expired.");

    documents.MapPut("/{id:guid}", async (Guid id, DocumentDetailsInput details, ISender sender) =>
        (await sender.Send(new UpdateDocumentDetailsCommand(id, details))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("UpdateDocumentDetails")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Document Details")
      .WithDescription("Not once verified.");

    documents.MapPost("/{id:guid}/file", async (Guid id, [FromForm] FileUploadForm form, ISender sender) =>
    {
      await using var content = Uploads.Open(form.File, out var file);
      return (await sender.Send(new ReplaceDocumentFileCommand(id, file))).ToOk();
    })
      .DisableAntiforgery()
      .Accepts<FileUploadForm>("multipart/form-data")
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("ReplaceDocumentFile")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Replace Document Scan")
      .WithDescription("A better scan of the same paper. A rejected paper goes back to unverified; a verified one cannot be replaced.");

    documents.MapGet("/{id:guid}/content", async (Guid id, ISender sender) => (await sender.Send(new GetDocumentContentQuery(id))).ToFile())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetDocumentContent")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Download Document");

    foreach (var (step, route, permission, summary) in new[]
    {
      (DocumentReview.Submit, "submit", PermissionCatalog.Hr.Edit, "Send Document for Verification"),
      (DocumentReview.Verify, "verify", PermissionCatalog.Hr.Approve, "Verify Document"),
      (DocumentReview.Reject, "reject", PermissionCatalog.Hr.Approve, "Reject Document")
    })
    {
      documents.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new ReviewDocumentCommand(id, step))).ToOk())
        .RequirePermission(permission)
        .WithName($"{step}Document")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(summary);
    }

    documents.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeleteDocumentCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Delete)
      .WithName("DeleteDocument")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Delete Document")
      .WithDescription("Only a paper uploaded by mistake: not verified and not supporting any service record, HR action or request.");

    // ---- education ----

    app.MapGet("/employees/{id:guid}/education", async (Guid id, ISender sender) => (await sender.Send(new GetEducationQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithTags("Education")
      .WithName("GetEducation")
      .Produces<GetEducationQueryResult>()
      .WithSummary("Get Education");

    app.MapPost("/employees/{id:guid}/education", async (Guid id, EducationInput education, ISender sender) =>
        (await sender.Send(new AddEducationCommand(id, education))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithTags("Education")
      .WithName("AddEducation")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Add Qualification")
      .WithDescription("Marking it the highest qualification moves the flag from any other.");

    var education = app.MapGroup("/education").WithTags("Education");

    education.MapPut("/{id:guid}", async (Guid id, EducationInput input, ISender sender) => (await sender.Send(new UpdateEducationCommand(id, input))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("UpdateEducation")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Qualification");

    education.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new RemoveEducationCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("RemoveEducation")
      .Produces<UpdatedResult>()
      .WithSummary("Remove Qualification");

    education.MapPost("/{id:guid}/file", async (Guid id, [FromForm] FileUploadForm form, ISender sender) =>
    {
      await using var content = Uploads.Open(form.File, out var file);
      return (await sender.Send(new AttachEducationFileCommand(id, file))).ToOk();
    })
      .DisableAntiforgery()
      .Accepts<FileUploadForm>("multipart/form-data")
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("AttachEducationFile")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Attach Degree Scan");

    education.MapGet("/{id:guid}/content", async (Guid id, ISender sender) => (await sender.Send(new GetEducationContentQuery(id))).ToFile())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetEducationContent")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Download Degree Scan");
  }
}
