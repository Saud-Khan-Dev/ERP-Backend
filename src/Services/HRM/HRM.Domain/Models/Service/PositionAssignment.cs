/// An employee holding a sanctioned post for a period. Regular assignments use a seat (at most sanctioned_count at a
/// time, and an employee holds one regular post at a time); acting, additional-charge and look-after assignments can
/// sit beside a regular one. A wrongly recorded assignment is cancelled (status inactive), never deleted.
public class PositionAssignment : Aggregate<PositionAssignmentId>
{
  public PostId PostId { get; private set; } = default!;
  public EmployeeId EmployeeId { get; private set; } = default!;
  public AssignmentType AssignmentType { get; private set; }
  public ServiceHistoryId? ServiceHistoryId { get; private set; }
  public string? OrderNumber { get; private set; }
  public EmployeeDocumentId? OrderDocumentId { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public RecordStatus Status { get; private set; }
  public string? Remarks { get; private set; }

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  public bool IsLive => Status == RecordStatus.Active;

  public bool TakesSeat => IsLive && AssignmentType == AssignmentType.Regular;

  /// `postAssignments` = the live assignments of the post; `employeeAssignments` = the live assignments of the employee.
  public static PositionAssignment Create(
      PositionAssignmentId id,
      Post post,
      Employee employee,
      AssignmentType type,
      DateOnly effectiveFrom,
      DateOnly? effectiveTo,
      ServiceHistoryId? serviceHistoryId,
      string? orderNumber,
      EmployeeDocumentId? orderDocumentId,
      string? remarks,
      IReadOnlyCollection<PositionAssignment> postAssignments,
      IReadOnlyCollection<PositionAssignment> employeeAssignments)
  {
    ArgumentNullException.ThrowIfNull(post);
    ArgumentNullException.ThrowIfNull(employee);
    employee.EnsureInService();
    DateRange.EnsureValid(effectiveFrom, effectiveTo);

    var range = new DateRange(effectiveFrom, effectiveTo);

    var version = post.VersionOn(effectiveFrom)
      ?? throw new DomainException($"Post {post.PostCode} does not exist on {effectiveFrom:yyyy-MM-dd}.");

    if (version.LifecycleStatus == PostLifecycle.Abolished)
      throw new DomainException($"Post {post.PostCode} is abolished on {effectiveFrom:yyyy-MM-dd}.");

    if (employeeAssignments.Any(a => a.IsLive && a.PostId == post.Id && a.Range.Overlaps(range)))
      throw new DomainException($"{employee.DisplayName} already holds post {post.PostCode} in that period.");

    if (type == AssignmentType.Regular)
    {
      version.EnsureAcceptsRegularAppointment(post.PostCode);
      PostCapacity.EnsureSeatFree(post.PostCode, version.SanctionedCount, postAssignments, range, self: null);

      if (employeeAssignments.FirstOrDefault(a => a.TakesSeat && a.Range.Overlaps(range)) is { } held)
        throw new DomainException($"{employee.DisplayName} already holds a regular post from {held.EffectiveFrom:yyyy-MM-dd}. End it before giving another.");
    }

    return new PositionAssignment
    {
      Id = id,
      PostId = post.Id,
      EmployeeId = employee.Id,
      AssignmentType = type,
      ServiceHistoryId = serviceHistoryId,
      OrderNumber = Guard.Text(orderNumber, 100, "Order number"),
      OrderDocumentId = orderDocumentId,
      EffectiveFrom = effectiveFrom,
      EffectiveTo = effectiveTo,
      Status = RecordStatus.Active,
      Remarks = Guard.Text(remarks, 4000, "Remarks")
    };
  }

  /// The holder is relieved: the last day on the post.
  public void End(DateOnly lastDay)
  {
    EnsureLive();

    if (lastDay < EffectiveFrom)
      throw new DomainException($"The assignment started on {EffectiveFrom:yyyy-MM-dd}; it cannot end before that.");

    if (EffectiveTo is { } end && lastDay > end)
      throw new DomainException($"The assignment already ends on {end:yyyy-MM-dd}.");

    EffectiveTo = lastDay;
  }

  /// A wrongly recorded assignment: it no longer counts anywhere, but stays on record.
  public void Cancel(string? remarks)
  {
    EnsureLive();
    Status = RecordStatus.Inactive;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  public void LinkServiceHistory(ServiceHistoryId serviceHistoryId) => ServiceHistoryId = serviceHistoryId;

  private void EnsureLive()
  {
    if (!IsLive)
      throw new DomainException("This assignment was cancelled.");
  }
}

/// Seats of a post: regular holders overlapping a period may not exceed the sanctioned count. The database checks the
/// same at commit (a deferred trigger that locks the post), so two clerks cannot fill the last seat twice.
public static class PostCapacity
{
  public static int FilledOn(IEnumerable<PositionAssignment> postAssignments, DateOnly date) =>
      postAssignments.Count(a => a.TakesSeat && a.Range.Contains(date));

  public static void EnsureSeatFree(string postCode, int sanctionedCount, IEnumerable<PositionAssignment> postAssignments, DateRange range, PositionAssignmentId? self)
  {
    var taken = postAssignments.Count(a => a.TakesSeat && a.Id != self && a.Range.Overlaps(range));
    if (taken >= sanctionedCount)
      throw new DomainException($"Post {postCode} is full: {taken} of {sanctionedCount} seat(s) are already held in that period.");
  }
}
