using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record HrActionDto(
  Guid Id,
  HrActionType ActionType,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  Guid? OldPostId,
  string? OldPostCode,
  Guid? NewPostId,
  string? NewPostCode,
  int? OldBps,
  int? NewBps,
  string? OldOrgUnit,
  string? NewOrgUnit,
  DateOnly EffectiveDate,
  string? OrderNumber,
  string? Reason,
  Guid? SupportingDocumentId,
  HrActionStatus Status,
  Guid? ApprovalRequestId,
  Guid? ApprovedBy,
  DateTime? ApprovedAt,
  Guid? ResultingServiceHistoryId,
  Guid? CreatedBy,
  DateTime? CreatedAt,
  DateTime? UpdatedAt);

public sealed record HrActionInput(HrActionType ActionType, Guid? NewPostId, DateOnly EffectiveDate, string? OrderNumber, string? Reason, Guid? SupportingDocumentId);

public sealed record GetHrActionsQueryResult(PaginatedResult<HrActionDto> HrActions);

public sealed record GetHrActionsQuery(
  PaginationRequest Pagination,
  HrActionStatus? Status,
  HrActionType? ActionType,
  Guid? EmployeeId,
  DateOnly? From,
  DateOnly? To) : IQuery<Result<GetHrActionsQueryResult>>;

public sealed record GetHrActionQueryResult(HrActionDto HrAction);

public sealed record GetHrActionQuery(Guid Id) : IQuery<Result<GetHrActionQueryResult>>;

/// Drafts an HR action. The employee's current post (on the effective date) is captured as the "old" side; the new
/// grade and unit come from the new post.
public sealed record CreateHrActionCommand(Guid EmployeeId, HrActionInput Action) : ICommand<Result<CreatedResult>>;

public sealed record UpdateHrActionCommand(Guid Id, HrActionInput Action) : ICommand<Result<UpdatedResult>>;

public enum HrActionStep
{
  Submit,
  Return,
  Approve,
  Reject,
  Cancel
}

public sealed record MoveHrActionCommand(Guid Id, HrActionStep Step, Guid? ApprovalRequestId) : ICommand<Result<UpdatedResult>>;

/// Carries out an approved action (see HrActionApplier).
public sealed record ApplyHrActionCommand(Guid Id, HrActionApplyInput Input) : ICommand<Result<CreatedResult>>;

public class HrActionInputValidator : AbstractValidator<HrActionInput>
{
  public HrActionInputValidator()
  {
    RuleFor(x => x.ActionType).IsInEnum();
    RuleFor(x => x.OrderNumber).MaximumLength(100);
    RuleFor(x => x.Reason).MaximumLength(4000);
  }
}

public class CreateHrActionCommandValidator : AbstractValidator<CreateHrActionCommand>
{
  public CreateHrActionCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.Action).NotNull().SetValidator(new HrActionInputValidator());
  }
}

public class UpdateHrActionCommandValidator : AbstractValidator<UpdateHrActionCommand>
{
  public UpdateHrActionCommandValidator() => RuleFor(x => x.Action).NotNull().SetValidator(new HrActionInputValidator());
}

public class ApplyHrActionCommandValidator : AbstractValidator<ApplyHrActionCommand>
{
  public ApplyHrActionCommandValidator()
  {
    RuleFor(x => x.Input).NotNull();
    RuleFor(x => x.Input.ExternalReferenceOrg).MaximumLength(200);
    RuleFor(x => x.Input.EmploymentMethod).IsInEnum().When(x => x.Input?.EmploymentMethod is not null);
    RuleFor(x => x.Input.StageNumber).GreaterThanOrEqualTo(0).When(x => x.Input?.StageNumber is not null);
    RuleFor(x => x.Input.NoticePeriodDays).GreaterThanOrEqualTo(0).When(x => x.Input?.NoticePeriodDays is not null);
    RuleFor(x => x.Input.Remarks).MaximumLength(4000);
  }
}

public class HrActionHandlers(
  IApplicationDbContext context,
  ServiceRecordReader reader,
  HrActionApplier applier,
  HrLookup lookup,
  ICurrentUser currentUser,
  IClock clock) :
  IQueryHandler<GetHrActionsQuery, Result<GetHrActionsQueryResult>>,
  IQueryHandler<GetHrActionQuery, Result<GetHrActionQueryResult>>,
  ICommandHandler<CreateHrActionCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateHrActionCommand, Result<UpdatedResult>>,
  ICommandHandler<MoveHrActionCommand, Result<UpdatedResult>>,
  ICommandHandler<ApplyHrActionCommand, Result<CreatedResult>>
{
  public async Task<Result<GetHrActionsQueryResult>> Handle(GetHrActionsQuery query, CancellationToken cancellationToken)
  {
    var actions = context.HrActions.AsNoTracking();
    if (query.Status is { } status)
      actions = actions.Where(a => a.Status == status);
    if (query.ActionType is { } type)
      actions = actions.Where(a => a.ActionType == type);
    if (query.EmployeeId is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      actions = actions.Where(a => a.EmployeeId == employeeId);
    }
    if (query.From is { } from)
      actions = actions.Where(a => a.EffectiveDate >= from);
    if (query.To is { } to)
      actions = actions.Where(a => a.EffectiveDate <= to);

    var total = await actions.LongCountAsync(cancellationToken);
    var page = await actions.OrderByDescending(a => a.CreatedAt)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    var data = await MapAsync(page, cancellationToken);
    return Result<GetHrActionsQueryResult>.Success(new(new PaginatedResult<HrActionDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetHrActionQueryResult>> Handle(GetHrActionQuery query, CancellationToken cancellationToken)
  {
    var actionId = HrActionRequestId.Of(query.Id);
    var action = await context.HrActions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken)
      ?? throw new HrActionNotFoundException($"HR action {query.Id} was not found.");
    return Result<GetHrActionQueryResult>.Success(new((await MapAsync([action], cancellationToken))[0]));
  }

  public async Task<Result<CreatedResult>> Handle(CreateHrActionCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var (details, current) = await ResolveAsync(employee.Id, command.Action, cancellationToken);

    var open = await context.HrActions.AnyAsync(a => a.EmployeeId == employee.Id && a.ActionType == details.ActionType
      && (a.Status == HrActionStatus.Draft || a.Status == HrActionStatus.Pending || a.Status == HrActionStatus.Approved), cancellationToken);
    if (open)
      return Result<CreatedResult>.Failure($"{employee.DisplayName} already has an open {EnumText.Words(details.ActionType)} action. Finish or cancel it first.");

    var action = HrActionRequest.Draft(HrActionRequestId.New(), employee, details, current);
    context.HrActions.Add(action);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(action.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateHrActionCommand command, CancellationToken cancellationToken)
  {
    var action = await context.LoadHrActionAsync(command.Id, cancellationToken);
    var (details, current) = await ResolveAsync(action.EmployeeId, command.Action, cancellationToken);
    action.UpdateDraft(details, current);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(MoveHrActionCommand command, CancellationToken cancellationToken)
  {
    var action = await context.LoadHrActionAsync(command.Id, cancellationToken);
    switch (command.Step)
    {
      case HrActionStep.Submit: action.Submit(); break;
      case HrActionStep.Return: action.ReturnToDraft(); break;
      case HrActionStep.Approve: action.Approve(currentUser.UserId, clock.UtcNow, command.ApprovalRequestId); break;
      case HrActionStep.Reject: action.Reject(); break;
      case HrActionStep.Cancel: action.Cancel(); break;
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(ApplyHrActionCommand command, CancellationToken cancellationToken)
  {
    var action = await context.LoadHrActionAsync(command.Id, cancellationToken);
    var history = await applier.ApplyAsync(action, command.Input, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(history.Id);
  }

  /// The new grade and unit from the new post (on the effective date) and the employee's current post as the old side.
  private async Task<(HrActionDetails Details, ServiceChange Current)> ResolveAsync(EmployeeId employeeId, HrActionInput input, CancellationToken cancellationToken)
  {
    PostId? newPostId = null;
    PayScaleGradeId? newGrade = null;
    OrganizationUnitId? newUnit = null;

    if (input.NewPostId is { } post)
    {
      var newPost = await context.LoadPostAsync(post, cancellationToken);
      var version = newPost.VersionOn(input.EffectiveDate)
        ?? throw new DomainException($"Post {newPost.PostCode} does not exist on {input.EffectiveDate:yyyy-MM-dd}.");
      version.EnsureAcceptsRegularAppointment(newPost.PostCode);
      newPostId = newPost.Id;
      newGrade = version.GradeId;
      newUnit = version.OrgUnitId;
    }
    else if (HrActionRequest.NeedsNewPost(input.ActionType))
    {
      throw new DomainException($"The {EnumText.Words(input.ActionType)} needs the new post.");
    }

    EmployeeDocumentId? documentId = null;
    if (input.SupportingDocumentId is { } doc)
    {
      var document = await context.LoadDocumentAsync(doc, cancellationToken);
      if (document.EmployeeId != employeeId)
        throw new DomainException("The supporting document belongs to a different employee.");
      documentId = document.Id;
    }

    var current = await reader.RegularPostOnAsync(employeeId, input.EffectiveDate, cancellationToken);
    var details = new HrActionDetails(input.ActionType, newPostId, newGrade, newUnit, input.EffectiveDate, input.OrderNumber, input.Reason, documentId);
    return (details, current?.AsOld() ?? ServiceChange.None);
  }

  private async Task<List<HrActionDto>> MapAsync(List<HrActionRequest> actions, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(actions.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);
    var posts = await lookup.PostCodesAsync(actions.SelectMany(a => new[] { a.OldPostId, a.NewPostId }), cancellationToken);
    var grades = await lookup.GradesAsync(cancellationToken);
    var units = await lookup.UnitNamesAsync(actions.SelectMany(a => new[] { a.OldOrgUnitId, a.NewOrgUnitId }), clock.Today, cancellationToken);

    return actions.Select(a => new HrActionDto(
      a.Id.Value, a.ActionType, a.EmployeeId.Value, people.GetValueOrDefault(a.EmployeeId.Value)?.EmployeeNumber ?? "",
      people.GetValueOrDefault(a.EmployeeId.Value)?.FullName ?? "", a.OldPostId?.Value, a.OldPostId is null ? null : posts.GetValueOrDefault(a.OldPostId.Value),
      a.NewPostId?.Value, a.NewPostId is null ? null : posts.GetValueOrDefault(a.NewPostId.Value),
      a.OldGradeId is null ? null : grades.GetValueOrDefault(a.OldGradeId.Value)?.BpsNumber,
      a.NewGradeId is null ? null : grades.GetValueOrDefault(a.NewGradeId.Value)?.BpsNumber,
      a.OldOrgUnitId is null ? null : units.GetValueOrDefault(a.OldOrgUnitId.Value), a.NewOrgUnitId is null ? null : units.GetValueOrDefault(a.NewOrgUnitId.Value),
      a.EffectiveDate, a.OrderNumber, a.Reason, a.SupportingDocumentId?.Value, a.Status, a.ApprovalRequestId, a.ApprovedBy, a.ApprovedAt,
      a.ResultingServiceHistoryId?.Value, a.CreatedBy, a.CreatedAt, a.UpdatedAt)).ToList();
  }
}

// ---- separations ----

public sealed record SeparationDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  SeparationType SeparationType,
  DateOnly SeparationDate,
  Guid? ServiceHistoryId,
  Guid? SettlementPayrollTransactionId,
  string? OrderNumber,
  string? Reason,
  int? NoticePeriodDays,
  decimal? FinalSettlementAmount,
  decimal? OutstandingLoanAmount,
  decimal? LeaveEncashmentAmount,
  string? PensionReference,
  DateTime? CreatedAt);

public sealed record GetSeparationsQueryResult(PaginatedResult<SeparationDto> Separations);

public sealed record GetSeparationsQuery(PaginationRequest Pagination, SeparationType? Type, DateOnly? From, DateOnly? To) : IQuery<Result<GetSeparationsQueryResult>>;

public sealed record GetSeparationQueryResult(SeparationDto? Separation);

public sealed record GetEmployeeSeparationQuery(Guid EmployeeId) : IQuery<Result<GetSeparationQueryResult>>;

/// Records an employee leaving service directly (when no HR action was drafted). EmploymentStatus overrides the status
/// the type leaves them in (retired, resigned, terminated or deceased).
public sealed record RecordSeparationCommand(
  Guid EmployeeId,
  SeparationType SeparationType,
  DateOnly SeparationDate,
  string? OrderNumber,
  string? Reason,
  int? NoticePeriodDays,
  EmploymentStatus? EmploymentStatus,
  Guid? EventTypeId,
  Guid? SupportingDocumentId) : ICommand<Result<CreatedResult>>;

public sealed record UpdateSeparationCommand(
  Guid Id,
  string? OrderNumber,
  string? Reason,
  int? NoticePeriodDays,
  decimal? FinalSettlementAmount,
  decimal? OutstandingLoanAmount,
  decimal? LeaveEncashmentAmount,
  string? PensionReference) : ICommand<Result<UpdatedResult>>;

public class RecordSeparationCommandValidator : AbstractValidator<RecordSeparationCommand>
{
  public RecordSeparationCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.SeparationType).IsInEnum();
    RuleFor(x => x.EmploymentStatus)
      .Must(s => s is EmploymentStatus.Retired or EmploymentStatus.Resigned or EmploymentStatus.Terminated or EmploymentStatus.Deceased)
      .When(x => x.EmploymentStatus.HasValue)
      .WithMessage("After a separation the status is retired, resigned, terminated or deceased.");
    RuleFor(x => x.NoticePeriodDays).GreaterThanOrEqualTo(0).When(x => x.NoticePeriodDays.HasValue);
    RuleFor(x => x.OrderNumber).MaximumLength(100);
    RuleFor(x => x.Reason).MaximumLength(4000);
  }
}

public class UpdateSeparationCommandValidator : AbstractValidator<UpdateSeparationCommand>
{
  public UpdateSeparationCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.OutstandingLoanAmount).GreaterThanOrEqualTo(0).When(x => x.OutstandingLoanAmount.HasValue);
    RuleFor(x => x.LeaveEncashmentAmount).GreaterThanOrEqualTo(0).When(x => x.LeaveEncashmentAmount.HasValue);
    RuleFor(x => x.NoticePeriodDays).GreaterThanOrEqualTo(0).When(x => x.NoticePeriodDays.HasValue);
    RuleFor(x => x.PensionReference).MaximumLength(100);
  }
}

public class SeparationHandlers(IApplicationDbContext context, ServiceRecordWriter writer, HrLookup lookup, ICurrentUser currentUser) :
  IQueryHandler<GetSeparationsQuery, Result<GetSeparationsQueryResult>>,
  IQueryHandler<GetEmployeeSeparationQuery, Result<GetSeparationQueryResult>>,
  ICommandHandler<RecordSeparationCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateSeparationCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetSeparationsQueryResult>> Handle(GetSeparationsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Separations.AsNoTracking();
    if (query.Type is { } type)
      rows = rows.Where(s => s.SeparationType == type);
    if (query.From is { } from)
      rows = rows.Where(s => s.SeparationDate >= from);
    if (query.To is { } to)
      rows = rows.Where(s => s.SeparationDate <= to);

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(s => s.SeparationDate)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await MapAsync(page, cancellationToken);
    return Result<GetSeparationsQueryResult>.Success(new(new PaginatedResult<SeparationDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetSeparationQueryResult>> Handle(GetEmployeeSeparationQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");
    var separation = await context.Separations.AsNoTracking().FirstOrDefaultAsync(s => s.EmployeeId == employeeId, cancellationToken);
    return Result<GetSeparationQueryResult>.Success(new(separation is null ? null : (await MapAsync([separation], cancellationToken))[0]));
  }

  public async Task<Result<CreatedResult>> Handle(RecordSeparationCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);

    EmployeeDocumentId? documentId = null;
    if (command.SupportingDocumentId is { } doc)
    {
      var document = await context.LoadDocumentAsync(doc, cancellationToken);
      if (document.EmployeeId != employee.Id)
        throw new DomainException("The supporting document belongs to a different employee.");
      documentId = document.Id;
    }

    var (_, separation) = await writer.SeparateAsync(employee, new SeparationSpec(command.SeparationType, command.SeparationDate, command.OrderNumber,
      command.Reason, command.NoticePeriodDays, command.EmploymentStatus, command.EventTypeId is { } eventType ? ServiceEventTypeId.Of(eventType) : null,
      documentId, currentUser.UserId), cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(separation.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateSeparationCommand command, CancellationToken cancellationToken)
  {
    var separationId = SeparationId.Of(command.Id);
    var separation = await context.Separations.FirstOrDefaultAsync(s => s.Id == separationId, cancellationToken)
      ?? throw new SeparationNotFoundException($"Separation {command.Id} was not found.");
    separation.UpdateSettlement(command.OrderNumber, command.Reason, command.NoticePeriodDays, command.FinalSettlementAmount,
      command.OutstandingLoanAmount, command.LeaveEncashmentAmount, command.PensionReference);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<List<SeparationDto>> MapAsync(List<EmployeeSeparation> rows, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(rows.Select(s => (EmployeeId?)s.EmployeeId), cancellationToken);
    return rows.Select(s => new SeparationDto(s.Id.Value, s.EmployeeId.Value, people.GetValueOrDefault(s.EmployeeId.Value)?.EmployeeNumber ?? "",
      people.GetValueOrDefault(s.EmployeeId.Value)?.FullName ?? "", s.SeparationType, s.SeparationDate, s.ServiceHistoryId?.Value,
      s.SettlementPayrollTransactionId?.Value, s.OrderNumber, s.Reason, s.NoticePeriodDays, s.FinalSettlementAmount, s.OutstandingLoanAmount,
      s.LeaveEncashmentAmount, s.PensionReference, s.CreatedAt)).ToList();
  }
}
