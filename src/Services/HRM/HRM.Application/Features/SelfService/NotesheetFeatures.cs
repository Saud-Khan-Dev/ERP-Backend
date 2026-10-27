using FluentValidation;
using Microsoft.EntityFrameworkCore;

// Notesheets (Employee Portal): an employee writes a note and sends it to a colleague; it moves from one officer to the
// next until someone approves or rejects it, or it is returned to its writer. Only the writer and the people it passed
// through see it; anyone else reads it as not found.

public sealed record NotesheetInput(string Subject, string Body);

public sealed record NotesheetSummaryDto(
  Guid Id,
  string Number,
  string Subject,
  NotesheetStatus Status,
  Guid WriterId,
  string WriterName,
  Guid? HolderId,
  string? HolderName,
  /// When it reached the one holding it (or was decided).
  DateTime? Since,
  DateTime? WrittenAt,
  bool WithMe);

public sealed record NotesheetMovementDto(
  Guid Id,
  NotesheetAction Action,
  Guid FromId,
  string FromName,
  string? FromDesignation,
  Guid? ToId,
  string? ToName,
  string? Remarks,
  DateTime At,
  string? FileName);

public sealed record NotesheetDto(NotesheetSummaryDto Notesheet, string Body, string? FileName, IReadOnlyList<NotesheetMovementDto> Movements);

/// Someone a notesheet can go to: in service, with a portal account.
public sealed record NotesheetPersonDto(Guid EmployeeId, string EmployeeNumber, string FullName, string? Designation, string? OrgUnit);

public sealed record GetNotesheetsQueryResult(IReadOnlyList<NotesheetSummaryDto> Notesheets);
public sealed record GetNotesheetQueryResult(NotesheetDto Notesheet);
public sealed record GetNotesheetPeopleQueryResult(IReadOnlyList<NotesheetPersonDto> People);

/// box: with-me (waiting for me), mine (I wrote), seen (passed through me), or everything I can see (null).
public sealed record GetNotesheetsQuery(Guid Me, string? Box) : IQuery<Result<GetNotesheetsQueryResult>>;
public sealed record GetNotesheetQuery(Guid Id, Guid Me) : IQuery<Result<GetNotesheetQueryResult>>;
public sealed record GetNotesheetPeopleQuery(Guid Me, string? Search) : IQuery<Result<GetNotesheetPeopleQueryResult>>;
/// The note's own paper (movementId null) or a movement's.
public sealed record GetNotesheetFileQuery(Guid Id, Guid? MovementId, Guid Me, bool Download) : IQuery<Result<StoredFile>>;

public sealed record WriteNotesheetCommand(Guid Me, NotesheetInput Note) : ICommand<Result<CreatedResult>>;
public sealed record RewriteNotesheetCommand(Guid Id, Guid Me, NotesheetInput Note) : ICommand<Result<UpdatedResult>>;
public sealed record AttachNotesheetFileCommand(Guid Id, Guid Me, UploadedFile File) : ICommand<Result<UpdatedResult>>;

public enum NotesheetStep { Send, Forward, Return, Approve, Reject, Withdraw }

/// One step on a notesheet: send / forward (to someone), return (to its writer), approve, reject, withdraw - with a
/// remark and a paper.
public sealed record MoveNotesheetCommand(Guid Id, Guid Me, NotesheetStep Step, Guid? ToEmployeeId, string? Remarks, UploadedFile? File = null)
  : ICommand<Result<UpdatedResult>>;

public class NotesheetInputValidator : AbstractValidator<NotesheetInput>
{
  public NotesheetInputValidator()
  {
    RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
    RuleFor(x => x.Body).NotEmpty().WithMessage("Write the note.").MaximumLength(8000);
  }
}

public class WriteNotesheetCommandValidator : AbstractValidator<WriteNotesheetCommand>
{
  public WriteNotesheetCommandValidator() => RuleFor(x => x.Note).NotNull().SetValidator(new NotesheetInputValidator());
}

public class RewriteNotesheetCommandValidator : AbstractValidator<RewriteNotesheetCommand>
{
  public RewriteNotesheetCommandValidator() => RuleFor(x => x.Note).NotNull().SetValidator(new NotesheetInputValidator());
}

public class MoveNotesheetCommandValidator : AbstractValidator<MoveNotesheetCommand>
{
  public MoveNotesheetCommandValidator()
  {
    RuleFor(x => x.Remarks).MaximumLength(2000);
    RuleFor(x => x.ToEmployeeId).NotEmpty().When(x => x.Step is NotesheetStep.Send or NotesheetStep.Forward).WithMessage("Choose whom it goes to.");
  }
}

public class NotesheetHandlers(IApplicationDbContext context, FileUploads files, HrLookup lookup, IClock clock, EmployeeScope scope, IAuditTrail audit) :
  IQueryHandler<GetNotesheetsQuery, Result<GetNotesheetsQueryResult>>,
  IQueryHandler<GetNotesheetQuery, Result<GetNotesheetQueryResult>>,
  IQueryHandler<GetNotesheetPeopleQuery, Result<GetNotesheetPeopleQueryResult>>,
  IQueryHandler<GetNotesheetFileQuery, Result<StoredFile>>,
  ICommandHandler<WriteNotesheetCommand, Result<CreatedResult>>,
  ICommandHandler<RewriteNotesheetCommand, Result<UpdatedResult>>,
  ICommandHandler<AttachNotesheetFileCommand, Result<UpdatedResult>>,
  ICommandHandler<MoveNotesheetCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetNotesheetsQueryResult>> Handle(GetNotesheetsQuery query, CancellationToken cancellationToken)
  {
    var me = EmployeeId.Of(query.Me);
    using var all = scope.Unrestricted();
    var rows = context.Notesheets.AsNoTracking().Include(n => n.Movements).AsQueryable();
    rows = query.Box switch
    {
      "with-me" => rows.Where(n => n.HolderId == me && n.Status == NotesheetStatus.InProgress),
      "mine" => rows.Where(n => n.WriterId == me),
      "seen" => rows.Where(n => n.WriterId != me && n.Movements.Any(m => m.FromId == me || m.ToId == me)),
      _ => rows.Where(n => n.WriterId == me || n.HolderId == me || n.Movements.Any(m => m.FromId == me || m.ToId == me))
    };
    var list = await rows.OrderByDescending(n => n.Serial).Take(300).ToListAsync(cancellationToken);
    return Result<GetNotesheetsQueryResult>.Success(new(await SummariesAsync(list, me, cancellationToken)));
  }

  public async Task<Result<GetNotesheetQueryResult>> Handle(GetNotesheetQuery query, CancellationToken cancellationToken)
  {
    var me = EmployeeId.Of(query.Me);
    var note = await LoadAsync(query.Id, me, cancellationToken);
    var summary = (await SummariesAsync([note], me, cancellationToken))[0];
    var people = await PeopleAsync(note.Movements.SelectMany(m => new[] { m.FromId, m.ToId }), cancellationToken);
    var movements = note.Movements.Select(m =>
    {
      var from = people.GetValueOrDefault(m.FromId.Value);
      return new NotesheetMovementDto(m.Id.Value, m.Action, m.FromId.Value, from.Name ?? "", from.Designation, m.ToId?.Value,
        m.ToId is { } to ? people.GetValueOrDefault(to.Value).Name : null, m.Remarks, m.At, m.FileName);
    }).ToList();
    return Result<GetNotesheetQueryResult>.Success(new(new NotesheetDto(summary, note.Body, note.FileName, movements)));
  }

  public async Task<Result<GetNotesheetPeopleQueryResult>> Handle(GetNotesheetPeopleQuery query, CancellationToken cancellationToken)
  {
    var me = EmployeeId.Of(query.Me);
    var inService = EmploymentStatuses.InService;
    using var all = scope.Unrestricted();
    var rows = context.Employees.AsNoTracking()
      .Where(e => e.Id != me && e.UserId != null && e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus));
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search);
      rows = rows.Where(e => EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape) || EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape));
    }
    var people = await rows.OrderBy(e => e.FullName).Take(300).Select(e => new { e.Id, e.EmployeeNumber, e.FullName }).ToListAsync(cancellationToken);
    var seats = await lookup.PlacementsAsync(people.Select(p => p.Id), clock.Today, cancellationToken);
    return Result<GetNotesheetPeopleQueryResult>.Success(new(people.Select(p =>
    {
      var seat = seats.GetValueOrDefault(p.Id.Value);
      return new NotesheetPersonDto(p.Id.Value, p.EmployeeNumber, p.FullName ?? p.EmployeeNumber, seat?.Designation, seat?.OrgUnit);
    }).ToList()));
  }

  public async Task<Result<StoredFile>> Handle(GetNotesheetFileQuery query, CancellationToken cancellationToken)
  {
    var note = await LoadAsync(query.Id, EmployeeId.Of(query.Me), cancellationToken);
    var (path, name) = query.MovementId is { } movementId
      ? note.Movements.FirstOrDefault(m => m.Id.Value == movementId) is { } m ? (m.FilePath, m.FileName) : (null, null)
      : (note.FilePath, note.FileName);
    if (path is null)
      throw new StoredFileNotFoundException("There is no paper here.");
    audit.Record(query.Download ? AuditActions.Downloaded : AuditActions.Viewed, note, name);
    await audit.SaveAsync(cancellationToken);
    return Result<StoredFile>.Success(await files.OpenAsync(path, Path.GetFileNameWithoutExtension(name ?? "paper"), cancellationToken));
  }

  public async Task<Result<CreatedResult>> Handle(WriteNotesheetCommand command, CancellationToken cancellationToken)
  {
    var writer = await context.LoadEmployeeAsync(command.Me, cancellationToken);
    int serial;
    using (scope.Unrestricted())
      serial = (await context.Notesheets.MaxAsync(n => (int?)n.Serial, cancellationToken) ?? 0) + 1;
    var note = Notesheet.Write(NotesheetId.New(), serial, writer, command.Note.Subject, command.Note.Body);
    context.Notesheets.Add(note);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(note.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(RewriteNotesheetCommand command, CancellationToken cancellationToken)
  {
    var note = await LoadAsync(command.Id, EmployeeId.Of(command.Me), cancellationToken);
    note.Rewrite(EmployeeId.Of(command.Me), command.Note.Subject, command.Note.Body);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(AttachNotesheetFileCommand command, CancellationToken cancellationToken)
  {
    var me = EmployeeId.Of(command.Me);
    var note = await LoadAsync(command.Id, me, cancellationToken);
    var writer = await context.LoadEmployeeAsync(command.Me, cancellationToken);
    var path = await files.SaveDocumentAsync(command.File, writer.EmployeeNumber, "notesheets", cancellationToken);
    note.Attach(me, path, Path.GetFileName(command.File.FileName));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(MoveNotesheetCommand command, CancellationToken cancellationToken)
  {
    var me = EmployeeId.Of(command.Me);
    var note = await LoadAsync(command.Id, me, cancellationToken);
    Employee? to = null;
    if (command.Step is NotesheetStep.Send or NotesheetStep.Forward)
    {
      using (scope.Unrestricted())
        to = await context.LoadEmployeeAsync(command.ToEmployeeId!.Value, cancellationToken);
      if (to.UserId is null)
        return Result<UpdatedResult>.Failure($"{to.DisplayName} has no portal account, so they could not open it. Ask HR to link their account.");
    }

    string? path = null, name = null;
    if (command.File is { } file)
    {
      var mine = await context.LoadEmployeeAsync(command.Me, cancellationToken);
      path = await files.SaveDocumentAsync(file, mine.EmployeeNumber, "notesheets", cancellationToken);
      name = Path.GetFileName(file.FileName);
    }

    var now = clock.UtcNow;
    _ = command.Step switch
    {
      NotesheetStep.Send => note.Send(me, to!, command.Remarks, now, path, name),
      NotesheetStep.Forward => note.Forward(me, to!, command.Remarks, now, path, name),
      NotesheetStep.Return => note.Return(me, command.Remarks, now, path, name),
      NotesheetStep.Approve => note.Decide(me, true, command.Remarks, now, path, name),
      NotesheetStep.Reject => note.Decide(me, false, command.Remarks, now, path, name),
      _ => note.Withdraw(me, command.Remarks, now)
    };
    if (command.Step is NotesheetStep.Approve or NotesheetStep.Reject)
      audit.Record(command.Step == NotesheetStep.Approve ? HrmVerbs.Approved : HrmVerbs.Rejected, note, command.Remarks);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// The notesheet, if the person may see it (its writer, its holder, or someone it passed through); else not found.
  private async Task<Notesheet> LoadAsync(Guid id, EmployeeId me, CancellationToken cancellationToken)
  {
    var key = NotesheetId.Of(id);
    Notesheet? note;
    using (scope.Unrestricted())
      note = await context.Notesheets.Include(n => n.Movements).FirstOrDefaultAsync(n => n.Id == key, cancellationToken);
    if (note is null || !note.IsSeenBy(me))
      throw new NotesheetNotFoundException($"Notesheet {id} was not found.");
    return note;
  }

  private async Task<Dictionary<Guid, (string? Name, string? Designation)>> PeopleAsync(IEnumerable<EmployeeId?> ids, CancellationToken cancellationToken)
  {
    var list = ids.Where(i => i is not null).Select(i => i!).Distinct().ToList();
    using var all = scope.Unrestricted();
    var names = await lookup.EmployeesAsync(list.Select(i => (EmployeeId?)i), cancellationToken);
    var seats = await lookup.PlacementsAsync(list, clock.Today, cancellationToken);
    return list.ToDictionary(i => i.Value, i => (names.GetValueOrDefault(i.Value)?.FullName, seats.GetValueOrDefault(i.Value)?.Designation));
  }

  private async Task<List<NotesheetSummaryDto>> SummariesAsync(List<Notesheet> notes, EmployeeId me, CancellationToken cancellationToken)
  {
    var people = await PeopleAsync(notes.SelectMany(n => new[] { (EmployeeId?)n.WriterId, n.HolderId }), cancellationToken);
    return notes.Select(n => new NotesheetSummaryDto(n.Id.Value, n.Number, n.Subject, n.Status, n.WriterId.Value, people.GetValueOrDefault(n.WriterId.Value).Name ?? "",
      n.HolderId?.Value, n.HolderId is { } h ? people.GetValueOrDefault(h.Value).Name : null, n.Movements.LastOrDefault()?.At ?? n.CreatedAt, n.CreatedAt,
      n.HolderId == me && n.Status == NotesheetStatus.InProgress)).ToList();
  }
}
