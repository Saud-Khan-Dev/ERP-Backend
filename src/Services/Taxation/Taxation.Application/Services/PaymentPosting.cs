using Microsoft.EntityFrameworkCore;

/// What was received, by whichever route: PaidAt null means now.
public sealed record PaymentReceived(decimal Amount, PaymentChannel Channel, string? TransactionRef, string? ReceiptNo, string? BankCode, DateTime? PaidAt);

/// Takes money against a file's reference: the one place the rules of a payment are applied, for staff and gateways alike.
/// The caller holds the file's lock inside a transaction (LockTaxFileAsync), then saves and commits.
public sealed class PaymentPosting(
  IApplicationDbContext context,
  TaxSetup setup,
  Ledger ledger,
  Messenger messenger,
  CodeIssuer codes,
  ICurrentUser currentUser,
  IClock clock)
{
  public async Task<Result<ReceivePaymentResult>> TakeAsync(TaxFile file, PaymentReceived received, CancellationToken cancellationToken)
  {
    var today = clock.Today;

    var reference = Text(received.TransactionRef);
    if (reference is not null && await context.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.TransactionRef == reference, cancellationToken) is { } posted)
    {
      if (posted.TaxFileId != file.Id || posted.Amount != Rupees.Round(received.Amount))
        return Result<ReceivePaymentResult>.Failure($"Transaction {reference} was already posted for another reference or amount.");
      var dues = await ledger.DuesAsync(file, cancellationToken);
      return Result<ReceivePaymentResult>.Success(new(posted.Id.Value, true, posted.Amount, dues, dues, [], posted.ReceiptNo));
    }

    var duesBefore = await ledger.DuesAsync(file, cancellationToken);
    if (duesBefore <= 0)
      return Result<ReceivePaymentResult>.Failure($"Nothing is owed on reference {Numbers.FormatPsid(file.Psid)}.");
    var amount = Rupees.Round(received.Amount);
    if (amount > duesBefore)
      return Result<ReceivePaymentResult>.Failure($"The amount is more than the {Rupees.Text(duesBefore)} owed on this reference.");
    var paidAt = received.PaidAt ?? clock.UtcNow;
    if (DateOnly.FromDateTime(paidAt.AddHours(5)) < file.OpenedOn)
      return Result<ReceivePaymentResult>.Failure($"{file.FileNo} was opened on {file.OpenedOn:dd'/'MM'/'yyyy}. A payment on it cannot be dated before that.");

    var manual = Payment.IsManual(received.Channel);
    var receipt = manual ? await ReceiptNoAsync(received.ReceiptNo, cancellationToken) : null;
    if (manual && receipt is null)
      return Result<ReceivePaymentResult>.Failure($"Receipt no. {received.ReceiptNo!.Trim()} is already used. Each cash or counter receipt has its own number.");

    var payer = file.Addressee(today);
    var payment = Payment.Receive(PaymentId.New(), file, payer?.Name, received.Channel, amount, reference, receipt, received.BankCode, paidAt,
      manual ? Guard.Actor(currentUser.UserId, "receive cash") : currentUser.UserId, clock.UtcNow);
    context.Payments.Add(payment);

    var surcharge = await setup.HeadAsync(HeadCodes.Surcharge, cancellationToken);
    var touched = await ledger.SettleAsync(file, payment, surcharge.Id, today, cancellationToken);

    var last = touched.LastOrDefault();
    var values = last is not null ? Messenger.Values(file, last, payer?.Name) : Messenger.FileValues(file, payer?.Name ?? "");
    values["amount"] = Rupees.Text(amount);
    values["balance"] = Rupees.Text(duesBefore - amount);
    await messenger.SmsAsync(MessageCodes.PaymentReceived, file, last, null, last?.PayerMobile ?? payer?.Mobile, payer?.OwnerId, values, cancellationToken);
    await messenger.SyncAsync(file, PsidSyncReason.Payment, last?.Id, null, null,
      $"{Rupees.Text(amount)} by {EnumText.Words(received.Channel)}{(receipt is null ? "" : " receipt " + receipt)}{(last is null ? " (opening arrears)" : "")}", cancellationToken);

    return Result<ReceivePaymentResult>.Success(new(payment.Id.Value, false, amount, duesBefore, duesBefore - amount, touched.Select(d => d.Id.Value).ToList(), receipt));
  }

  /// The receipt number of a cash or counter payment: the one typed (null when it is already used), else the next free one.
  private async Task<string?> ReceiptNoAsync(string? typed, CancellationToken cancellationToken)
  {
    if (Text(typed) is { } given)
      return await context.Payments.AnyAsync(p => p.ReceiptNo == given, cancellationToken) ? null : given;
    while (true)
    {
      var next = Numbers.ReceiptNo(await codes.NextAsync(NumberSequence.ReceiptNo, cancellationToken));
      if (!await context.Payments.AnyAsync(p => p.ReceiptNo == next, cancellationToken))
        return next;
    }
  }

  private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
