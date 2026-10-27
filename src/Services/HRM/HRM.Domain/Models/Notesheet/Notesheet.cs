/// A notesheet: an official note an employee writes and sends up to a colleague, who forwards it with a remark, returns
/// it to the writer, or approves / rejects it. It is always with one person (or back with its writer, or decided); every
/// step is kept as a movement (who, to whom, when, the remark and a paper). Only the writer and the people it has
/// passed through see it.
public class Notesheet : Aggregate<NotesheetId>
{
  private readonly List<NotesheetMovement> _movements = new();

  /// NS-00001, NS-00002 … in the order they are written.
  public int Serial { get; private set; }
  public string Subject { get; private set; } = default!;
  public string Body { get; private set; } = default!;
  /// The writer.
  public EmployeeId WriterId { get; private set; } = default!;
  /// Who it is with now (null once decided or withdrawn; the writer while a draft or returned).
  public EmployeeId? HolderId { get; private set; }
  public NotesheetStatus Status { get; private set; }
  /// A paper written with the note (the writer's, while it is a draft or back with them).
  public string? FilePath { get; private set; }
  public string? FileName { get; private set; }
  public DateTime? DecidedAt { get; private set; }

  public IReadOnlyList<NotesheetMovement> Movements => _movements.OrderBy(m => m.At).ToList().AsReadOnly();

  public string Number => $"NS-{Serial:00000}";
  public bool IsClosed => Status is NotesheetStatus.Approved or NotesheetStatus.Rejected or NotesheetStatus.Withdrawn;

  public static Notesheet Write(NotesheetId id, int serial, Employee writer, string subject, string body)
  {
    ArgumentNullException.ThrowIfNull(writer);
    writer.EnsureInService();
    var note = new Notesheet { Id = id, Serial = serial, WriterId = writer.Id, HolderId = writer.Id, Status = NotesheetStatus.Draft };
    note.Rewrite(writer.Id, subject, body);
    return note;
  }

  /// The writer changes the note while it is with them (a draft, or returned to them).
  public void Rewrite(EmployeeId by, string subject, string body)
  {
    EnsureWriterHolds(by, "change the note");
    Subject = Guard.RequiredText(subject, 300, "Subject");
    Body = Guard.RequiredText(body, 8000, "Note");
  }

  public void Attach(EmployeeId by, string filePath, string fileName)
  {
    EnsureWriterHolds(by, "attach a paper");
    FilePath = Guard.RequiredText(filePath, 500, "File");
    FileName = Guard.RequiredText(fileName, 255, "File name");
  }

  /// The writer sends it (first time, or again after it was returned).
  public NotesheetMovement Send(EmployeeId by, Employee to, string? remarks, DateTime at, string? filePath = null, string? fileName = null)
  {
    EnsureWriterHolds(by, "send it");
    return Move(NotesheetAction.Sent, by, to, remarks, at, filePath, fileName);
  }

  public NotesheetMovement Forward(EmployeeId by, Employee to, string? remarks, DateTime at, string? filePath = null, string? fileName = null)
  {
    EnsureHolds(by);
    if (to.Id == by)
      throw new DomainException("Forward it to someone else.");
    return Move(NotesheetAction.Forwarded, by, to, remarks, at, filePath, fileName);
  }

  /// Back to the writer, with what to put right (a remark is needed).
  public NotesheetMovement Return(EmployeeId by, string? remarks, DateTime at, string? filePath = null, string? fileName = null)
  {
    EnsureHolds(by);
    if (by == WriterId)
      throw new DomainException("It is already with you.");
    var movement = Record(NotesheetAction.Returned, by, WriterId, RequiredRemark(remarks, "Say what is to be put right."), at, filePath, fileName);
    HolderId = WriterId;
    Status = NotesheetStatus.Returned;
    return movement;
  }

  public NotesheetMovement Decide(EmployeeId by, bool approve, string? remarks, DateTime at, string? filePath = null, string? fileName = null)
  {
    EnsureHolds(by);
    if (by == WriterId)
      throw new DomainException("Nobody decides their own notesheet; send it to the officer who decides.");
    var movement = Record(approve ? NotesheetAction.Approved : NotesheetAction.Rejected, by, null,
      approve ? Guard.Text(remarks, 2000, "Remarks") : RequiredRemark(remarks, "Say why it is not approved."), at, filePath, fileName);
    HolderId = null;
    Status = approve ? NotesheetStatus.Approved : NotesheetStatus.Rejected;
    DecidedAt = at;
    return movement;
  }

  /// The writer takes it back before it is decided.
  public NotesheetMovement Withdraw(EmployeeId by, string? remarks, DateTime at)
  {
    if (by != WriterId)
      throw new DomainException("Only its writer can withdraw a notesheet.");
    if (IsClosed)
      throw new DomainException($"The notesheet is already {EnumText.Words(Status)}.");
    var movement = Record(NotesheetAction.Withdrawn, by, null, Guard.Text(remarks, 2000, "Remarks"), at, null, null);
    HolderId = null;
    Status = NotesheetStatus.Withdrawn;
    return movement;
  }

  /// The writer, the one holding it, or anyone it passed through.
  public bool IsSeenBy(EmployeeId employee) =>
      WriterId == employee || HolderId == employee || _movements.Any(m => m.FromId == employee || m.ToId == employee);

  private NotesheetMovement Move(NotesheetAction action, EmployeeId by, Employee to, string? remarks, DateTime at, string? filePath, string? fileName)
  {
    ArgumentNullException.ThrowIfNull(to);
    to.EnsureInService();
    if (to.Id == WriterId && action == NotesheetAction.Forwarded)
      throw new DomainException("To send it back to its writer, return it.");
    var movement = Record(action, by, to.Id, Guard.Text(remarks, 2000, "Remarks"), at, filePath, fileName);
    HolderId = to.Id;
    Status = NotesheetStatus.InProgress;
    return movement;
  }

  private NotesheetMovement Record(NotesheetAction action, EmployeeId from, EmployeeId? to, string? remarks, DateTime at, string? filePath, string? fileName)
  {
    var movement = NotesheetMovement.Create(Id, action, from, to, remarks, at, filePath, fileName);
    _movements.Add(movement);
    return movement;
  }

  private void EnsureWriterHolds(EmployeeId by, string what)
  {
    if (by != WriterId)
      throw new DomainException($"Only its writer can {what}.");
    if (Status is not (NotesheetStatus.Draft or NotesheetStatus.Returned))
      throw new DomainException(IsClosed ? $"The notesheet is {EnumText.Words(Status)}." : "It is with someone else now; it can be changed when it comes back to you.");
  }

  private void EnsureHolds(EmployeeId by)
  {
    if (IsClosed)
      throw new DomainException($"The notesheet is {EnumText.Words(Status)}.");
    if (Status != NotesheetStatus.InProgress || HolderId != by)
      throw new DomainException("The notesheet is not with you.");
  }

  private static string RequiredRemark(string? remarks, string message) => Guard.Text(remarks, 2000, "Remarks") ?? throw new DomainException(message);
}

/// One step of a notesheet (append-only): sent, forwarded, returned, approved, rejected or withdrawn - by whom, to whom,
/// when, with the remark and a paper.
public class NotesheetMovement : Entity<NotesheetMovementId>
{
  public NotesheetId NotesheetId { get; private set; } = default!;
  public NotesheetAction Action { get; private set; }
  public EmployeeId FromId { get; private set; } = default!;
  public EmployeeId? ToId { get; private set; }
  public string? Remarks { get; private set; }
  public DateTime At { get; private set; }
  public string? FilePath { get; private set; }
  public string? FileName { get; private set; }

  internal static NotesheetMovement Create(NotesheetId notesheetId, NotesheetAction action, EmployeeId from, EmployeeId? to, string? remarks, DateTime at,
      string? filePath, string? fileName)
  {
    if ((filePath is null) != (fileName is null))
      throw new DomainException("A file needs its name.");
    return new()
    {
      Id = NotesheetMovementId.New(), NotesheetId = notesheetId, Action = action, FromId = from, ToId = to, Remarks = remarks, At = at,
      FilePath = Guard.Text(filePath, 500, "File"), FileName = Guard.Text(fileName, 255, "File name")
    };
  }
}
