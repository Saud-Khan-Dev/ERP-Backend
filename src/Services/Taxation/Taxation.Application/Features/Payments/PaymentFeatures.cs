using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// Money received against a property's constant 1Link reference: settled oldest year first, item by item. A 1Link, QR or
/// online payment carries the bank's transaction reference (posting it twice changes nothing); cash and bank-counter
/// payments carry the Section's manual receipt number (issued when not given).
public sealed record ReceivePaymentCommand(string Psid, decimal Amount, PaymentChannel Channel, string? TransactionRef, string? ReceiptNo, string? BankCode, DateTime? PaidAt)
  : ICommand<Result<ReceivePaymentResult>>;

public sealed record GetPaymentsQueryResult(PaginatedResult<PaymentDto> Payments);
public sealed record GetPaymentsQuery(PaginationRequest Pagination, Guid? TaxFileId, PaymentChannel? Channel, DateOnly? From, DateOnly? To, string? Search)
  : IQuery<Result<GetPaymentsQueryResult>>;
public sealed record GetPaymentQueryResult(PaymentDto Payment);
public sealed record GetPaymentQuery(Guid Id) : IQuery<Result<GetPaymentQueryResult>>;

/// What is behind a reference: the bill inquiry 1Link (or the counter) makes before taking a payment.
public sealed record GetReferenceQuery(string Psid) : IQuery<Result<ReferenceDto>>;

public sealed record GetSmsMessagesQueryResult(PaginatedResult<SmsDto> Messages);
public sealed record GetSmsMessagesQuery(PaginationRequest Pagination, Guid? TaxFileId, Guid? DemandId) : IQuery<Result<GetSmsMessagesQueryResult>>;

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

public class PaymentHandlers(
  IApplicationDbContext context,
  TaxSetup setup,
  Ledger ledger,
  Messenger messenger,
  CodeIssuer codes,
  ICurrentUser currentUser,
  IClock clock) :
  ICommandHandler<ReceivePaymentCommand, Result<ReceivePaymentResult>>,
  IQueryHandler<GetPaymentsQuery, Result<GetPaymentsQueryResult>>,
  IQueryHandler<GetPaymentQuery, Result<GetPaymentQueryResult>>,
  IQueryHandler<GetReferenceQuery, Result<ReferenceDto>>,
  IQueryHandler<GetSmsMessagesQuery, Result<GetSmsMessagesQueryResult>>
{
  public async Task<Result<ReceivePaymentResult>> Handle(ReceivePaymentCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileByPsidAsync(command.Psid, cancellationToken);

    var reference = string.IsNullOrWhiteSpace(command.TransactionRef) ? null : command.TransactionRef.Trim();
    if (reference is not null && await context.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.TransactionRef == reference, cancellationToken) is { } posted)
    {
      if (posted.TaxFileId != file.Id || posted.Amount != Rupees.Round(command.Amount))
        return Result<ReceivePaymentResult>.Failure($"Transaction {reference} was already posted for another reference or amount.");
      var dues = await ledger.DuesAsync(file, cancellationToken);
      return Result<ReceivePaymentResult>.Success(new(posted.Id.Value, true, posted.Amount, dues, dues, []));
    }

    var demands = (await ledger.DemandsAsync(file.Id, cancellationToken)).Where(d => d.IsOpen).OrderBy(d => d.FiscalYear).ToList();
    if (demands.Count == 0)
      return Result<ReceivePaymentResult>.Failure($"Nothing is billed on reference {Numbers.FormatPsid(file.Psid)}.");
    var duesBefore = demands.Sum(d => d.Balance);
    var amount = Rupees.Round(command.Amount);
    if (amount > duesBefore)
      return Result<ReceivePaymentResult>.Failure($"The amount exceeds the {Rupees.Text(duesBefore)} behind this reference.");

    var manual = Payment.IsManual(command.Channel);
    var receipt = manual ? (string.IsNullOrWhiteSpace(command.ReceiptNo) ? Numbers.ReceiptNo(await codes.NextAsync(NumberSequence.ReceiptNo, cancellationToken)) : command.ReceiptNo.Trim()) : command.ReceiptNo;
    var payment = Payment.Receive(PaymentId.New(), file, command.Channel, amount, reference, receipt, command.BankCode, command.PaidAt ?? clock.UtcNow,
      manual ? Guard.Actor(currentUser.UserId, "receive cash") : currentUser.UserId, clock.UtcNow);
    context.Payments.Add(payment);

    var surcharge = await setup.HeadAsync(HeadCodes.Surcharge, cancellationToken);
    var paid = await ledger.PaidByItemAsync(demands, cancellationToken);
    var left = amount;
    var touched = new List<Guid>();
    foreach (var demand in demands)
    {
      if (left <= 0)
        break;
      var settled = demand.Settle(left, paid, surcharge.Id, clock.Today);
      payment.Allocate(demand, settled);
      left -= settled.Sum(s => s.Amount);
      touched.Add(demand.Id.Value);
    }

    var last = demands.Last(d => touched.Contains(d.Id.Value));
    var values = Messenger.Values(file, last);
    values["amount"] = Rupees.Text(amount);
    values["balance"] = Rupees.Text(duesBefore - amount);
    var ownerId = file.Payers.FirstOrDefault(p => p.Id == last.PayerId)?.OwnerId;
    await messenger.SmsAsync(MessageCodes.PaymentReceived, file, last, null, last.PayerMobile, ownerId, values, cancellationToken);
    await messenger.SyncAsync(file, PsidSyncReason.Payment, last.Id, null, null, $"{Rupees.Text(amount)} by {EnumText.Words(command.Channel)}{(receipt is null ? "" : " receipt " + receipt)}", cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<ReceivePaymentResult>.Success(new(payment.Id.Value, false, amount, duesBefore, duesBefore - amount, touched));
  }

  public async Task<Result<GetPaymentsQueryResult>> Handle(GetPaymentsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Payments.AsNoTracking().Include(p => p.Allocations).AsQueryable();
    if (query.TaxFileId is { } f)
    {
      var fileId = TaxFileId.Of(f);
      rows = rows.Where(p => p.TaxFileId == fileId);
    }
    if (query.Channel is { } channel)
      rows = rows.Where(p => p.Channel == channel);
    if (query.From is { } from)
    {
      var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-5);
      rows = rows.Where(p => p.PaidAt >= start);
    }
    if (query.To is { } to)
    {
      var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-5);
      rows = rows.Where(p => p.PaidAt < end);
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      // a 1Link reference is often typed as printed, in groups of four
      var reference = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
      var files = context.TaxFiles.Where(f => f.FileNo.ToUpper().Contains(s)).Select(f => f.Id);
      rows = rows.Where(p => p.Psid.Contains(reference) || p.TransactionRef.ToUpper().Contains(s) || (p.ReceiptNo != null && p.ReceiptNo.ToUpper().Contains(s)) || files.Contains(p.TaxFileId));
    }
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(p => p.PaidAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await DtosAsync(page, cancellationToken);
    return Result<GetPaymentsQueryResult>.Success(new(new PaginatedResult<PaymentDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetPaymentQueryResult>> Handle(GetPaymentQuery query, CancellationToken cancellationToken)
  {
    var id = PaymentId.Of(query.Id);
    var payment = await context.Payments.AsNoTracking().Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
      ?? throw new PaymentNotFoundException($"Payment {query.Id} was not found.");
    return Result<GetPaymentQueryResult>.Success(new((await DtosAsync([payment], cancellationToken))[0]));
  }

  public async Task<Result<ReferenceDto>> Handle(GetReferenceQuery query, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileByPsidAsync(query.Psid, cancellationToken);
    var demands = (await ledger.DemandsAsync(file.Id, cancellationToken)).Where(d => d.IsOpen).OrderBy(d => d.FiscalYear).ToList();
    var payer = file.Addressee(clock.Today);
    return Result<ReferenceDto>.Success(new ReferenceDto(file.Psid!, Numbers.FormatPsid(file.Psid), file.Id.Value, file.FileNo, file.PropertyCode, payer?.Name,
      await ledger.DuesAsync(file, cancellationToken), demands.Select(d => d.DueDate).Where(d => d is not null).DefaultIfEmpty(null).Min(),
      demands.Select(d => new ReferenceYearDto(d.Id.Value, d.DemandNo, d.FiscalYear, FiscalYear.Label(d.FiscalYear), d.Status, d.Balance, d.DueDate)).ToList()));
  }

  public async Task<Result<GetSmsMessagesQueryResult>> Handle(GetSmsMessagesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.SmsMessages.AsNoTracking();
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
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(m => m.QueuedAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var templates = await context.MessageTemplates.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Code, cancellationToken);
    var data = page.Select(m => new SmsDto(m.Id.Value, m.TaxFileId?.Value, m.DemandId?.Value, m.NoticeId?.Value, m.ToMobile, templates.GetValueOrDefault(m.TemplateId) ?? "",
      m.Body, m.QueuedAt, m.SentAt, m.DeliveryStatus)).ToList();
    return Result<GetSmsMessagesQueryResult>.Success(new(new PaginatedResult<SmsDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  private async Task<List<PaymentDto>> DtosAsync(IReadOnlyList<Payment> payments, CancellationToken cancellationToken)
  {
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var fileIds = payments.Select(p => p.TaxFileId).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    return payments.Select(p => new PaymentDto(p.Id.Value, p.TaxFileId.Value, files.GetValueOrDefault(p.TaxFileId) ?? "", p.Psid, p.Channel, p.ReceiptNo, p.ReceivedBy,
      p.BankCode, p.TransactionRef, p.PaidAt, p.Amount, p.PostedAt, p.Status,
      p.Allocations.Select(a => new AllocationDto(a.Id.Value, a.HeadId.Value, heads.GetValueOrDefault(a.HeadId)?.Code ?? "", heads.GetValueOrDefault(a.HeadId)?.Name ?? "",
        a.DemandLineId?.Value, a.ArrearsId?.Value, a.PenaltyId?.Value, a.FeeId?.Value, a.Amount)).ToList())).ToList();
  }
}
