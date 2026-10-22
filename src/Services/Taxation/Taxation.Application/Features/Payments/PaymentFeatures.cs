using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// Money received against a property's constant 1Link reference: settled oldest year first, item by item (before the
/// first demand, the opening arrears of the old register). A 1Link, QR or online payment carries the bank's transaction
/// reference (posting it twice changes nothing); cash and bank-counter payments carry the Section's manual receipt number
/// (issued when not given; a number is used once). The file is locked while the payment is taken, so two payments at
/// the same moment are taken one after the other.
public sealed record ReceivePaymentCommand(string Psid, decimal Amount, PaymentChannel Channel, string? TransactionRef, string? ReceiptNo, string? BankCode, DateTime? PaidAt)
  : ICommand<Result<ReceivePaymentResult>>;

/// Takes a payment back (a bounced cheque, a posting on the wrong reference): what it paid is owed again. Needs approval
/// and a reason; the payment stays on record as reversed.
public sealed record ReversePaymentCommand(Guid Id, string Reason) : ICommand<Result<ReversePaymentResult>>;
public sealed record ReversePaymentResult(Guid PaymentId, decimal Amount, decimal? DuesAfter, IReadOnlyList<string> Warnings);

public sealed record GetPaymentsQueryResult(PaginatedResult<PaymentDto> Payments);
/// Kind: demand (against a reference) or fee (a fee receipt); ReceivedBy: the cashier (a user id), for the day's close.
public sealed record GetPaymentsQuery(PaginationRequest Pagination, Guid? TaxFileId, PaymentChannel? Channel, DateOnly? From, DateOnly? To, string? Search,
  PaymentKind? Kind = null, PaymentStatus? Status = null, Guid? ReceivedBy = null, Guid? CircleId = null, Guid? PartyId = null, Guid? FeeId = null)
  : IQuery<Result<GetPaymentsQueryResult>>;
public sealed record GetPaymentQueryResult(PaymentDto Payment);
public sealed record GetPaymentQuery(Guid Id) : IQuery<Result<GetPaymentQueryResult>>;

/// What is behind a reference: the bill inquiry 1Link (or the counter) makes before taking a payment.
public sealed record GetReferenceQuery(string Psid) : IQuery<Result<ReferenceDto>>;

public sealed record GetSmsMessagesQueryResult(PaginatedResult<SmsDto> Messages);
public sealed record GetSmsMessagesQuery(PaginationRequest Pagination, Guid? TaxFileId, Guid? DemandId, SmsStatus? Status = null, string? Search = null,
  DateOnly? From = null, DateOnly? To = null) : IQuery<Result<GetSmsMessagesQueryResult>>;

public class ReceivePaymentCommandValidator : AbstractValidator<ReceivePaymentCommand>
{
  public ReceivePaymentCommandValidator()
  {
    RuleFor(x => x.Psid).NotEmpty().MaximumLength(30);
    RuleFor(x => x.Amount).GreaterThan(0);
    RuleFor(x => x.TransactionRef).MaximumLength(60);
    RuleFor(x => x.ReceiptNo).MaximumLength(40);
    RuleFor(x => x.BankCode).MaximumLength(20);
  }
}

public class ReversePaymentCommandValidator : AbstractValidator<ReversePaymentCommand>
{
  public ReversePaymentCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
}

public class PaymentHandlers(
  IApplicationDbContext context,
  TaxSetup setup,
  Ledger ledger,
  Messenger messenger,
  CodeIssuer codes,
  ICurrentUser currentUser,
  IDutyRules dutyRules,
  IAuditTrail audit,
  IClock clock) :
  ICommandHandler<ReceivePaymentCommand, Result<ReceivePaymentResult>>,
  ICommandHandler<ReversePaymentCommand, Result<ReversePaymentResult>>,
  IQueryHandler<GetPaymentsQuery, Result<GetPaymentsQueryResult>>,
  IQueryHandler<GetPaymentQuery, Result<GetPaymentQueryResult>>,
  IQueryHandler<GetReferenceQuery, Result<ReferenceDto>>,
  IQueryHandler<GetSmsMessagesQuery, Result<GetSmsMessagesQueryResult>>
{
  public async Task<Result<ReceivePaymentResult>> Handle(ReceivePaymentCommand command, CancellationToken cancellationToken)
  {
    var digits = new string((command.Psid ?? "").Where(char.IsAsciiDigit).ToArray());
    var fileId = await context.ScopedFiles().AsNoTracking().Where(f => f.Psid == digits).Select(f => f.Id).FirstOrDefaultAsync(cancellationToken)
      ?? throw new TaxFileNotFoundException($"No property has the reference {command.Psid}.");

    await using var transaction = await context.BeginTransactionAsync(cancellationToken);
    await context.LockTaxFileAsync(fileId, cancellationToken);
    var file = await context.LoadTaxFileAsync(fileId.Value, cancellationToken);
    var today = clock.Today;

    var reference = Text(command.TransactionRef);
    if (reference is not null && await context.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.TransactionRef == reference, cancellationToken) is { } posted)
    {
      if (posted.TaxFileId != file.Id || posted.Amount != Rupees.Round(command.Amount))
        return Result<ReceivePaymentResult>.Failure($"Transaction {reference} was already posted for another reference or amount.");
      var dues = await ledger.DuesAsync(file, cancellationToken);
      return Result<ReceivePaymentResult>.Success(new(posted.Id.Value, true, posted.Amount, dues, dues, [], posted.ReceiptNo));
    }

    var duesBefore = await ledger.DuesAsync(file, cancellationToken);
    if (duesBefore <= 0)
      return Result<ReceivePaymentResult>.Failure($"Nothing is owed on reference {Numbers.FormatPsid(file.Psid)}.");
    var amount = Rupees.Round(command.Amount);
    if (amount > duesBefore)
      return Result<ReceivePaymentResult>.Failure($"The amount exceeds the {Rupees.Text(duesBefore)} behind this reference.");
    var paidAt = command.PaidAt ?? clock.UtcNow;
    if (DateOnly.FromDateTime(paidAt.AddHours(5)) < file.OpenedOn)
      return Result<ReceivePaymentResult>.Failure($"{file.FileNo} was opened on {file.OpenedOn:dd MMM yyyy}; a payment on it cannot be dated before that.");

    var manual = Payment.IsManual(command.Channel);
    var receipt = manual ? await ReceiptNoAsync(command.ReceiptNo, cancellationToken) : null;
    if (manual && receipt is null)
      return Result<ReceivePaymentResult>.Failure($"Receipt no. {command.ReceiptNo!.Trim()} is already used. Each cash or counter receipt has its own number.");

    var payer = file.Addressee(today);
    var payment = Payment.Receive(PaymentId.New(), file, payer?.Name, command.Channel, amount, reference, receipt, command.BankCode, paidAt,
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
      $"{Rupees.Text(amount)} by {EnumText.Words(command.Channel)}{(receipt is null ? "" : " receipt " + receipt)}{(last is null ? " (opening arrears)" : "")}", cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Result<ReceivePaymentResult>.Success(new(payment.Id.Value, false, amount, duesBefore, duesBefore - amount, touched.Select(d => d.Id.Value).ToList(), receipt));
  }

  public async Task<Result<ReversePaymentResult>> Handle(ReversePaymentCommand command, CancellationToken cancellationToken)
  {
    var id = PaymentId.Of(command.Id);
    var fileId = await context.Payments.AsNoTracking().InScope(context).Where(p => p.Id == id)
        .Select(p => new { p.TaxFileId, Maker = p.CreatedBy ?? p.ReceivedBy }).FirstOrDefaultAsync(cancellationToken)
      ?? throw new PaymentNotFoundException($"Payment {command.Id} was not found.");
    var actor = Guard.Actor(currentUser.UserId, "reverse a payment");
    // a cashier does not reverse their own receipts (the person who posted it, or took the cash)
    if (await dutyRules.SeparationRefusalAsync(DutyRules.TaxPaymentReverse, fileId.Maker, actor, "reverse a payment you posted", cancellationToken) is { } refusal)
      return Result<ReversePaymentResult>.Failure(refusal);

    await using var transaction = await context.BeginTransactionAsync(cancellationToken);
    if (fileId.TaxFileId is { } locked)
      await context.LockTaxFileAsync(locked, cancellationToken);
    var payment = await context.Payments.Include(p => p.Allocations).FirstAsync(p => p.Id == id, cancellationToken);
    if (payment.Status == PaymentStatus.Reversed)
      return Result<ReversePaymentResult>.Failure($"The payment was already reversed on {payment.ReversedAt:dd MMM yyyy}.");

    await ledger.ReverseAsync(payment, command.Reason, actor, clock.UtcNow, clock.Today, cancellationToken);
    audit.Record("Reversed", payment, $"{Rupees.Text(payment.Amount)}: {command.Reason.Trim()}");

    var warnings = new List<string>();
    decimal? duesAfter = null;
    if (payment.TaxFileId is { } f)
    {
      var file = await context.LoadTaxFileAsync(f.Value, cancellationToken);
      if (!payment.IsFeeReceipt)
        await messenger.SyncAsync(file, PsidSyncReason.Reversal, null, null, null,
          $"{Rupees.Text(payment.Amount)}{(payment.ReceiptNo is null ? "" : " receipt " + payment.ReceiptNo)} reversed: {command.Reason.Trim()}", cancellationToken);
      duesAfter = await ledger.DuesAsync(file, cancellationToken);
      var since = DateOnly.FromDateTime(payment.PaidAt.AddHours(5));
      var clearances = await context.Clearances.AsNoTracking()
        .Where(c => c.TaxFileId == file.Id && c.IssuedOn >= since && c.Status == ClearanceStatus.Issued && c.ValidUntil >= clock.Today)
        .Select(c => c.ClearanceNo).ToListAsync(cancellationToken);
      foreach (var no in clearances)
        warnings.Add($"Clearance {no} was issued while this payment counted; revoke it if it should not stand.");
    }

    await context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Result<ReversePaymentResult>.Success(new(payment.Id.Value, payment.Amount, duesAfter, warnings));
  }

  public async Task<Result<GetPaymentsQueryResult>> Handle(GetPaymentsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Payments.AsNoTracking().Include(p => p.Allocations).AsQueryable().InScope(context);
    if (query.TaxFileId is { } f)
    {
      var fileId = TaxFileId.Of(f);
      rows = rows.Where(p => p.TaxFileId == fileId);
    }
    if (query.PartyId is { } party)
    {
      var partyId = PartyId.Of(party);
      rows = rows.Where(p => p.PartyId == partyId);
    }
    if (query.FeeId is { } fee)
    {
      var feeId = FeeId.Of(fee);
      rows = rows.Where(p => p.FeeId == feeId);
    }
    if (query.CircleId is { } circle)
    {
      var circleId = TaxCircleId.Of(circle);
      var inCircle = context.TaxFiles.Where(x => x.CircleId == circleId).Select(x => x.Id);
      rows = rows.Where(p => p.TaxFileId != null && inCircle.Contains(p.TaxFileId));
    }
    if (query.Channel is { } channel)
      rows = rows.Where(p => p.Channel == channel);
    if (query.Kind is { } kind)
      rows = kind == PaymentKind.Fee ? rows.Where(p => p.FeeId != null) : rows.Where(p => p.FeeId == null);
    if (query.Status is { } status)
      rows = rows.Where(p => p.Status == status);
    if (query.ReceivedBy is { } cashier)
      rows = rows.Where(p => p.ReceivedBy == cashier);
    if (query.From is { } from)
    {
      var start = Pkt.StartOf(from);
      rows = rows.Where(p => p.PaidAt >= start);
    }
    if (query.To is { } to)
    {
      var end = Pkt.StartOf(to.AddDays(1));
      rows = rows.Where(p => p.PaidAt < end);
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      // a 1Link reference is often typed as printed, in groups of four
      var reference = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
      var files = context.TaxFiles.Where(x => x.FileNo.ToUpper().Contains(s)).Select(x => x.Id);
      rows = rows.Where(p => (p.Psid != null && p.Psid.Contains(reference)) || (p.TransactionRef != null && p.TransactionRef.ToUpper().Contains(s))
        || (p.ReceiptNo != null && p.ReceiptNo.ToUpper().Contains(s)) || (p.PayerName != null && p.PayerName.ToUpper().Contains(s))
        || (p.TaxFileId != null && files.Contains(p.TaxFileId)));
    }
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(p => p.PaidAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await DtosAsync(page, cancellationToken);
    return Result<GetPaymentsQueryResult>.Success(new(new PaginatedResult<PaymentDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetPaymentQueryResult>> Handle(GetPaymentQuery query, CancellationToken cancellationToken)
  {
    var id = PaymentId.Of(query.Id);
    var payment = await context.Payments.AsNoTracking().Include(p => p.Allocations).AsQueryable().InScope(context).FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
      ?? throw new PaymentNotFoundException($"Payment {query.Id} was not found.");
    return Result<GetPaymentQueryResult>.Success(new((await DtosAsync([payment], cancellationToken))[0]));
  }

  public async Task<Result<ReferenceDto>> Handle(GetReferenceQuery query, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileByPsidAsync(query.Psid, cancellationToken);
    var demands = (await ledger.DemandsAsync(file.Id, cancellationToken)).Where(d => d.IsOpen).OrderBy(d => d.FiscalYear).ToList();
    var opening = demands.Count == 0 && !(await ledger.DemandsAsync(file.Id, cancellationToken)).Any(d => d.IsIssued)
      ? (await ledger.UnbilledImportsAsync(file.Id, cancellationToken)).Sum(a => a.Outstanding) : 0m;
    var payer = file.Addressee(clock.Today);
    return Result<ReferenceDto>.Success(new ReferenceDto(file.Psid!, Numbers.FormatPsid(file.Psid), file.Id.Value, file.FileNo, file.PropertyCode, payer?.Name,
      await ledger.DuesAsync(file, cancellationToken), demands.Select(d => d.DueDate).Where(d => d is not null).DefaultIfEmpty(null).Min(),
      demands.Select(d => new ReferenceYearDto(d.Id.Value, d.DemandNo, d.FiscalYear, FiscalYear.Label(d.FiscalYear), d.Status, d.Balance, d.DueDate)).ToList(), opening));
  }

  public async Task<Result<GetSmsMessagesQueryResult>> Handle(GetSmsMessagesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.SmsMessages.AsNoTracking().InScope(context);
    if (query.TaxFileId is { } f)
    {
      var fileId = TaxFileId.Of(f);
      rows = rows.Where(m => m.TaxFileId == fileId);
    }
    if (query.DemandId is { } d)
    {
      var demandId = DemandId.Of(d);
      rows = rows.Where(m => m.DemandId == demandId);
    }
    if (query.Status is { } status)
      rows = rows.Where(m => m.DeliveryStatus == status);
    if (query.From is { } from)
    {
      var start = Pkt.StartOf(from);
      rows = rows.Where(m => m.QueuedAt >= start);
    }
    if (query.To is { } to)
    {
      var end = Pkt.StartOf(to.AddDays(1));
      rows = rows.Where(m => m.QueuedAt < end);
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      var files = context.TaxFiles.Where(x => x.FileNo.ToUpper().Contains(s)).Select(x => x.Id);
      rows = rows.Where(m => m.ToMobile.Contains(s) || m.Body.ToUpper().Contains(s) || (m.TaxFileId != null && files.Contains(m.TaxFileId)));
    }
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(m => m.QueuedAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var templates = await context.MessageTemplates.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Code, cancellationToken);
    var fileIds = page.Where(m => m.TaxFileId is not null).Select(m => m.TaxFileId!).Distinct().ToList();
    var fileNos = await context.TaxFiles.AsNoTracking().Where(x => fileIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FileNo, cancellationToken);
    var data = page.Select(m => new SmsDto(m.Id.Value, m.TaxFileId?.Value, m.TaxFileId is { } fid ? fileNos.GetValueOrDefault(fid) : null, m.DemandId?.Value, m.NoticeId?.Value,
      m.ToMobile, templates.GetValueOrDefault(m.TemplateId) ?? "", m.Body, m.QueuedAt, m.SentAt, m.DeliveryStatus)).ToList();
    return Result<GetSmsMessagesQueryResult>.Success(new(new PaginatedResult<SmsDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
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

  private async Task<List<PaymentDto>> DtosAsync(IReadOnlyList<Payment> payments, CancellationToken cancellationToken) =>
      await PaymentDtos.ForAsync(context, setup, payments, cancellationToken);

  private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// Payment rows as the registers, the receipt and the file show them.
public static class PaymentDtos
{
  public static async Task<List<PaymentDto>> ForAsync(IApplicationDbContext context, TaxSetup setup, IReadOnlyList<Payment> payments, CancellationToken cancellationToken)
  {
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var fileIds = payments.Where(p => p.TaxFileId is not null).Select(p => p.TaxFileId!).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    var feeIds = payments.Where(p => p.FeeId is not null).Select(p => p.FeeId!).Distinct().ToList();
    var fees = await context.Fees.AsNoTracking().Where(f => feeIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => new { f.FeeNo, f.HeadId }, cancellationToken);
    var partyIds = payments.Where(p => p.PartyId is not null).Select(p => p.PartyId!).Distinct().ToList();
    var parties = await context.Parties.AsNoTracking().Where(x => partyIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

    // the fiscal year each allocation paid (its line, row or penalty's demand), for the receipt's "for FY" column
    var lineIds = payments.SelectMany(p => p.Allocations).Where(a => a.DemandLineId is not null).Select(a => a.DemandLineId!).Distinct().ToList();
    var penaltyIds = payments.SelectMany(p => p.Allocations).Where(a => a.PenaltyId is not null).Select(a => a.PenaltyId!).Distinct().ToList();
    var arrearsIds = payments.SelectMany(p => p.Allocations).Where(a => a.ArrearsId is not null).Select(a => a.ArrearsId!).Distinct().ToList();
    var lineYears = await context.DemandLines.AsNoTracking().Where(l => lineIds.Contains(l.Id))
      .Join(context.Demands, l => l.DemandId, d => d.Id, (l, d) => new { l.Id, d.FiscalYear }).ToDictionaryAsync(x => x.Id.Value, x => (int?)x.FiscalYear, cancellationToken);
    var penaltyYears = await context.Penalties.AsNoTracking().Where(p => penaltyIds.Contains(p.Id))
      .Join(context.Demands, p => p.DemandId, d => d.Id, (p, d) => new { p.Id, d.FiscalYear }).ToDictionaryAsync(x => x.Id.Value, x => (int?)x.FiscalYear, cancellationToken);
    var arrearsYears = await context.ArrearsRows.AsNoTracking().Where(a => arrearsIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id.Value, a => a.FiscalYear, cancellationToken);
    int? YearOf(PaymentAllocation a) => a.DemandLineId is { } l ? lineYears.GetValueOrDefault(l.Value)
      : a.PenaltyId is { } p ? penaltyYears.GetValueOrDefault(p.Value)
      : a.ArrearsId is { } r ? arrearsYears.GetValueOrDefault(r.Value) : null;

    return payments.Select(p =>
    {
      var fee = p.FeeId is { } fid ? fees.GetValueOrDefault(fid) : null;
      return new PaymentDto(p.Id.Value, p.FeeId is null ? PaymentKind.Demand : PaymentKind.Fee, p.TaxFileId?.Value, p.TaxFileId is { } tf ? files.GetValueOrDefault(tf) : null,
        p.Psid, p.PartyId?.Value, p.PartyId is { } pid ? parties.GetValueOrDefault(pid) : null, p.FeeId?.Value, fee?.FeeNo, p.PayerName, p.Channel, p.ReceiptNo, p.ReceivedBy,
        p.BankCode, p.TransactionRef, p.PaidAt, p.Amount, p.PostedAt, p.Status, p.ReversedAt, p.ReversedBy, p.ReversalReason,
        p.Allocations.Select(a => new AllocationDto(a.Id.Value, a.HeadId.Value, heads.GetValueOrDefault(a.HeadId)?.Code ?? "", heads.GetValueOrDefault(a.HeadId)?.Name ?? "",
          a.DemandLineId?.Value, a.ArrearsId?.Value, a.PenaltyId?.Value, a.FeeId?.Value, YearOf(a), a.Amount)).ToList());
    }).ToList();
  }
}

/// Pakistan time (UTC+5, no daylight saving): the day a payment or message falls on.
public static class Pkt
{
  public static DateTime StartOf(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-5);

  public static DateOnly DayOf(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(5));
}
