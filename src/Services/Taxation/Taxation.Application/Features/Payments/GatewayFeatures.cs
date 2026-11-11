using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// The payments gateways reported, for the Section: what is held, and why. A held payment is money already taken from
/// someone, so it stays open until it is applied or an officer closes it with a reason.
public sealed record GetGatewayNotificationsQueryResult(PaginatedResult<GatewayNotificationDto> Notifications);
/// Waiting: what the bell counts - held, or received over five minutes ago and not worked yet.
public sealed record GetGatewayNotificationsQuery(PaginationRequest Pagination, GatewayNotificationStatus? Status, string? Search, bool Waiting = false) : IQuery<Result<GetGatewayNotificationsQueryResult>>;

public sealed record RetryGatewayNotificationResult(GatewayOutcomeKind Outcome, Guid? PaymentId, string? Reason);
public sealed record RetryGatewayNotificationCommand(Guid Id) : ICommand<Result<RetryGatewayNotificationResult>>;
public sealed record DismissGatewayNotificationCommand(Guid Id, string Reason) : ICommand<Result<UpdatedResult>>;

public class DismissGatewayNotificationCommandValidator : AbstractValidator<DismissGatewayNotificationCommand>
{
  public DismissGatewayNotificationCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class GatewayNotificationHandlers(IApplicationDbContext context, GatewayPayments gateway, IAuditTrail audit, IClock clock) :
  IQueryHandler<GetGatewayNotificationsQuery, Result<GetGatewayNotificationsQueryResult>>,
  ICommandHandler<RetryGatewayNotificationCommand, Result<RetryGatewayNotificationResult>>,
  ICommandHandler<DismissGatewayNotificationCommand, Result<UpdatedResult>>
{
  private const string EveryTown = "The gateway's payments are not linked to a town. So this needs access to every town.";

  public async Task<Result<GetGatewayNotificationsQueryResult>> Handle(GetGatewayNotificationsQuery query, CancellationToken cancellationToken)
  {
    if (context.IsLimited())
      return Result<GetGatewayNotificationsQueryResult>.Failure(EveryTown);
    var rows = context.GatewayNotifications.AsNoTracking();
    if (query.Status is { } status)
      rows = rows.Where(n => n.Status == status);
    if (query.Waiting)
    {
      var stuck = clock.UtcNow.AddMinutes(-GatewayNotification.StuckMinutes);
      rows = rows.Where(n => n.Status == GatewayNotificationStatus.Held || (n.Status == GatewayNotificationStatus.Received && n.ReceivedAt < stuck));
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim();
      var reference = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
      rows = rows.Where(n => n.TransactionRef.Contains(s) || n.Psid.Contains(reference) || n.EventId.Contains(s));
    }
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(n => n.ReceivedAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var fileIds = page.Where(n => n.TaxFileId is not null).Select(n => n.TaxFileId!).Distinct().ToList();
    var fileNos = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    var data = page.Select(n => new GatewayNotificationDto(n.Id.Value, n.Source, n.EventType, n.EventId, n.Channel, n.Psid, n.Amount, n.TransactionRef, n.BankCode, n.PaidAt, n.ReceivedAt,
      n.ProcessedAt, n.Status, n.TaxFileId?.Value, n.TaxFileId is { } f ? fileNos.GetValueOrDefault(f) : null, n.PaymentId?.Value, n.Error)).ToList();
    return Result<GetGatewayNotificationsQueryResult>.Success(new(new PaginatedResult<GatewayNotificationDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<RetryGatewayNotificationResult>> Handle(RetryGatewayNotificationCommand command, CancellationToken cancellationToken)
  {
    if (context.IsLimited())
      return Result<RetryGatewayNotificationResult>.Failure(EveryTown);
    var row = await LoadAsync(command.Id, cancellationToken);
    if (!row.IsOpen)
      return Result<RetryGatewayNotificationResult>.Failure($"This payment is already {EnumText.Words(row.Status)}.");
    var outcome = await gateway.RetryAsync(row.Id, cancellationToken);
    return Result<RetryGatewayNotificationResult>.Success(new(outcome.Kind, outcome.PaymentId, outcome.Reason));
  }

  public async Task<Result<UpdatedResult>> Handle(DismissGatewayNotificationCommand command, CancellationToken cancellationToken)
  {
    if (context.IsLimited())
      return Result<UpdatedResult>.Failure(EveryTown);
    var row = await LoadAsync(command.Id, cancellationToken);
    row.Dismiss(command.Reason, clock.UtcNow);
    audit.Record("Dismissed", "Gateway payment", row.Id.Value, row.TransactionRef, null, row.TaxFileId?.Value, null, $"{Rupees.Text(row.Amount)}: {command.Reason.Trim()}");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<GatewayNotification> LoadAsync(Guid id, CancellationToken cancellationToken)
  {
    var notificationId = GatewayNotificationId.Of(id);
    return await context.GatewayNotifications.FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken)
      ?? throw new PaymentNotFoundException($"Gateway payment {id} was not found.");
  }
}
