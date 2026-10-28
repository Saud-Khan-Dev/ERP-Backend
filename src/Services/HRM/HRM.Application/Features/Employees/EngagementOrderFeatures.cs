using FluentValidation;
using Microsoft.EntityFrameworkCore;

// Orders of contingent staff: the office and designation they work as, the period and the wage, with the order letter.
// A renewal is a new order; the days between two orders are not paid. The person's term follows their latest order.

/// Rate is withheld (null, named in Restricted) from a caller without HR_SENSITIVE.VIEW, like every pay figure.
public sealed record EngagementOrderDto(
  Guid Id,
  Guid EmployeeId,
  Guid OrgUnitId,
  string? Office,
  Guid DesignationId,
  string? Designation,
  DateOnly StartDate,
  DateOnly EndDate,
  WageBasis WageBasis,
  decimal? Rate,
  Guid LetterDocumentId,
  RecordStatus Status,
  string? CancelReason,
  DateTime? CreatedAt,
  IReadOnlyList<string>? Restricted = null);

public sealed record GetEngagementOrdersQueryResult(IReadOnlyList<EngagementOrderDto> Orders);
public sealed record GetEngagementOrdersQuery(Guid EmployeeId, bool Own = false) : IQuery<Result<GetEngagementOrdersQueryResult>>;

public sealed record EngagementOrderInput(Guid OrgUnitId, Guid DesignationId, DateOnly StartDate, DateOnly EndDate, WageBasis WageBasis, decimal Rate, Guid LetterDocumentId);
public sealed record AddEngagementOrderCommand(Guid EmployeeId, EngagementOrderInput Order) : ICommand<Result<CreatedResult>>;
public sealed record CancelEngagementOrderCommand(Guid Id, string Reason) : ICommand<Result<UpdatedResult>>;

public class AddEngagementOrderCommandValidator : AbstractValidator<AddEngagementOrderCommand>
{
  public AddEngagementOrderCommandValidator()
  {
    RuleFor(x => x.Order).NotNull();
    RuleFor(x => x.Order.OrgUnitId).NotEmpty().WithMessage("Choose the office.");
    RuleFor(x => x.Order.DesignationId).NotEmpty().WithMessage("Choose the designation.");
    RuleFor(x => x.Order.WageBasis).IsInEnum();
    RuleFor(x => x.Order.Rate).GreaterThan(0).WithMessage("Give the wage of the order.");
    RuleFor(x => x.Order.LetterDocumentId).NotEmpty().WithMessage("Attach the order letter.");
  }
}

public class CancelEngagementOrderCommandValidator : AbstractValidator<CancelEngagementOrderCommand>
{
  public CancelEngagementOrderCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the order is cancelled.").MaximumLength(500);
}

public class EngagementOrderHandlers(IApplicationDbContext context, HrLookup lookup, Sensitive sensitive, DutyGuard duty, IClock clock) :
  IQueryHandler<GetEngagementOrdersQuery, Result<GetEngagementOrdersQueryResult>>,
  ICommandHandler<AddEngagementOrderCommand, Result<CreatedResult>>,
  ICommandHandler<CancelEngagementOrderCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetEngagementOrdersQueryResult>> Handle(GetEngagementOrdersQuery query, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(query.EmployeeId, cancellationToken);
    var orders = await context.EngagementOrders.AsNoTracking().Where(o => o.EmployeeId == employee.Id)
      .OrderByDescending(o => o.StartDate).ToListAsync(cancellationToken);
    var designations = await lookup.DesignationsAsync(orders.Select(o => (DesignationId?)o.DesignationId), cancellationToken);
    var offices = await lookup.UnitNamesAsync(orders.Select(o => (OrganizationUnitId?)o.OrgUnitId), clock.Today, cancellationToken);
    var shows = sensitive.Shows(query.Own);
    var withheld = sensitive.Withheld(query.Own, Sensitive.Pay);
    return Result<GetEngagementOrdersQueryResult>.Success(new(orders.Select(o => new EngagementOrderDto(
      o.Id.Value, o.EmployeeId.Value, o.OrgUnitId.Value, offices.GetValueOrDefault(o.OrgUnitId.Value), o.DesignationId.Value,
      designations.GetValueOrDefault(o.DesignationId.Value), o.StartDate, o.EndDate, o.WageBasis, shows ? o.Rate : null, o.LetterDocumentId.Value,
      o.Status, o.CancelReason, o.CreatedAt, withheld)).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(AddEngagementOrderCommand command, CancellationToken cancellationToken)
  {
    var input = command.Order;
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    employee.EnsureProfileActive();
    if (employee.HasLeftService)
      return Result<CreatedResult>.Failure($"{employee.DisplayName} has left service.");
    await duty.EnsureNotOwnRecordAsync(employee.Id, "add an order", employee, cancellationToken);

    var office = await context.LoadOrgUnitAsync(input.OrgUnitId, cancellationToken);
    if (!office.IsActiveOn(input.StartDate))
      return Result<CreatedResult>.Failure("That office is not on the structure on the order's first day.");
    (await context.LoadDesignationAsync(input.DesignationId, cancellationToken)).EnsureActive();
    var letter = await context.LoadDocumentAsync(input.LetterDocumentId, cancellationToken);
    if (letter.EmployeeId != employee.Id)
      return Result<CreatedResult>.Failure("The letter is not one of this employee's documents.");

    var order = EngagementOrder.Create(EngagementOrderId.New(), employee, office.Id, DesignationId.Of(input.DesignationId), input.StartDate,
      input.EndDate, input.WageBasis, input.Rate, letter.Id);
    var clash = await context.EngagementOrders.AsNoTracking()
      .Where(o => o.EmployeeId == employee.Id && o.Status == RecordStatus.Active && o.StartDate <= order.EndDate && o.EndDate >= order.StartDate)
      .Select(o => new { o.StartDate, o.EndDate }).FirstOrDefaultAsync(cancellationToken);
    if (clash is not null)
      return Result<CreatedResult>.Failure($"An order already covers {clash.StartDate:dd'/'MM'/'yyyy} to {clash.EndDate:dd'/'MM'/'yyyy}; a renewal starts after it ends.");

    context.EngagementOrders.Add(order);
    await FollowTermAsync(employee, order, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(order.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(CancelEngagementOrderCommand command, CancellationToken cancellationToken)
  {
    var id = EngagementOrderId.Of(command.Id);
    var order = await context.EngagementOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
      ?? throw new EngagementOrderNotFoundException($"Order {command.Id} was not found.");
    var employee = await context.LoadEmployeeAsync(order.EmployeeId.Value, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "cancel an order", employee, cancellationToken);

    // pay already posted for the order's days stays: reverse that payroll first
    var paid = await (
        from t in context.PayrollTransactions
        where t.EmployeeId == order.EmployeeId
        join r in context.PayrollRuns on t.PayrollRunId equals r.Id
        where PayrollInputBuilder.Posted.Contains(r.Status)
        join p in context.PayrollPeriods on r.PayrollPeriodId equals p.Id
        where p.StartDate <= order.EndDate && p.EndDate >= order.StartDate
        select p.StartDate)
      .AnyAsync(cancellationToken);
    if (paid)
      return Result<UpdatedResult>.Failure("Pay for this order's days has been finalized; reverse that payroll run before cancelling the order.");

    order.Cancel(command.Reason);
    var latest = await context.EngagementOrders.Where(o => o.EmployeeId == order.EmployeeId && o.Id != order.Id && o.Status == RecordStatus.Active)
      .OrderByDescending(o => o.EndDate).FirstOrDefaultAsync(cancellationToken);
    if (latest is not null)
      await FollowTermAsync(employee, latest, cancellationToken);
    else
      employee.SetTerm(null, null, null);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// The person's term is their latest order (the "contracts ending" queue reads it).
  private async Task FollowTermAsync(Employee employee, EngagementOrder candidate, CancellationToken cancellationToken)
  {
    var latestEnd = await context.EngagementOrders.Where(o => o.EmployeeId == employee.Id && o.Status == RecordStatus.Active && o.Id != candidate.Id)
      .MaxAsync(o => (DateOnly?)o.EndDate, cancellationToken);
    if (latestEnd is null || candidate.EndDate >= latestEnd)
      employee.SetTerm(candidate.StartDate, candidate.EndDate, null);
  }
}
