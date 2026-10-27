public sealed class NotesheetStepForm
{
  /// send | forward | return | approve | reject | withdraw
  public string Step { get; set; } = default!;
  public Guid? ToEmployeeId { get; set; }
  public string? Remarks { get; set; }
  public IFormFile? File { get; set; }
}

/// Notesheets in the portal (/me/notesheets): written by an employee and moved from officer to officer until approved,
/// rejected, returned or withdrawn. Seen only by its writer and the people it passed through (others: 404).
public class NotesheetEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var notes = app.MapGroup("/me/notesheets").WithTags("Self Service").RequireAuthorization().AllOffices();
    static async Task<Guid> Self(CurrentEmployee current, CancellationToken cancellationToken) => (await current.IdAsync(cancellationToken)).Value;

    notes.MapGet("/", async (string? box, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetNotesheetsQuery(await Self(current, ct), box))).ToOk())
      .WithName("GetMyNotesheets")
      .Produces<GetNotesheetsQueryResult>()
      .WithSummary("My Notesheets")
      .WithDescription("box = with-me (waiting for me) | mine (I wrote) | seen (passed through me); without it, all I can see. Newest first.");

    notes.MapGet("/people", async (string? search, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetNotesheetPeopleQuery(await Self(current, ct), search))).ToOk())
      .WithName("GetNotesheetPeople")
      .Produces<GetNotesheetPeopleQueryResult>()
      .WithSummary("Whom a Notesheet Can Go To")
      .WithDescription("People in service with a portal account (not me), by name or number.");

    notes.MapGet("/{id:guid}", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetNotesheetQuery(id, await Self(current, ct)))).ToOk())
      .WithName("GetMyNotesheet")
      .Produces<GetNotesheetQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("A Notesheet with Its Movements");

    notes.MapPost("/", async (NotesheetInput note, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new WriteNotesheetCommand(await Self(current, ct), note))).ToOk())
      .WithName("WriteNotesheet")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Write a Notesheet")
      .WithDescription("A draft, with me until I send it.");

    notes.MapPut("/{id:guid}", async (Guid id, NotesheetInput note, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new RewriteNotesheetCommand(id, await Self(current, ct), note))).ToOk())
      .WithName("RewriteNotesheet")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Change My Notesheet")
      .WithDescription("While it is a draft or back with me.");

    notes.MapPost("/{id:guid}/file", async (Guid id, [Microsoft.AspNetCore.Mvc.FromForm] FileUploadForm form, CurrentEmployee current, ISender sender, CancellationToken ct) =>
    {
      await using var content = Uploads.Open(form.File, out var file);
      return (await sender.Send(new AttachNotesheetFileCommand(id, await Self(current, ct), file))).ToOk();
    })
      .DisableAntiforgery()
      .Accepts<FileUploadForm>("multipart/form-data")
      .WithName("AttachNotesheetFile")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Attach a Paper to My Notesheet");

    notes.MapPost("/{id:guid}/steps", async (Guid id, [Microsoft.AspNetCore.Mvc.FromForm] NotesheetStepForm form, CurrentEmployee current, ISender sender, CancellationToken ct) =>
    {
      var step = QueryParsing.ParseEnum<NotesheetStep>(form.Step, "step") ?? throw new DomainException("Say what to do with it.");
      if (form.File is null)
        return (await sender.Send(new MoveNotesheetCommand(id, await Self(current, ct), step, form.ToEmployeeId, form.Remarks))).ToOk();
      await using var content = Uploads.Open(form.File, out var file);
      return (await sender.Send(new MoveNotesheetCommand(id, await Self(current, ct), step, form.ToEmployeeId, form.Remarks, file))).ToOk();
    })
      .DisableAntiforgery()
      .Accepts<NotesheetStepForm>("multipart/form-data")
      .WithName("MoveNotesheet")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Move a Notesheet")
      .WithDescription("step = send (the writer; to someone) | forward (whoever holds it; to someone) | return (to its writer; remarks needed) | "
        + "approve | reject (remarks needed; never one's own) | withdraw (the writer). A remark and a paper go with the step.");

    notes.MapGet("/{id:guid}/file", async (Guid id, bool? download, CurrentEmployee current, HttpResponse response, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetNotesheetFileQuery(id, null, await Self(current, ct), download ?? true))).ToFile(response))
      .WithName("GetNotesheetFile")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("The Paper of a Notesheet");

    notes.MapGet("/{id:guid}/movements/{movementId:guid}/file", async (Guid id, Guid movementId, bool? download, CurrentEmployee current, HttpResponse response, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetNotesheetFileQuery(id, movementId, await Self(current, ct), download ?? true))).ToFile(response))
      .WithName("GetNotesheetMovementFile")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("The Paper of a Step");
  }
}
