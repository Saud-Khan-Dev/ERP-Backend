public sealed record TeamTaskRequest(Guid EmployeeId, string Title, string? Description, TaskPriority Priority, DateOnly? DueDate);
public sealed record TeamTaskEdit(string Title, string? Description, TaskPriority Priority, DateOnly? DueDate);
public sealed record SendBackRequest(int ProgressPercentage, string Notes);

/// A supervisor's part of the portal (/me/team): the people whose post reports to the supervisor's, the tasks given to
/// them, and giving / following / sending back tasks. Anyone outside the team reads as not found.
public class MyTeamEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var team = app.MapGroup("/me/team").WithTags("Self Service").RequireAuthorization().AllOffices();

    static async Task<Guid> Self(CurrentEmployee current, CancellationToken cancellationToken) => (await current.IdAsync(cancellationToken)).Value;

    team.MapGet("/", async (CurrentEmployee current, ISender sender, CancellationToken ct) => (await sender.Send(new GetMyTeamQuery(await Self(current, ct)))).ToOk())
      .WithName("GetMyTeam")
      .Produces<GetMyTeamQueryResult>()
      .WithSummary("My Team")
      .WithDescription("The people whose post reports to a post I hold today (regular, acting, additional charge, look after), with their open and overdue tasks.");

    team.MapGet("/tasks", async (int? pageIndex, int? pageSize, Guid? employeeId, string? status, bool? openOnly, bool? overdueOnly, string? search,
        CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetTasksQuery(QueryParsing.Page(pageIndex, pageSize), employeeId, QueryParsing.ParseEnum<EmployeeTaskStatus>(status, "status"), null,
          overdueOnly ?? false, openOnly ?? false, Search: search, ForSupervisor: await Self(current, ct)))).ToOk())
      .WithName("GetMyTeamTasks")
      .Produces<GetTasksQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("My Team's Tasks");

    team.MapGet("/tasks/{id:guid}", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetTaskQuery(id, null, await Self(current, ct)))).ToOk())
      .WithName("GetMyTeamTask")
      .Produces<GetTaskQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("A Task of My Team");

    team.MapPost("/tasks", async (TeamTaskRequest r, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new AssignTaskCommand(r.EmployeeId, r.Title, r.Description, r.Priority, r.DueDate, await Self(current, ct)))).ToOk())
      .WithName("GiveTaskToMyTeam")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Give a Task")
      .WithDescription("To someone in my team only.");

    team.MapPut("/tasks/{id:guid}", async (Guid id, TeamTaskEdit r, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new EditTaskCommand(id, r.Title, r.Description, r.Priority, r.DueDate, await Self(current, ct)))).ToOk())
      .WithName("EditMyTeamTask")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Edit a Task of My Team");

    foreach (var (action, route, summary) in new[] { (TaskAction.Hold, "hold", "Put on Hold"), (TaskAction.Resume, "resume", "Resume"), (TaskAction.Cancel, "cancel", "Cancel") })
    {
      team.MapPost($"/tasks/{{id:guid}}/{route}", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
          (await sender.Send(new ChangeTaskStatusCommand(id, action, await Self(current, ct)))).ToOk())
        .WithName($"{route}MyTeamTask")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(summary);
    }

    team.MapPost("/tasks/{id:guid}/send-back", async (Guid id, SendBackRequest r, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new SendBackTaskCommand(id, r.ProgressPercentage, r.Notes, await Self(current, ct)))).ToOk())
      .WithName("SendBackMyTeamTask")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Send Back a Task Reported Done")
      .WithDescription("It is open again at the percentage given (0-99), with what is still to be done as an update.");

    team.MapGet("/tasks/{id:guid}/updates/{updateId:guid}/file", async (Guid id, Guid updateId, bool? download, CurrentEmployee current, HttpResponse response, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetTaskFileQuery(id, updateId, null, await Self(current, ct), download ?? true))).ToFile(response))
      .WithName("GetMyTeamTaskFile")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("A File of My Team's Task");
  }
}
