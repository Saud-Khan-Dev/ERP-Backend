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

public sealed record TaskUpdateDto(Guid Id, int ProgressPercentage, string? Notes, Guid AuthorId, string? AuthorName, DateTime? CreatedAt);

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
  string? Remarks);

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
  Guid? OnlyForEmployee = null) : IQuery<Result<GetTasksQueryResult>>;

public sealed record GetTaskQueryResult(TaskDto Task);
public sealed record GetTaskQuery(Guid Id, Guid? OnlyForEmployee = null) : IQuery<Result<GetTaskQueryResult>>;

public sealed record AssignTaskCommand(Guid EmployeeId, string Title, string? Description, TaskPriority Priority, DateOnly? DueDate) : ICommand<Result<CreatedResult>>;
public sealed record EditTaskCommand(Guid Id, string Title, string? Description, TaskPriority Priority, DateOnly? DueDate) : ICommand<Result<UpdatedResult>>;

/// A progress note; 100% completes the task. The employee records progress on their own tasks through self-service.
public sealed record RecordTaskProgressCommand(Guid Id, int ProgressPercentage, string? Notes, Guid? OnlyForEmployee = null) : ICommand<Result<CreatedResult>>;

public enum TaskAction
{
  Hold,
  Resume,
  Cancel
}

public sealed record ChangeTaskStatusCommand(Guid Id, TaskAction Action) : ICommand<Result<UpdatedResult>>;

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
  Guid? OnlyForEmployee = null) : IQuery<Result<GetEmployeeRequestsQueryResult>>;

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

public class TaskRequestHandlers(IApplicationDbContext context, HrLookup lookup, ICurrentUser currentUser, IClock clock) :
  IQueryHandler<GetTasksQuery, Result<GetTasksQueryResult>>,
  IQueryHandler<GetTaskQuery, Result<GetTaskQueryResult>>,
  ICommandHandler<AssignTaskCommand, Result<CreatedResult>>,
  ICommandHandler<EditTaskCommand, Result<UpdatedResult>>,
  ICommandHandler<RecordTaskProgressCommand, Result<CreatedResult>>,
  ICommandHandler<ChangeTaskStatusCommand, Result<UpdatedResult>>,
  IQueryHandler<GetEmployeeRequestsQuery, Result<GetEmployeeRequestsQueryResult>>,
  IQueryHandler<GetEmployeeRequestQuery, Result<GetEmployeeRequestQueryResult>>,
  ICommandHandler<SubmitEmployeeRequestCommand, Result<CreatedResult>>,
  ICommandHandler<DecideEmployeeRequestCommand, Result<UpdatedResult>>,
  ICommandHandler<CancelEmployeeRequestCommand, Result<UpdatedResult>>
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

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderBy(t => t.Status == EmployeeTaskStatus.Completed || t.Status == EmployeeTaskStatus.Cancelled)
      .ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await TaskSummariesAsync(page, cancellationToken);
    return Result<GetTasksQueryResult>.Success(new(new PaginatedResult<TaskSummaryDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetTaskQueryResult>> Handle(GetTaskQuery query, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(query.Id, query.OnlyForEmployee, cancellationToken);
    var summary = (await TaskSummariesAsync([task], cancellationToken))[0];
    var authors = await UserNamesAsync(task.Updates.Select(u => u.AuthorId), cancellationToken);
    return Result<GetTaskQueryResult>.Success(new(new TaskDto(summary, task.Description, task.Updates
      .Select(u => new TaskUpdateDto(u.Id.Value, u.ProgressPercentage, u.Notes, u.AuthorId, authors.GetValueOrDefault(u.AuthorId), u.CreatedAt)).ToList())));
  }

  public async Task<Result<CreatedResult>> Handle(AssignTaskCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var task = EmployeeTask.Assign(EmployeeTaskId.New(), employee, currentUser.UserId, command.Title, command.Description, command.Priority, command.DueDate);
    context.Tasks.Add(task);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(task.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(EditTaskCommand command, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(command.Id, null, cancellationToken);
    task.Edit(command.Title, command.Description, command.Priority, command.DueDate);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(RecordTaskProgressCommand command, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(command.Id, command.OnlyForEmployee, cancellationToken);
    var update = task.RecordProgress(command.ProgressPercentage, command.Notes, currentUser.UserId, clock.UtcNow);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(update.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeTaskStatusCommand command, CancellationToken cancellationToken)
  {
    var task = await LoadTaskAsync(command.Id, null, cancellationToken);
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

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(r => r.SubmittedAt)
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
    if (command.Approve)
      request.Approve(currentUser.UserId, clock.UtcNow, command.Remarks);
    else
      request.Reject(currentUser.UserId, clock.UtcNow, command.Remarks);

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

  private async Task<EmployeeTask> LoadTaskAsync(Guid id, Guid? onlyFor, CancellationToken cancellationToken)
  {
    var task = await context.LoadTaskAsync(id, cancellationToken);
    if (onlyFor is { } own && task.EmployeeId.Value != own)
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
    return requests.Select(r =>
    {
      var person = people.GetValueOrDefault(r.EmployeeId.Value);
      JsonElement? data = r.RequestedData is { } json ? JsonDocument.Parse(json).RootElement.Clone() : null;
      return new EmployeeRequestDto(r.Id.Value, r.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", r.RequestTypeId.Value,
        types.GetValueOrDefault(r.RequestTypeId), r.Subject, r.Description, data, r.SupportingDocumentId?.Value, r.Status, r.SubmittedAt, r.ReviewedBy,
        r.ReviewedAt, r.Remarks);
    }).ToList();
  }
}
