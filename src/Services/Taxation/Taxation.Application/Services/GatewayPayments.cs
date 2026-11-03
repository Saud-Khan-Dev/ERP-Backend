using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public enum GatewayOutcomeKind
{
  /// The payment is on record.
  Posted,
  /// This report was already dealt with; nothing changed.
  AlreadyPosted,
  /// The money is real but could not be applied: kept for the Section.
  Held,
  /// Not proved genuine, or it contradicts an earlier report. Nothing changed.
  Refused,
  /// It could not be checked now. Nothing changed, and the payment's outcome is unknown.
  Unavailable
}

public sealed record GatewayOutcome(GatewayOutcomeKind Kind, Guid? NotificationId = null, Guid? PaymentId = null, string? Reason = null);

/// Turns a gateway's report into a payment. Every message passes the gateway's own verification first, and nothing here
/// takes facts from anywhere else, so an unverified message cannot settle a liability. The order of the steps is what
/// keeps the money safe: the report is kept (committed) before it is worked, a repeat finds the row it left behind, and
/// the payment and the row's POSTED mark are saved in one transaction under the file's lock.
public sealed class GatewayPayments(
  IApplicationDbContext context,
  IPaymentGateway gateway,
  PaymentPosting posting,
  IAuditTrail audit,
  IClock clock,
  ILogger<GatewayPayments> logger)
{
  private const string Kind = "Gateway payment";

  public async Task<GatewayOutcome> ReceiveAsync(GatewayMessage message, CancellationToken cancellationToken)
  {
    switch (await gateway.VerifyAsync(message, cancellationToken))
    {
      case GatewayVerification.Refused refused:
        // forged or unconfirmed: logged, not stored, so a flood of them cannot fill the database. No message content.
        logger.LogWarning("Gateway {Source} message refused: {Reason}", gateway.Source, refused.Reason);
        return new(GatewayOutcomeKind.Refused, Reason: refused.Reason);

      case GatewayVerification.Unavailable unavailable:
        logger.LogWarning("Gateway {Source} message could not be checked: {Reason}", gateway.Source, unavailable.Reason);
        return new(GatewayOutcomeKind.Unavailable, Reason: unavailable.Reason);

      case GatewayVerification.Genuine genuine:
        return await RecordAsync(genuine.Facts, cancellationToken);

      default:
        throw new InvalidOperationException("Unknown gateway verification.");
    }
  }

  /// A Section officer tries a kept payment again, after fixing what stopped it. Its facts are the ones on record.
  public async Task<GatewayOutcome> RetryAsync(GatewayNotificationId id, CancellationToken cancellationToken) => await ProcessAsync(id, cancellationToken);

  private async Task<GatewayOutcome> RecordAsync(GatewayPaymentFacts facts, CancellationToken cancellationToken)
  {
    using var system = AuditScope.ActingAs(gateway.Source);

    GatewayNotification row;
    try
    {
      row = GatewayNotification.Record(GatewayNotificationId.New(), gateway.Source, facts, clock.UtcNow);
    }
    catch (DomainException ex)
    {
      logger.LogError("Gateway {Source} sent a genuine message that cannot be read, event {EventId}: {Reason}", gateway.Source, facts.EventId, ex.Message);
      return new(GatewayOutcomeKind.Refused, Reason: ex.Message);
    }

    if (await context.TryAddGatewayNotificationAsync(row, cancellationToken))
      return await ProcessAsync(row.Id, cancellationToken);

    var first = await context.GatewayNotifications.AsNoTracking().FirstAsync(n => n.Source == row.Source && n.EventId == row.EventId, cancellationToken);
    if (!first.SameFacts(gateway.Source, facts))
    {
      logger.LogError("Gateway {Source} event {EventId} was reported again with different facts.", gateway.Source, facts.EventId);
      return new(GatewayOutcomeKind.Refused, first.Id.Value, Reason: "The event was reported before with different facts.");
    }
    return first.Status switch
    {
      GatewayNotificationStatus.Posted => new(GatewayOutcomeKind.AlreadyPosted, first.Id.Value, first.PaymentId?.Value),
      GatewayNotificationStatus.Received => await ProcessAsync(first.Id, cancellationToken),  // the first attempt stopped half way
      _ => new(GatewayOutcomeKind.Held, first.Id.Value, Reason: first.Error)
    };
  }

  private async Task<GatewayOutcome> ProcessAsync(GatewayNotificationId id, CancellationToken cancellationToken)
  {
    var psid = await context.GatewayNotifications.AsNoTracking().Where(n => n.Id == id).Select(n => n.Psid).FirstAsync(cancellationToken);
    var fileId = await context.TaxFiles.AsNoTracking().Where(f => f.Psid == psid).Select(f => f.Id).FirstOrDefaultAsync(cancellationToken);
    if (fileId is null)
      return await HoldAsync(id, $"No property has the reference {psid}.", null, cancellationToken);

    string refusal;
    await using (var transaction = await context.BeginTransactionAsync(cancellationToken))
    {
      await context.LockTaxFileAsync(fileId, cancellationToken);
      // read under the lock: a report being worked by another request is finished by now
      var row = await context.GatewayNotifications.FirstAsync(n => n.Id == id, cancellationToken);
      if (!row.IsOpen)
        return Finished(row);

      try
      {
        var reason = await TakeAsync(row, fileId, cancellationToken);
        if (reason is null)
        {
          await context.SaveChangesAsync(cancellationToken);
          await transaction.CommitAsync(cancellationToken);
          logger.LogInformation("Gateway {Source} event {EventId} posted as payment {PaymentId}.", row.Source, row.EventId, row.PaymentId?.Value);
          return new(GatewayOutcomeKind.Posted, row.Id.Value, row.PaymentId?.Value);
        }
        refusal = reason;
      }
      catch (DomainException ex)
      {
        refusal = ex.Message;
      }
      catch (DbUpdateException)
      {
        // the same transaction reference on another file at this same moment: the second one waits for the Section
        refusal = "The payment could not be saved; its transaction reference may already be on record.";
      }
      await transaction.RollbackAsync(cancellationToken);
    }

    context.DiscardChanges();
    return await HoldAsync(id, refusal, fileId, cancellationToken);
  }

  /// Takes the money and marks the row. Null when it worked, else why it could not be applied.
  private async Task<string?> TakeAsync(GatewayNotification row, TaxFileId fileId, CancellationToken cancellationToken)
  {
    if (Rupees.Round(row.Amount) != row.Amount)
      return $"{row.Amount} is not a whole number of rupees; a payment on a reference is taken in whole rupees.";

    var file = await context.LoadTaxFileAsync(fileId.Value, cancellationToken);
    var taken = await posting.TakeAsync(file, new PaymentReceived(row.Amount, row.Channel, row.TransactionRef, null, row.BankCode, row.PaidAt), cancellationToken);
    if (!taken.IsSuccess)
      return taken.Message;
    row.MarkPosted(PaymentId.Of(taken.Value!.PaymentId), fileId, clock.UtcNow);
    audit.Record(taken.Value.AlreadyPosted ? "Matched" : "Posted", Kind, row.Id.Value, row.TransactionRef, null, fileId.Value, file.FileNo,
      $"{Rupees.Text(row.Amount)} reported by {row.Source}");
    return null;
  }

  private async Task<GatewayOutcome> HoldAsync(GatewayNotificationId id, string reason, TaxFileId? fileId, CancellationToken cancellationToken)
  {
    var row = await context.GatewayNotifications.FirstAsync(n => n.Id == id, cancellationToken);
    if (row.Status == GatewayNotificationStatus.Posted || row.Status == GatewayNotificationStatus.Dismissed)
      return Finished(row);
    if (row.Status == GatewayNotificationStatus.Received || row.Error != reason)
    {
      row.Hold(reason, fileId, clock.UtcNow);
      audit.Record("Held", Kind, row.Id.Value, row.TransactionRef, null, fileId?.Value, null, reason);
      await context.SaveChangesAsync(cancellationToken);
    }
    logger.LogWarning("Gateway {Source} event {EventId} held: {Reason}", row.Source, row.EventId, reason);
    return new(GatewayOutcomeKind.Held, row.Id.Value, Reason: reason);
  }

  private static GatewayOutcome Finished(GatewayNotification row) => row.Status == GatewayNotificationStatus.Posted
    ? new(GatewayOutcomeKind.AlreadyPosted, row.Id.Value, row.PaymentId?.Value)
    : new(GatewayOutcomeKind.Refused, row.Id.Value, Reason: row.Error);
}
