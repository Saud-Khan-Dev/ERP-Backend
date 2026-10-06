/// A piece of work given to an employee (Employee Portal). Progress comes from the employee's updates: the latest
/// update's percentage is the task's, 100% completes it, and the first progress starts it.
public class EmployeeTask : Aggregate<EmployeeTaskId>
{
  private readonly List<EmployeeTaskUpdate> _updates = new();

  public EmployeeId EmployeeId { get; private set; } = default!;
  /// The Identity user who gave the task.
  public Guid AssignedBy { get; private set; }
  public string Title { get; private set; } = default!;
  public string? Description { get; private set; }
  public TaskPriority Priority { get; private set; }
  public DateOnly? DueDate { get; private set; }
  public EmployeeTaskStatus Status { get; private set; }
  public int ProgressPercentage { get; private set; }
  public DateTime? CompletedAt { get; private set; }

  public IReadOnlyList<EmployeeTaskUpdate> Updates => _updates.OrderBy(u => u.CreatedAt).ToList().AsReadOnly();

  public bool IsClosed => Status is EmployeeTaskStatus.Completed or EmployeeTaskStatus.Cancelled;

  public static EmployeeTask Assign(EmployeeTaskId id, Employee employee, Guid? assignedBy, string title, string? description, TaskPriority priority, DateOnly? dueDate)
  {
    ArgumentNullException.ThrowIfNull(employee);
    employee.EnsureInService();

    var task = new EmployeeTask
    {
      Id = id,
      EmployeeId = employee.Id,
      AssignedBy = Guard.Actor(assignedBy, "assign a task"),
      Status = EmployeeTaskStatus.Pending
    };
    task.Edit(title, description, priority, dueDate);
    return task;
  }

  public void Edit(string title, string? description, TaskPriority priority, DateOnly? dueDate)
  {
    EnsureOpen();
    Title = Guard.RequiredText(title, 200, "Title");
    Description = Guard.Text(description, 4000, "Description");
    Priority = priority;
    DueDate = dueDate;
  }

  /// Records progress (append-only) and mirrors it on the task.
  public EmployeeTaskUpdate RecordProgress(int progressPercentage, string? notes, Guid? updatedBy, DateTime at)
  {
    EnsureOpen();
    if (Status == EmployeeTaskStatus.OnHold)
      throw new DomainException("The task is on hold; resume it before recording progress.");

    var update = EmployeeTaskUpdate.Create(Id, Guard.Between(progressPercentage, 0, 100, "Progress"), notes, Guard.Actor(updatedBy, "update a task"), at);
    _updates.Add(update);

    ProgressPercentage = progressPercentage;
    if (progressPercentage == 100)
    {
      Status = EmployeeTaskStatus.Completed;
      CompletedAt ??= at;
    }
    else if (Status == EmployeeTaskStatus.Pending && progressPercentage > 0)
    {
      Status = EmployeeTaskStatus.InProgress;
    }

    return update;
  }

  public void PutOnHold()
  {
    EnsureOpen();
    if (Status == EmployeeTaskStatus.OnHold)
      throw new DomainException("The task is already on hold.");
    Status = EmployeeTaskStatus.OnHold;
  }

  public void Resume()
  {
    if (Status != EmployeeTaskStatus.OnHold)
      throw new DomainException("Only a task on hold can be resumed.");
    Status = ProgressPercentage > 0 ? EmployeeTaskStatus.InProgress : EmployeeTaskStatus.Pending;
  }

  public void Cancel()
  {
    EnsureOpen();
    Status = EmployeeTaskStatus.Cancelled;
  }

  private void EnsureOpen()
  {
    if (IsClosed)
      throw new DomainException($"The task is {EnumText.Words(Status)}.");
  }
}

/// An append-only progress note on a task. updated_by is the person who wrote it.
public class EmployeeTaskUpdate : Entity<TaskUpdateId>
{
  public EmployeeTaskId TaskId { get; private set; } = default!;
  public int ProgressPercentage { get; private set; }
  public string? Notes { get; private set; }
  /// Column updated_by: the author of the update (not an audit stamp).
  public Guid AuthorId { get; private set; }

  internal static EmployeeTaskUpdate Create(EmployeeTaskId taskId, int progressPercentage, string? notes, Guid authorId, DateTime at) => new()
  {
    Id = TaskUpdateId.New(),
    TaskId = taskId,
    ProgressPercentage = progressPercentage,
    Notes = Guard.Text(notes, 4000, "Notes"),
    AuthorId = authorId,
    CreatedAt = at
  };
}

/// Kinds of request an employee can raise (Transfer Request, Document Request, Profile Correction ...).
public class EmployeeRequestType : Aggregate<EmployeeRequestTypeId>
{
  public string Name { get; private set; } = default!;
  public string? Code { get; private set; }
  public bool RequiresDocument { get; private set; }
  public bool IsActive { get; private set; }

  public static EmployeeRequestType Create(EmployeeRequestTypeId id, string name, string? code, bool requiresDocument)
  {
    var type = new EmployeeRequestType { Id = id, IsActive = true };
    type.Update(name, code, requiresDocument);
    return type;
  }

  public void Update(string name, string? code, bool requiresDocument)
  {
    Name = Guard.RequiredText(name, 100, "Request type name");
    Code = Guard.OptionalCode(code, 30, "Request type code");
    RequiresDocument = requiresDocument;
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Request type '{Name}' is inactive.");
  }
}

/// A request an employee raises (Employee Portal): pending -> approved / rejected, or cancelled by the employee.
/// requested_data carries what is asked for as JSON (e.g. the corrected fields).
public class EmployeeRequest : Aggregate<EmployeeRequestId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public EmployeeRequestTypeId RequestTypeId { get; private set; } = default!;
  public string? Subject { get; private set; }
  public string? Description { get; private set; }
  public string? RequestedData { get; private set; }
  public EmployeeDocumentId? SupportingDocumentId { get; private set; }
  public EmployeeRequestStatus Status { get; private set; }
  public DateTime SubmittedAt { get; private set; }
  public Guid? ReviewedBy { get; private set; }
  public DateTime? ReviewedAt { get; private set; }
  public string? Remarks { get; private set; }

  /// `supportingDocument` must be one of the employee's own documents.
  public static EmployeeRequest Submit(
      EmployeeRequestId id,
      Employee employee,
      EmployeeRequestType type,
      string? subject,
      string? description,
      string? requestedDataJson,
      EmployeeDocument? supportingDocument,
      DateTime submittedAt)
  {
    ArgumentNullException.ThrowIfNull(employee);
    ArgumentNullException.ThrowIfNull(type);
    employee.EnsureProfileActive();
    type.EnsureActive();

    if (type.RequiresDocument && supportingDocument is null)
      throw new DomainException($"A {type.Name} needs a supporting document.");

    if (supportingDocument is not null && supportingDocument.EmployeeId != employee.Id)
      throw new DomainException("The supporting document belongs to a different employee.");

    if (string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(description))
      throw new DomainException("Say what the request is about (a subject or a description).");

    return new EmployeeRequest
    {
      Id = id,
      EmployeeId = employee.Id,
      RequestTypeId = type.Id,
      Subject = Guard.Text(subject, 200, "Subject"),
      Description = Guard.Text(description, 4000, "Description"),
      RequestedData = requestedDataJson,
      SupportingDocumentId = supportingDocument?.Id,
      Status = EmployeeRequestStatus.Pending,
      SubmittedAt = submittedAt
    };
  }

  public void Approve(Guid? reviewedBy, DateTime at, string? remarks) => Decide(EmployeeRequestStatus.Approved, reviewedBy, at, remarks);

  public void Reject(Guid? reviewedBy, DateTime at, string? remarks)
  {
    if (string.IsNullOrWhiteSpace(remarks))
      throw new DomainException("Give the reason for rejecting the request.");
    Decide(EmployeeRequestStatus.Rejected, reviewedBy, at, remarks);
  }

  /// The employee withdraws a request that has not been decided yet.
  public void Cancel()
  {
    EnsurePending();
    Status = EmployeeRequestStatus.Cancelled;
  }

  private void Decide(EmployeeRequestStatus status, Guid? reviewedBy, DateTime at, string? remarks)
  {
    EnsurePending();
    ReviewedBy = Guard.Actor(reviewedBy, "decide a request");
    ReviewedAt = at;
    Remarks = Guard.Text(remarks, 4000, "Remarks");
    Status = status;
  }

  private void EnsurePending()
  {
    if (Status != EmployeeRequestStatus.Pending)
      throw new DomainException($"The request is already {EnumText.Words(Status)}.");
  }
}
