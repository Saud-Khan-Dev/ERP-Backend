using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- service history ----

public sealed record GetServiceHistoryQueryResult(IReadOnlyList<ServiceHistoryDto> ServiceHistory);

public sealed record GetServiceHistoryQuery(Guid EmployeeId) : IQuery<Result<GetServiceHistoryQueryResult>>;

/// Records a past service event from the service book (back-filling, or a correction as an offsetting row). It writes
/// history only: the employee's current post, pay and status are changed through HR actions.
public sealed record RecordServiceEventCommand(
  Guid EmployeeId,
  Guid EventTypeId,
  DateOnly EffectiveDate,
  Guid? OldPostId,
  Guid? NewPostId,
  Guid? OldGradeId,
  Guid? NewGradeId,
  Guid? OldOrgUnitId,
  Guid? NewOrgUnitId,
  Guid? RecruitmentMethodId,
  string? ExternalReferenceOrg,
  string? OrderNumber,
  Guid? SupportingDocumentId,
  string? Reason,
  string? Remarks) : ICommand<Result<CreatedResult>>;

public class RecordServiceEventCommandValidator : AbstractValidator<RecordServiceEventCommand>
{
  public RecordServiceEventCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.EventTypeId).NotEmpty();
    RuleFor(x => x.ExternalReferenceOrg).MaximumLength(200);
    RuleFor(x => x.OrderNumber).MaximumLength(100);
    RuleFor(x => x.Reason).MaximumLength(4000);
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

// ---- assignments ----

public sealed record GetAssignmentsQueryResult(IReadOnlyList<AssignmentDto> Assignments);

public sealed record GetAssignmentsQuery(Guid EmployeeId, bool IncludeCancelled) : IQuery<Result<GetAssignmentsQueryResult>>;

/// An acting charge, additional charge or look-after arrangement (beside the employee's regular post). A regular
/// assignment here records an existing holder (data entry); new appointments and moves go through HR actions.
public sealed record CreateAssignmentCommand(
  Guid EmployeeId,
  Guid PostId,
  AssignmentType AssignmentType,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  string? OrderNumber,
  Guid? OrderDocumentId,
  string? Remarks) : ICommand<Result<CreatedResult>>;

public sealed record EndAssignmentCommand(Guid Id, DateOnly LastDay) : ICommand<Result<UpdatedResult>>;

public sealed record CancelAssignmentCommand(Guid Id, string? Remarks) : ICommand<Result<UpdatedResult>>;

public class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
  public CreateAssignmentCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.PostId).NotEmpty();
    RuleFor(x => x.AssignmentType).IsInEnum();
    RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue)
      .WithMessage("The assignment cannot end before it starts.");
    RuleFor(x => x.OrderNumber).MaximumLength(100);
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

public class ServiceRecordHandlers(IApplicationDbContext context, ServiceRecordReader reader, ServiceRecordWriter writer, HrLookup lookup, ICurrentUser currentUser) :
  IQueryHandler<GetServiceHistoryQuery, Result<GetServiceHistoryQueryResult>>,
  ICommandHandler<RecordServiceEventCommand, Result<CreatedResult>>,
  IQueryHandler<GetAssignmentsQuery, Result<GetAssignmentsQueryResult>>,
  ICommandHandler<CreateAssignmentCommand, Result<CreatedResult>>,
  ICommandHandler<EndAssignmentCommand, Result<UpdatedResult>>,
  ICommandHandler<CancelAssignmentCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetServiceHistoryQueryResult>> Handle(GetServiceHistoryQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");

    var rows = await context.ServiceHistory.AsNoTracking().Where(h => h.EmployeeId == employeeId)
      .OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.CreatedAt).ToListAsync(cancellationToken);
    return Result<GetServiceHistoryQueryResult>.Success(new(await ServiceHistoryMapper.MapAsync(context, lookup, rows, cancellationToken)));
  }

  public async Task<Result<CreatedResult>> Handle(RecordServiceEventCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var eventType = await context.LoadEventTypeAsync(command.EventTypeId, cancellationToken);
    var document = await SupportingDocumentAsync(employee.Id, command.SupportingDocumentId, cancellationToken);

    PostId? Post(Guid? id) => id is { } value ? PostId.Of(value) : null;
    PayScaleGradeId? Grade(Guid? id) => id is { } value ? PayScaleGradeId.Of(value) : null;
    OrganizationUnitId? Unit(Guid? id) => id is { } value ? OrganizationUnitId.Of(value) : null;

    var change = new ServiceChange(Post(command.OldPostId), Post(command.NewPostId), Grade(command.OldGradeId), Grade(command.NewGradeId),
      Unit(command.OldOrgUnitId), Unit(command.NewOrgUnitId));
    await EnsureExistAsync(change, cancellationToken);

    RecruitmentMethodId? method = command.RecruitmentMethodId is { } m ? RecruitmentMethodId.Of(m) : null;
    if (method is not null && !await context.RecruitmentMethods.AnyAsync(x => x.Id == method, cancellationToken))
      throw new RecruitmentMethodNotFoundException($"Recruitment method {command.RecruitmentMethodId} was not found.");

    var history = writer.RecordEvent(employee, eventType, command.EffectiveDate, change, method, command.ExternalReferenceOrg,
      command.OrderNumber, document, command.Reason, command.Remarks, currentUser.UserId);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(history.Id);
  }

  public async Task<Result<GetAssignmentsQueryResult>> Handle(GetAssignmentsQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");

    var rows = await context.PositionAssignments.AsNoTracking()
      .Where(a => a.EmployeeId == employeeId && (query.IncludeCancelled || a.Status == RecordStatus.Active))
      .OrderByDescending(a => a.EffectiveFrom).ToListAsync(cancellationToken);
    return Result<GetAssignmentsQueryResult>.Success(new(await AssignmentMapper.MapAsync(lookup, rows, cancellationToken)));
  }

  public async Task<Result<CreatedResult>> Handle(CreateAssignmentCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var post = await context.LoadPostAsync(command.PostId, cancellationToken);
    var document = await SupportingDocumentAsync(employee.Id, command.OrderDocumentId, cancellationToken);

    var assignment = PositionAssignment.Create(PositionAssignmentId.New(), post, employee, command.AssignmentType, command.EffectiveFrom,
      command.EffectiveTo, null, command.OrderNumber, document, command.Remarks,
      await reader.LiveAssignmentsOfPostAsync(post.Id, cancellationToken),
      await reader.LiveAssignmentsOfEmployeeAsync(employee.Id, cancellationToken));

    context.PositionAssignments.Add(assignment);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(assignment.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(EndAssignmentCommand command, CancellationToken cancellationToken)
  {
    var assignment = await context.LoadAssignmentAsync(command.Id, cancellationToken);
    assignment.End(command.LastDay);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(CancelAssignmentCommand command, CancellationToken cancellationToken)
  {
    var assignment = await context.LoadAssignmentAsync(command.Id, cancellationToken);
    if (await context.PayrollSegments.AnyAsync(s => s.PostId == assignment.PostId
        && context.PayrollTransactions.Any(t => t.Id == s.PayrollTransactionId && t.EmployeeId == assignment.EmployeeId), cancellationToken))
      return Result<UpdatedResult>.Failure("The employee has been paid on this post; end the assignment instead of cancelling it.");

    assignment.Cancel(command.Remarks);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// A supporting document must be one of the employee's own papers.
  private async Task<EmployeeDocumentId?> SupportingDocumentAsync(EmployeeId employeeId, Guid? documentId, CancellationToken cancellationToken)
  {
    if (documentId is not { } id)
      return null;

    var document = await context.LoadDocumentAsync(id, cancellationToken);
    if (document.EmployeeId != employeeId)
      throw new DomainException("The supporting document belongs to a different employee.");
    return document.Id;
  }

  private async Task EnsureExistAsync(ServiceChange change, CancellationToken cancellationToken)
  {
    var posts = new[] { change.OldPostId, change.NewPostId }.Where(p => p is not null).Select(p => p!).Distinct().ToList();
    if (posts.Count > 0 && await context.Posts.CountAsync(p => posts.Contains(p.Id), cancellationToken) != posts.Count)
      throw new PostNotFoundException("A post of the event was not found.");

    var grades = new[] { change.OldGradeId, change.NewGradeId }.Where(g => g is not null).Select(g => g!).Distinct().ToList();
    if (grades.Count > 0 && await context.PayScaleGrades.CountAsync(g => grades.Contains(g.Id), cancellationToken) != grades.Count)
      throw new PayScaleGradeNotFoundException("A grade of the event was not found.");

    var units = new[] { change.OldOrgUnitId, change.NewOrgUnitId }.Where(u => u is not null).Select(u => u!).Distinct().ToList();
    if (units.Count > 0 && await context.OrganizationUnits.CountAsync(u => units.Contains(u.Id), cancellationToken) != units.Count)
      throw new OrganizationUnitNotFoundException("An org unit of the event was not found.");
  }
}

/// Labels service-history rows (event names, post codes, BPS, unit names).
public static class ServiceHistoryMapper
{
  public static async Task<List<ServiceHistoryDto>> MapAsync(IApplicationDbContext context, HrLookup lookup, List<EmployeeServiceHistory> rows, CancellationToken cancellationToken)
  {
    var events = await context.ServiceEventTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);
    var posts = await lookup.PostCodesAsync(rows.SelectMany(r => new[] { r.OldPostId, r.NewPostId }), cancellationToken);
    var grades = await lookup.GradesAsync(cancellationToken);
    var units = await lookup.UnitNamesAsync(rows.SelectMany(r => new[] { r.OldOrgUnitId, r.NewOrgUnitId }), DateOnly.MaxValue, cancellationToken);
    var methodIds = rows.Where(r => r.RecruitmentMethodId is not null).Select(r => r.RecruitmentMethodId!).Distinct().ToList();
    var methods = await context.RecruitmentMethods.AsNoTracking().Where(m => methodIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

    string? Post(PostId? id) => id is null ? null : posts.GetValueOrDefault(id.Value);
    int? Bps(PayScaleGradeId? id) => id is null ? null : grades.TryGetValue(id.Value, out var g) ? g.BpsNumber : null;
    string? Unit(OrganizationUnitId? id) => id is null ? null : units.GetValueOrDefault(id.Value);

    return rows.Select(r => new ServiceHistoryDto(
      r.Id.Value, r.EmployeeId.Value, r.EventTypeId.Value, events.GetValueOrDefault(r.EventTypeId)?.Name, events.GetValueOrDefault(r.EventTypeId)?.Category,
      r.EffectiveDate, r.OldPostId?.Value, Post(r.OldPostId), r.NewPostId?.Value, Post(r.NewPostId), Bps(r.OldGradeId), Bps(r.NewGradeId),
      r.OldOrgUnitId?.Value, Unit(r.OldOrgUnitId), r.NewOrgUnitId?.Value, Unit(r.NewOrgUnitId), r.RecruitmentMethodId?.Value,
      r.RecruitmentMethodId is null ? null : methods.GetValueOrDefault(r.RecruitmentMethodId), r.ExternalReferenceOrg, r.OrderNumber,
      r.SupportingDocumentId?.Value, r.Reason, r.Remarks, r.ApprovedBy, r.CreatedAt)).ToList();
  }
}

/// Labels assignments with the post as it was when the assignment started.
public static class AssignmentMapper
{
  public static async Task<List<AssignmentDto>> MapAsync(HrLookup lookup, List<PositionAssignment> rows, CancellationToken cancellationToken)
  {
    var result = new List<AssignmentDto>(rows.Count);
    foreach (var group in rows.GroupBy(a => a.EffectiveFrom))
    {
      var posts = await lookup.PostsOnAsync(group.Select(a => a.PostId), group.Key, cancellationToken);
      foreach (var a in group)
      {
        var post = posts.GetValueOrDefault(a.PostId.Value);
        result.Add(new AssignmentDto(a.Id.Value, a.EmployeeId.Value, a.PostId.Value, post?.PostCode ?? "", post?.Designation, post?.Bps ?? 0,
          post?.OrgUnit, a.AssignmentType, a.EffectiveFrom, a.EffectiveTo, a.Status, a.OrderNumber, a.OrderDocumentId?.Value, a.ServiceHistoryId?.Value, a.Remarks));
      }
    }
    return result.OrderByDescending(a => a.EffectiveFrom).ToList();
  }
}
