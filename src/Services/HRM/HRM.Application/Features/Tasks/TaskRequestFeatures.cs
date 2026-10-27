using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record TaskSummaryDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  Guid AssignedBy,
  string? AssignedByName,
  string Title,
  TaskPriority Priority,
  DateOnly? DueDate,
  EmployeeTaskStatus Status,
  int ProgressPercentage,
  DateTime? CompletedAt,
  bool IsOverdue,
  DateTime? CreatedAt);

public sealed record TaskUpdateDto(Guid Id, int ProgressPercentage, string? Notes, Guid AuthorId, string? AuthorName, DateTime? CreatedAt, string? FileName = null);

/// One person in a supervisor's team, with their open and overdue tasks.
public sealed record TeamMemberDto(Guid EmployeeId, string EmployeeNumber, string FullName, string? Designation, string? PostCode, string? OrgUnit, int OpenTasks, int OverdueTasks);
public sealed record GetMyTeamQueryResult(IReadOnlyList<TeamMemberDto> Members);

public sealed record TaskDto(TaskSummaryDto Task, string? Description, IReadOnlyList<TaskUpdateDto> Updates);

public sealed record EmployeeRequestDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  Guid RequestTypeId,
  string? RequestType,
  string? Subject,
  string? Description,
  JsonElement? RequestedData,
  Guid? SupportingDocumentId,
  EmployeeRequestStatus Status,
  DateTime SubmittedAt,
  Guid? ReviewedBy,
  DateTime? ReviewedAt,
  string? Remarks,
  /// The paper sent with the request: its kind and number, to open it from the request.
  string? SupportingDocument = null);

// ---- tasks ----

public sealed record GetTasksQueryResult(PaginatedResult<TaskSummaryDto> Tasks);

/// OnlyForEmployee: self-service sees only its own tasks.
public sealed record GetTasksQuery(
  PaginationRequest Pagination,
  Guid? EmployeeId,
  EmployeeTaskStatus? Status,
  TaskPriority? Priority,
  bool OverdueOnly,
  bool OpenOnly,
  Guid? OnlyForEmployee = null,
  string? Search = null,
  bool AssignedByMe = false,
  DateOnly? DueFrom = null,
  DateOnly? DueTo = null,
  /// A supervisor in the portal: only their team's tasks (their employee id).
  Guid? ForSupervisor = null) : IQuery<Result<GetTasksQueryResult>>;

public sealed record GetTaskQueryResult(TaskDto Task);
public sealed record GetTaskQuery(Guid Id, Guid? OnlyForEmployee = null, Guid? ForSupervisor = null) : IQuery<Result<GetTaskQueryResult>>;
public sealed record GetMyTeamQuery(Guid Supervisor) : IQuery<Result<GetMyTeamQueryResult>>;
/// The file of a progress update: the employee's own task, a supervisor's team, or HR (neither set).
public sealed record GetTaskFileQuery(Guid TaskId, Guid UpdateId, Guid? OnlyForEmployee = null, Guid? ForSupervisor = null, bool Download = true) : IQuery<Result<StoredFile>>;

public sealed record AssignTaskCommand(Guid EmployeeId, string Title, string? Description, TaskPriority Priority, DateOnly? DueDate, Guid? ForSupervisor = null) : ICommand<Result<CreatedResult>>;
public sealed record EditTaskCommand(Guid Id, string Title, string? Description, TaskPriority Priority, DateOnly? DueDate, Guid? ForSupervisor = null) : ICommand<Result<UpdatedResult>>;
/// A task reported done is not finished: open again at the percentage given, with what is still to be done.
public sealed record SendBackTaskCommand(Guid Id, int ProgressPercentage, string Notes, Guid? ForSupervisor = null) : ICommand<Result<UpdatedResult>>;


/// A progress note; 100% completes the task. The employee records progress on their own tasks through self-service.
/// `File`: a site photo or the report itself, saved with the update.
public sealed record RecordTaskProgressCommand(Guid Id, int ProgressPercentage, string? Notes, Guid? OnlyForEmployee = null, UploadedFile? File = null) : ICommand<Result<CreatedResult>>;

public enum TaskAction
{
  Hold,
  Resume,
  Cancel
}

public sealed record ChangeTaskStatusCommand(Guid Id, TaskAction Action, Guid? ForSupervisor = null) : ICommand<Result<UpdatedResult>>;

public class AssignTaskCommandValidator : AbstractValidator<AssignTaskCommand>
{
  public AssignTaskCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    RuleFor(x => x.Description).MaximumLength(4000);
    RuleFor(x => x.Priority).IsInEnum();
  }
}

public class EditTaskCommandValidator : AbstractValidator<EditTaskCommand>
{
  public EditTaskCommandValidator()
  {
    RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    RuleFor(x => x.Description).MaximumLength(4000);
    RuleFor(x => x.Priority).IsInEnum();
  }
}

public class SendBackTaskCommandValidator : AbstractValidator<SendBackTaskCommand>
{
  public SendBackTaskCommandValidator()
  {
    RuleFor(x => x.ProgressPercentage).InclusiveBetween(0, 99);
    RuleFor(x => x.Notes).NotEmpty().WithMessage("Say what is still to be done.").MaximumLength(4000);
  }
}

public class RecordTaskProgressCommandValidator : AbstractValidator<RecordTaskProgressCommand>
{
  public RecordTaskProgressCommandValidator()
  {
    RuleFor(x => x.ProgressPercentage).InclusiveBetween(0, 100);
    RuleFor(x => x.Notes).MaximumLength(4000);
  }
}

// ---- requests ----

public sealed record GetEmployeeRequestsQueryResult(PaginatedResult<EmployeeRequestDto> Requests);

public sealed record GetEmployeeRequestsQuery(
  PaginationRequest Pagination,
  Guid? EmployeeId,
  EmployeeRequestStatus? Status,
  Guid? RequestTypeId,
  Guid? OnlyForEmployee = null,
  string? Search = null,
  DateOnly? From = null,
  DateOnly? To = null,
  bool OldestFirst = false) : IQuery<Result<GetEmployeeRequestsQueryResult>>;

/// HR withdraws a request entered by mistake (kept as cancelled, with who, when and why).
public sealed record WithdrawEmployeeRequestCommand(Guid Id, string? Remarks) : ICommand<Result<UpdatedResult>>;

public sealed record GetEmployeeRequestQueryResult(EmployeeRequestDto Request);
public sealed record GetEmployeeRequestQuery(Guid Id, Guid? OnlyForEmployee = null) : IQuery<Result<GetEmployeeRequestQueryResult>>;

/// RequestedData: what is asked for, as a JSON object (e.g. the corrected fields of a profile correction).
public sealed record SubmitEmployeeRequestCommand(
  Guid EmployeeId,
  Guid RequestTypeId,
  string? Subject,
  string? Description,
  JsonElement? RequestedData,
  Guid? SupportingDocumentId) : ICommand<Result<CreatedResult>>;

public sealed record DecideEmployeeRequestCommand(Guid Id, bool Approve, string? Remarks) : ICommand<Result<UpdatedResult>>;
public sealed record CancelEmployeeRequestCommand(Guid Id, Guid? OnlyForEmployee = null) : ICommand<Result<UpdatedResult>>;

public class SubmitEmployeeRequestCommandValidator : AbstractValidator<SubmitEmployeeRequestCommand>
{
  public SubmitEmployeeRequestCommandValidator()
  {
    RuleFor(x => x.RequestTypeId).NotEmpty();
    RuleFor(x => x.Subject).MaximumLength(200);
    RuleFor(x => x.Description).MaximumLength(4000);
    RuleFor(x => x.RequestedData).Must(d => d is null || d.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Null)
      .WithMessage("requestedData must be a JSON object.");
  }
}

public class DecideEmployeeRequestCommandValidator : AbstractValidator<DecideEmployeeRequestCommand>
{
  public DecideEmployeeRequestCommandValidator() => RuleFor(x => x.Remarks).MaximumLength(4000);
}

public class TaskRequestHandlers(IApplicationDbContext context, HrLookup lookup, ICurrentUser currentUser, IClock clock, DutyGuard duty, IAuditTrail audit, MyTeam team, FileUploads files) :
  IQueryHandler<GetTasksQuery, Result<GetTasksQueryResult>>,
  IQueryHandler<GetTaskQuery, Result<GetTaskQueryResult>>,
  ICommandHandler<AssignTaskCommand, Result<CreatedResult>>,
  ICommandHandler<EditTaskCommand, Result<UpdatedResult>>,
  ICommandHandler<RecordTaskProgressCommand, Result<CreatedResult>>,
  ICommandHandler<ChangeTaskStatusCommand, Result<UpdatedResult>>,
  ICommandHandler<SendBackTaskCommand, Result<UpdatedResult>>,
  IQueryHandler<GetMyTeamQuery, Result<GetMyTeamQueryResult>>,
  IQueryHandler<GetTaskFileQuery, Result<StoredFile>>,
  IQueryHandler<GetEmployeeRequestsQuery, Result<GetEmployeeRequestsQueryResult>>,
  IQueryHandler<GetEmployeeRequestQuery, Result<GetEmployeeRequestQueryResult>>,
  ICommandHandler<SubmitEmployeeRequestCommand, Result<CreatedResult>>,
  ICommandHandler<DecideEmployeeRequestCommand, Result<UpdatedResult>>,
  ICommandHandler<CancelEmployeeRequestCommand, Result<UpdatedResult>>,
  ICommandHandler<WithdrawEmployeeRequestCommand, Result<UpdatedResult>>
{
  // ---- tasks ----

  public async Task<Result<GetTasksQueryResult>> Handle(GetTasksQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Tasks.AsNoTracking();
    if ((query.OnlyForEmployee ?? query.EmployeeId) is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      rows = rows.Where(t => t.EmployeeId == employeeId);
    }
    if (query.ForSupervisor is { } supervisor)
    {
      var members = await team.MembersAsync(EmployeeId.Of(supervisor), cancellationToken);
      rows = rows.Where(t => members.Contains(t.EmployeeId));
    }
    if (query.Status is { } status)
      rows = rows.Where(t => t.Status == status);
    if (query.Priority is { } priority)
      rows = rows.Where(t => t.Priority == priority);
    if (query.OpenOnly || query.OverdueOnly)
      rows = rows.Where(t => t.Status != EmployeeTaskStatus.Completed && t.Status != EmployeeTaskStatus.Cancelled);
    if (query.OverdueOnly)
    {
      var today = clock.Today;
      rows = rows.Where(t => t.DueDate != null && t.DueDate < today);
    }
    if (query.AssignedByMe && currentUser.UserId is { } me)
      rows = rows.Where(t => t.AssignedBy == me);
    if (query.DueFrom is { } dueFrom)
      rows = rows.Where(t => t.DueDate != null && t.DueDate >= dueFrom);
    if (query.DueTo is { } dueTo)
      rows = rows.Where(t => t.DueDate != null && t.DueDate <= dueTo);
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search);
      rows = rows.Where(t => EF.Functions.Like(t.Title.ToLower(), pattern, SearchPattern.Escape)
        || context.Employees.Any(e => e.Id == t.EmployeeId && (EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape)
          || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape))));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderBy(t => t.Status == EmployeeTaskStatus.Completed || t.Status == EmployeeTaskStatus.Cancelled)
      .ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await TaskSummariesAsync(page, cancellationToken);
    return Result<GetTasksQueryResult>.Success(new(new PaginatedResult<TaskSummaryDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetTaskQueryResult>> Handle(GetTaskQuery query, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(query.Id, query.OnlyForEmployee, cancellationToken, query.ForSupervisor);
    var summary = (await TaskSummariesAsync([task], cancellationToken))[0];
    var authors = await UserNamesAsync(task.Updates.Select(u => u.AuthorId), cancellationToken);
    return Result<GetTaskQueryResult>.Success(new(new TaskDto(summary, task.Description, task.Updates
      .Select(u => new TaskUpdateDto(u.Id.Value, u.ProgressPercentage, u.Notes, u.AuthorId, authors.GetValueOrDefault(u.AuthorId), u.CreatedAt, u.FileName)).ToList())));
  }

  public async Task<Result<CreatedResult>> Handle(AssignTaskCommand command, CancellationToken cancellationToken)
  {
    if (command.ForSupervisor is { } supervisor)
      await team.EnsureMemberAsync(EmployeeId.Of(supervisor), EmployeeId.Of(command.EmployeeId), cancellationToken);
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var task = EmployeeTask.Assign(EmployeeTaskId.New(), employee, currentUser.UserId, command.Title, command.Description, command.Priority, command.DueDate);
    context.Tasks.Add(task);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(task.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(EditTaskCommand command, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(command.Id, null, cancellationToken, command.ForSupervisor);
    task.Edit(command.Title, command.Description, command.Priority, command.DueDate);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(RecordTaskProgressCommand command, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(command.Id, command.OnlyForEmployee, cancellationToken);
    string? path = null;
    if (command.File is { } file)
    {
      var employee = await context.LoadEmployeeAsync(task.EmployeeId.Value, cancellationToken);
      path = await files.SaveDocumentAsync(file, employee.EmployeeNumber, "tasks", cancellationToken);
    }
    var update = task.RecordProgress(command.ProgressPercentage, command.Notes, currentUser.UserId, clock.UtcNow, path, command.File is null ? null : Path.GetFileName(command.File.FileName));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(update.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(SendBackTaskCommand command, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(command.Id, null, cancellationToken, command.ForSupervisor);
    task.SendBack(command.ProgressPercentage, command.Notes, currentUser.UserId, clock.UtcNow);
    audit.Record(HrmVerbs.SentBack, task, command.Notes.Trim());
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<StoredFile>> Handle(GetTaskFileQuery query, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(query.TaskId, query.OnlyForEmployee, cancellationToken, query.ForSupervisor);
    var update = task.Updates.FirstOrDefault(u => u.Id.Value == query.UpdateId);
    if (update?.FilePath is not { } path)
      throw new StoredFileNotFoundException("This update has no file.");
    audit.Record(query.Download ? AuditActions.Downloaded : AuditActions.Viewed, task, update.FileName);
    await audit.SaveAsync(cancellationToken);
    return Result<StoredFile>.Success(await files.OpenAsync(path, Path.GetFileNameWithoutExtension(update.FileName ?? "file"), cancellationToken));
  }

  public async Task<Result<GetMyTeamQueryResult>> Handle(GetMyTeamQuery query, CancellationToken cancellationToken)
  {
    var members = await team.MembersAsync(EmployeeId.Of(query.Supervisor), cancellationToken);
    var today = clock.Today;
    var people = await lookup.EmployeesAsync(members.Select(m => (EmployeeId?)m), cancellationToken);
    var placements = await lookup.PlacementsAsync(members, today, cancellationToken);
    var open = await context.Tasks.AsNoTracking()
      .Where(t => members.Contains(t.EmployeeId) && t.Status != EmployeeTaskStatus.Completed && t.Status != EmployeeTaskStatus.Cancelled)
      .Select(t => new { t.EmployeeId, t.DueDate }).ToListAsync(cancellationToken);
    var rows = members.Select(m =>
    {
      var person = people.GetValueOrDefault(m.Value);
      var seat = placements.GetValueOrDefault(m.Value);
      var mine = open.Where(t => t.EmployeeId == m).ToList();
      return new TeamMemberDto(m.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", seat?.Designation, seat?.PostCode, seat?.OrgUnit,
        mine.Count, mine.Count(t => t.DueDate < today));
    }).OrderBy(r => r.FullName).ToList();
    return Result<GetMyTeamQueryResult>.Success(new(rows));
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeTaskStatusCommand command, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(command.Id, null, cancellationToken, command.ForSupervisor);
    switch (command.Action)
    {
      case TaskAction.Hold: task.PutOnHold(); break;
      case TaskAction.Resume: task.Resume(); break;
      case TaskAction.Cancel: task.Cancel(); break;
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- requests ----

  public async Task<Result<GetEmployeeRequestsQueryResult>> Handle(GetEmployeeRequestsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Requests.AsNoTracking();
    if ((query.OnlyForEmployee ?? query.EmployeeId) is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      rows = rows.Where(r => r.EmployeeId == employeeId);
    }
    if (query.Status is { } status)
      rows = rows.Where(r => r.Status == status);
    if (query.RequestTypeId is { } type)
    {
      var typeId = EmployeeRequestTypeId.Of(type);
      rows = rows.Where(r => r.RequestTypeId == typeId);
    }
    if (query.From is { } from)
    {
      var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-5);
      rows = rows.Where(r => r.SubmittedAt >= start);
    }
    if (query.To is { } to)
    {
      var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-5);
      rows = rows.Where(r => r.SubmittedAt < end);
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search);
      rows = rows.Where(r => (r.Subject != null && EF.Functions.Like(r.Subject.ToLower(), pattern, SearchPattern.Escape))
        || (r.Description != null && EF.Functions.Like(r.Description.ToLower(), pattern, SearchPattern.Escape))
        || context.Employees.Any(e => e.Id == r.EmployeeId && (EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape)
          || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape))));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await (query.OldestFirst ? rows.OrderBy(r => r.SubmittedAt) : rows.OrderByDescending(r => r.SubmittedAt))
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await RequestsAsync(page, cancellationToken);
    return Result<GetEmployeeRequestsQueryResult>.Success(new(new PaginatedResult<EmployeeRequestDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetEmployeeRequestQueryResult>> Handle(GetEmployeeRequestQuery query, CancellationToken cancellationToken)
  {
    var request = await LoadRequestAsync(query.Id, query.OnlyForEmployee, cancellationToken);
    return Result<GetEmployeeRequestQueryResult>.Success(new((await RequestsAsync([request], cancellationToken))[0]));
  }

  public async Task<Result<CreatedResult>> Handle(SubmitEmployeeRequestCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var typeId = EmployeeRequestTypeId.Of(command.RequestTypeId);
    var type = await context.RequestTypes.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
      ?? throw new RequestTypeNotFoundException($"Request type {command.RequestTypeId} was not found.");
    var document = command.SupportingDocumentId is { } documentId ? await context.LoadDocumentAsync(documentId, cancellationToken) : null;
    var data = command.RequestedData is { ValueKind: JsonValueKind.Object } json ? json.GetRawText() : null;

    var request = EmployeeRequest.Submit(EmployeeRequestId.New(), employee, type, command.Subject, command.Description, data, document, clock.UtcNow);
    context.Requests.Add(request);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(request.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(DecideEmployeeRequestCommand command, CancellationToken cancellationToken)
  {
    var request = await LoadRequestAsync(command.Id, null, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(request.EmployeeId, "decide a request", request, cancellationToken);
    if (command.Approve)
      request.Approve(currentUser.UserId, clock.UtcNow, command.Remarks);
    else
      request.Reject(currentUser.UserId, clock.UtcNow, command.Remarks);
    audit.Record(command.Approve ? HrmVerbs.Approved : HrmVerbs.Rejected, request, string.IsNullOrWhiteSpace(command.Remarks) ? null : $"Remarks: {command.Remarks.Trim()}");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(WithdrawEmployeeRequestCommand command, CancellationToken cancellationToken)
  {
    var request = await LoadRequestAsync(command.Id, null, cancellationToken);
    request.Withdraw(currentUser.UserId, clock.UtcNow, command.Remarks);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(CancelEmployeeRequestCommand command, CancellationToken cancellationToken)
  {
    var request = await LoadRequestAsync(command.Id, command.OnlyForEmployee, cancellationToken);
    request.Cancel();
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// `onlyFor`: the employee's own task; `supervisor`: a task of their team. Anything else reads as not found.
  private async Task<EmployeeTask> LoadTaskAsync(Guid id, Guid? onlyFor, CancellationToken cancellationToken, Guid? supervisor = null)
  {
    var task = await context.LoadTaskAsync(id, cancellationToken);
    if (onlyFor is { } own && task.EmployeeId.Value != own)
      throw new TaskNotFoundException($"Task {id} was not found.");
    if (supervisor is { } boss && !(await team.MembersAsync(EmployeeId.Of(boss), cancellationToken)).Contains(task.EmployeeId))
      throw new TaskNotFoundException($"Task {id} was not found.");
    return task;
  }

  private async Task<EmployeeRequest> LoadRequestAsync(Guid id, Guid? onlyFor, CancellationToken cancellationToken)
  {
    var request = await context.LoadRequestAsync(id, cancellationToken);
    if (onlyFor is { } own && request.EmployeeId.Value != own)
      throw new EmployeeRequestNotFoundException($"Request {id} was not found.");
    return request;
  }

  /// Names of Identity users who are also employees (whoever assigned a task or wrote an update).
  private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
  {
    var ids = userIds.Distinct().Select(id => (Guid?)id).ToList();
    return await context.Employees.AsNoTracking().Where(e => e.UserId != null && ids.Contains(e.UserId))
      .ToDictionaryAsync(e => e.UserId!.Value, e => e.FullName ?? e.EmployeeNumber, cancellationToken);
  }

  private async Task<List<TaskSummaryDto>> TaskSummariesAsync(List<EmployeeTask> tasks, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(tasks.Select(t => (EmployeeId?)t.EmployeeId), cancellationToken);
    var assigners = await UserNamesAsync(tasks.Select(t => t.AssignedBy), cancellationToken);
    var today = clock.Today;
    return tasks.Select(t =>
    {
      var person = people.GetValueOrDefault(t.EmployeeId.Value);
      return new TaskSummaryDto(t.Id.Value, t.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", t.AssignedBy, assigners.GetValueOrDefault(t.AssignedBy),
        t.Title, t.Priority, t.DueDate, t.Status, t.ProgressPercentage, t.CompletedAt, !t.IsClosed && t.DueDate < today, t.CreatedAt);
    }).ToList();
  }

  private async Task<List<EmployeeRequestDto>> RequestsAsync(List<EmployeeRequest> requests, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(requests.Select(r => (EmployeeId?)r.EmployeeId), cancellationToken);
    var types = await context.RequestTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
    var documentIds = requests.Where(r => r.SupportingDocumentId is not null).Select(r => r.SupportingDocumentId!).ToList();
    var documents = await (
        from d in context.Documents.AsNoTracking()
        where documentIds.Contains(d.Id)
        join t in context.DocumentTypes on d.DocumentTypeId equals t.Id
        select new { d.Id, t.Name, d.DocumentNumber })
      .ToDictionaryAsync(d => d.Id, d => d.DocumentNumber is null ? d.Name : $"{d.Name} {d.DocumentNumber}", cancellationToken);
    return requests.Select(r =>
    {
      var person = people.GetValueOrDefault(r.EmployeeId.Value);
      JsonElement? data = r.RequestedData is { } json ? JsonDocument.Parse(json).RootElement.Clone() : null;
      return new EmployeeRequestDto(r.Id.Value, r.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", r.RequestTypeId.Value,
        types.GetValueOrDefault(r.RequestTypeId), r.Subject, r.Description, data, r.SupportingDocumentId?.Value, r.Status, r.SubmittedAt, r.ReviewedBy,
        r.ReviewedAt, r.Remarks, r.SupportingDocumentId is null ? null : documents.GetValueOrDefault(r.SupportingDocumentId));
    }).ToList();
  }
}
