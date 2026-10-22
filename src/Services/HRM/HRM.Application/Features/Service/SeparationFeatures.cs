using FluentValidation;
using Microsoft.EntityFrameworkCore;

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
  DateTime? CreatedAt,
  /// The final settlement is worked out (an amount is entered or a settlement payroll slip is linked).
  bool Settled = false);

public sealed record GetSeparationsQueryResult(PaginatedResult<SeparationDto> Separations);

/// Search = employee number or name; Settled = false lists the separations whose final settlement is not worked out.
public sealed record GetSeparationsQuery(
  PaginationRequest Pagination,
  SeparationType? Type,
  DateOnly? From,
  DateOnly? To,
  string? Search = null,
  bool? Settled = null) : IQuery<Result<GetSeparationsQueryResult>>;

public sealed record GetSeparationQueryResult(SeparationDto? Separation);

public sealed record GetEmployeeSeparationQuery(Guid EmployeeId) : IQuery<Result<GetSeparationQueryResult>>;

/// What a recorded separation still leaves for someone to do, as it stands now.
public sealed record GetSeparationChecklistQuery(Guid EmployeeId) : IQuery<Result<SeparationChecklistDto>>;

/// Records an employee leaving service directly (when no HR action was drafted). EmploymentStatus overrides the status
/// the type leaves them in.
public sealed record RecordSeparationCommand(
  Guid EmployeeId,
  SeparationType SeparationType,
  DateOnly SeparationDate,
  string? OrderNumber,
  string? Reason,
  int? NoticePeriodDays,
  EmploymentStatus? EmploymentStatus,
  Guid? EventTypeId,
  Guid? SupportingDocumentId) : ICommand<Result<RecordSeparationResult>>;

public sealed record RecordSeparationResult(Guid Id, SeparationChecklistDto Checklist);

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
    RuleFor(x => x.SeparationType).IsInEnum().NotEqual(SeparationType.Other).WithMessage("Choose the kind of separation (retirement, resignation, dismissal ...).");
    RuleFor(x => x.EmploymentStatus)
      .Must(s => EmploymentStatuses.LeftService.Contains(s!.Value))
      .When(x => x.EmploymentStatus.HasValue)
      .WithMessage("After a separation the status is one of the left-service states (retired, resigned, dismissed ...).");
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
    RuleFor(x => x.OrderNumber).MaximumLength(100);
    RuleFor(x => x.Reason).MaximumLength(4000);
  }
}

public class SeparationHandlers(IApplicationDbContext context, ServiceRecordWriter writer, HrLookup lookup, ICurrentUser currentUser, DutyGuard duty, IAuditTrail audit) :
  IQueryHandler<GetSeparationsQuery, Result<GetSeparationsQueryResult>>,
  IQueryHandler<GetEmployeeSeparationQuery, Result<GetSeparationQueryResult>>,
  IQueryHandler<GetSeparationChecklistQuery, Result<SeparationChecklistDto>>,
  ICommandHandler<RecordSeparationCommand, Result<RecordSeparationResult>>,
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
    if (query.Settled is { } settled)
      rows = settled
        ? rows.Where(s => s.SettlementPayrollTransactionId != null || s.FinalSettlementAmount != null)
        : rows.Where(s => s.SettlementPayrollTransactionId == null && s.FinalSettlementAmount == null);
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search.Trim());
      rows = rows.Where(s => context.Employees.Any(e => e.Id == s.EmployeeId && (EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape)
        || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape))));
    }

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

  public async Task<Result<SeparationChecklistDto>> Handle(GetSeparationChecklistQuery query, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(query.EmployeeId, cancellationToken);
    var separation = await context.Separations.AsNoTracking().FirstOrDefaultAsync(s => s.EmployeeId == employee.Id, cancellationToken)
      ?? throw new SeparationNotFoundException($"{employee.DisplayName} has no separation recorded.");
    return Result<SeparationChecklistDto>.Success(await writer.ChecklistAsync(employee, separation, null, cancellationToken));
  }

  public async Task<Result<RecordSeparationResult>> Handle(RecordSeparationCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "record a separation", employee, cancellationToken);

    EmployeeDocumentId? documentId = null;
    if (command.SupportingDocumentId is { } doc)
    {
      var document = await context.LoadDocumentAsync(doc, cancellationToken);
      if (document.EmployeeId != employee.Id)
        throw new DomainException("The supporting document belongs to a different employee.");
      documentId = document.Id;
    }

    var (_, separation, checklist) = await writer.SeparateAsync(employee, new SeparationSpec(command.SeparationType, command.SeparationDate, command.OrderNumber,
      command.Reason, command.NoticePeriodDays, command.EmploymentStatus, command.EventTypeId is { } eventType ? ServiceEventTypeId.Of(eventType) : null,
      documentId, currentUser.UserId), cancellationToken);

    audit.Record(HrmVerbs.Separated, employee, $"{EnumText.Label(command.SeparationType)} on {command.SeparationDate:yyyy-MM-dd}"
      + (string.IsNullOrWhiteSpace(command.Reason) ? "" : $" · Reason: {command.Reason.Trim()}"));
    await context.SaveChangesAsync(cancellationToken);
    return Result<RecordSeparationResult>.Success(new(separation.Id.Value, checklist));
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateSeparationCommand command, CancellationToken cancellationToken)
  {
    var separationId = SeparationId.Of(command.Id);
    var separation = await context.Separations.FirstOrDefaultAsync(s => s.Id == separationId, cancellationToken)
      ?? throw new SeparationNotFoundException($"Separation {command.Id} was not found.");
    await duty.EnsureNotOwnRecordAsync(separation.EmployeeId, "change a separation settlement", separation, cancellationToken);
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
      s.LeaveEncashmentAmount, s.PensionReference, s.CreatedAt, s.SettlementPayrollTransactionId is not null || s.FinalSettlementAmount is not null)).ToList();
  }
}
