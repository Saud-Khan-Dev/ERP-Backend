public sealed record AssignTaskRequest(Guid EmployeeId, string Title, string? Description, TaskPriority? Priority, DateOnly? DueDate);
public sealed record EditTaskRequest(string Title, string? Description, TaskPriority Priority, DateOnly? DueDate);
public sealed record DecideRequestBody(string? Remarks);

/// Tasks given to employees (they report progress through /me) and the requests employees raise (decided by HR).
public class TaskRequestEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- tasks ----

    var tasks = app.MapGroup("/tasks").WithTags("Tasks");

    tasks.MapGet("/", async (int? pageIndex, int? pageSize, Guid? employeeId, string? status, string? priority, bool? overdueOnly, bool? openOnly, ISender sender) =>
        (await sender.Send(new GetTasksQuery(QueryParsing.Page(pageIndex, pageSize), employeeId, QueryParsing.ParseEnum<EmployeeTaskStatus>(status, "status"),
          QueryParsing.ParseEnum<TaskPriority>(priority, "priority"), overdueOnly ?? false, openOnly ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetTasks")
      .Produces<GetTasksQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Tasks")
      .WithDescription("Open tasks first, by due date. status = pending | in_progress | completed | on_hold | cancelled; priority = low | medium | high | urgent.");

    tasks.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetTaskQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetTask")
      .Produces<GetTaskQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Task")
      .WithDescription("With its progress updates, oldest first.");

    tasks.MapPost("/", async (AssignTaskRequest r, ISender sender) =>
        (await sender.Send(new AssignTaskCommand(r.EmployeeId, r.Title, r.Description, r.Priority ?? TaskPriority.Medium, r.DueDate))).ToCreated(c => $"/tasks/{c.Id}"))
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithName("AssignTask")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Assign Task")
      .WithDescription("To an employee in service; priority defaults to medium.");

    tasks.MapPut("/{id:guid}", async (Guid id, EditTaskRequest r, ISender sender) =>
        (await sender.Send(new EditTaskCommand(id, r.Title, r.Description, r.Priority, r.DueDate))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("EditTask")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Edit Task");

    tasks.MapPost("/{id:guid}/progress", async (Guid id, ProgressRequest r, ISender sender) =>
        (await sender.Send(new RecordTaskProgressCommand(id, r.ProgressPercentage, r.Notes))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("RecordTaskProgress")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Progress")
      .WithDescription("100% completes the task.");

    foreach (var (action, route, summary) in new[] { (TaskAction.Hold, "hold", "Put Task on Hold"), (TaskAction.Resume, "resume", "Resume Task"), (TaskAction.Cancel, "cancel", "Cancel Task") })
    {
      tasks.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new ChangeTaskStatusCommand(id, action))).ToOk())
        .RequirePermission(PermissionCatalog.Hr.Edit)
        .WithName($"{action}Task")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(summary);
    }

    // ---- requests ----

    var requests = app.MapGroup("/employee-requests").WithTags("Employee Requests");

    requests.MapGet("/", async (int? pageIndex, int? pageSize, Guid? employeeId, string? status, Guid? requestTypeId, ISender sender) =>
        (await sender.Send(new GetEmployeeRequestsQuery(QueryParsing.Page(pageIndex, pageSize), employeeId,
          QueryParsing.ParseEnum<EmployeeRequestStatus>(status, "status"), requestTypeId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetEmployeeRequests")
      .Produces<GetEmployeeRequestsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Employee Requests")
      .WithDescription("Newest first. status = pending | approved | rejected | cancelled.");

    requests.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetEmployeeRequestQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetEmployeeRequest")
      .Produces<GetEmployeeRequestQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Employee Request");

    app.MapPost("/employees/{id:guid}/requests", async (Guid id, MyRequestInput r, ISender sender) =>
        (await sender.Send(new SubmitEmployeeRequestCommand(id, r.RequestTypeId, r.Subject, r.Description, r.RequestedData, r.SupportingDocumentId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithTags("Employee Requests")
      .WithName("SubmitEmployeeRequest")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Raise Request for Employee")
      .WithDescription("HR records a request handed in on paper. Employees raise their own through /me/requests.");

    foreach (var (approve, route, summary) in new[] { (true, "approve", "Approve Request"), (false, "reject", "Reject Request") })
    {
      requests.MapPost($"/{{id:guid}}/{route}", async (Guid id, DecideRequestBody? r, ISender sender) =>
          (await sender.Send(new DecideEmployeeRequestCommand(id, approve, r?.Remarks))).ToOk())
        .RequirePermission(PermissionCatalog.Hr.Approve)
        .WithName(approve ? "ApproveEmployeeRequest" : "RejectEmployeeRequest")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(summary)
        .WithDescription(approve ? "Records the decision; the change asked for (e.g. a profile correction) is made in its own screen." : "Remarks (the reason) are required.");
    }
  }
}
