using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- parties ----

public sealed record PartyInput(PartyType PartyType, string Name, string? CnicNtn, string? Mobile, string? Address, Guid? OwnerId, Guid? EmployeeId);

public sealed record GetPartiesQueryResult(PaginatedResult<PartyDto> Parties);
public sealed record GetPartiesQuery(PaginationRequest Pagination, string? Search, PartyType? PartyType, bool IncludeInactive = false) : IQuery<Result<GetPartiesQueryResult>>;
public sealed record GetPartyQueryResult(PartyDto Party);
public sealed record GetPartyQuery(Guid Id) : IQuery<Result<GetPartyQueryResult>>;
public sealed record CreatePartyCommand(PartyInput Party) : ICommand<Result<CreatedResult>>;
public sealed record UpdatePartyCommand(Guid Id, PartyInput Party) : ICommand<Result<UpdatedResult>>;
public sealed record SetPartyActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

/// Deletes a party registered by mistake: only while no fee was ever charged to it.
public sealed record DeletePartyCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class PartyInputValidator : AbstractValidator<PartyInput>
{
  public PartyInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    RuleFor(x => x.CnicNtn).MaximumLength(20);
    RuleFor(x => x.Mobile).MaximumLength(30);
    RuleFor(x => x.Address).MaximumLength(400);
  }
}

public class CreatePartyCommandValidator : AbstractValidator<CreatePartyCommand>
{
  public CreatePartyCommandValidator() => RuleFor(x => x.Party).NotNull().SetValidator(new PartyInputValidator());
}

public class UpdatePartyCommandValidator : AbstractValidator<UpdatePartyCommand>
{
  public UpdatePartyCommandValidator() => RuleFor(x => x.Party).NotNull().SetValidator(new PartyInputValidator());
}

// ---- fees ----

/// Quantity: kanal, sq ft, marla, pages ... as the head's rate is charged on; Value: the land value for a percentage
/// fee; ExtensionMonths: for the non-utilization fee of a building-plan extension (charged for those months only).
public sealed record FeeBasisInput(string HeadCode, Guid? TaxFileId, decimal? Quantity, decimal? Value, int? ExtensionMonths);

/// How a fee is paid at the counter: the channel, the manual receipt no. (issued when not given) or the bank's
/// transaction reference, the bank and when.
public sealed record FeePaymentInput(PaymentChannel Channel, string? ReceiptNo, string? TransactionRef, string? BankCode, DateTime? PaidAt);

public sealed record QuoteFeeQuery(FeeBasisInput Basis) : IQuery<Result<FeeQuoteDto>>;

/// Charges a fee (DUE). PayNow takes its receipt at once (needs TAX.POST); an exemption reason waives it at once (needs
/// TAX.WAIVE, as waiving later does, and - while the duty rule "Fees are waived by someone else" is on - is refused: the
/// person who charges a fee does not exempt it).
public sealed record ChargeFeeCommand(FeeBasisInput Basis, Guid? PartyId, FeeSourceType? SourceType, Guid? SourceId, string? SourceNo, string? ExemptionReason,
  FeePaymentInput? PayNow) : ICommand<Result<ChargeFeeResult>>;
public sealed record ChargeFeeResult(Guid Id, string FeeNo, decimal Amount, FeeStatus Status, Guid? PaymentId, string? ReceiptNo);

public sealed record GetFeesQueryResult(PaginatedResult<FeeDto> Fees);
public sealed record GetFeesQuery(PaginationRequest Pagination, Guid? TaxFileId, Guid? PartyId, string? HeadCode, FeeStatus? Status, string? Search = null,
  DateOnly? From = null, DateOnly? To = null) : IQuery<Result<GetFeesQueryResult>>;
public sealed record GetFeeQueryResult(FeeDto Fee);
public sealed record GetFeeQuery(Guid Id) : IQuery<Result<GetFeeQueryResult>>;

/// Takes a fee's receipt: a payment row for its whole amount.
public sealed record PayFeeCommand(Guid Id, FeePaymentInput Payment) : ICommand<Result<PayFeeResult>>;
public sealed record PayFeeResult(Guid FeeId, Guid PaymentId, string? ReceiptNo, decimal Amount);
public sealed record WaiveFeeCommand(Guid Id, string Reason) : ICommand<Result<UpdatedResult>>;

public class FeeBasisInputValidator : AbstractValidator<FeeBasisInput>
{
  public FeeBasisInputValidator()
  {
    RuleFor(x => x.HeadCode).NotEmpty().MaximumLength(30);
    RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).When(x => x.Quantity.HasValue);
    RuleFor(x => x.Value).GreaterThanOrEqualTo(0).When(x => x.Value.HasValue);
    RuleFor(x => x.ExtensionMonths).InclusiveBetween(1, 120).When(x => x.ExtensionMonths.HasValue);
  }
}

public class FeePaymentInputValidator : AbstractValidator<FeePaymentInput>
{
  public FeePaymentInputValidator()
  {
    RuleFor(x => x.ReceiptNo).MaximumLength(40);
    RuleFor(x => x.TransactionRef).MaximumLength(60);
    RuleFor(x => x.BankCode).MaximumLength(20);
  }
}

public class QuoteFeeQueryValidator : AbstractValidator<QuoteFeeQuery>
{
  public QuoteFeeQueryValidator() => RuleFor(x => x.Basis).NotNull().SetValidator(new FeeBasisInputValidator());
}

public class ChargeFeeCommandValidator : AbstractValidator<ChargeFeeCommand>
{
  public ChargeFeeCommandValidator()
  {
    RuleFor(x => x.Basis).NotNull().SetValidator(new FeeBasisInputValidator());
    RuleFor(x => x.SourceNo).MaximumLength(50);
    RuleFor(x => x.ExemptionReason).MaximumLength(200);
    RuleFor(x => x.PayNow!).SetValidator(new FeePaymentInputValidator()).When(x => x.PayNow is not null);
    RuleFor(x => x).Must(x => x.PayNow is null || string.IsNullOrWhiteSpace(x.ExemptionReason))
      .WithMessage("A fee is either waived (with the exemption reason) or paid, not both.");
  }
}

public class PayFeeCommandValidator : AbstractValidator<PayFeeCommand>
{
  public PayFeeCommandValidator() => RuleFor(x => x.Payment).NotNull().SetValidator(new FeePaymentInputValidator());
}

public class WaiveFeeCommandValidator : AbstractValidator<WaiveFeeCommand>
{
  public WaiveFeeCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
}

// ---- clearances ----

/// A clearance for a transfer or a building plan, issued only when nothing is outstanding on the file (no balance behind
/// its reference, no fee due).
public sealed record IssueClearanceCommand(Guid TaxFileId, ClearancePurpose Purpose, Guid? PropertyRefId, string? PropertyRefNo, Guid? FeeId) : ICommand<Result<CreatedResult>>;
/// Count: every clearance the filters match; Clearances: the page asked for (newest first).
public sealed record GetClearancesQueryResult(IReadOnlyList<ClearanceDto> Clearances, long Count);
public sealed record GetClearancesQuery(Guid? TaxFileId, ClearanceStatus? Status, Guid? PropertyId = null, string? Search = null, DateOnly? From = null, DateOnly? To = null,
  int? ExpiringWithinDays = null, PaginationRequest? Pagination = null) : IQuery<Result<GetClearancesQueryResult>>;
public sealed record GetClearanceQueryResult(ClearanceDto Clearance);
public sealed record GetClearanceQuery(Guid Id) : IQuery<Result<GetClearanceQueryResult>>;
/// The certificate with this number (CLR-00012), for Property's transfer and plan forms.
public sealed record GetClearanceByNumberQuery(string ClearanceNo) : IQuery<Result<GetClearanceQueryResult>>;
/// Used once by the transfer or plan it was issued for. PropertyId and Purpose, when given, must match the certificate.
public sealed record ConsumeClearanceCommand(Guid Id, Guid? PropertyId = null, ClearancePurpose? Purpose = null, Guid? PropertyRefId = null, string? PropertyRefNo = null)
  : ICommand<Result<UpdatedResult>>;
/// Revokes a clearance (issued in error, or no longer true), with the reason the trail keeps.
public sealed record RevokeClearanceCommand(Guid Id, string Reason) : ICommand<Result<UpdatedResult>>;

public class RevokeClearanceCommandValidator : AbstractValidator<RevokeClearanceCommand>
{
  public RevokeClearanceCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the clearance is revoked.").MaximumLength(300);
}

public class FeeHandlers(
  IApplicationDbContext context,
  TaxSetup setup,
  Ledger ledger,
  Messenger messenger,
  CodeIssuer codes,
  ICurrentUser currentUser,
  IDutyRules dutyRules,
  IAuditTrail audit,
  IClock clock) :
  IQueryHandler<GetPartiesQuery, Result<GetPartiesQueryResult>>,
  IQueryHandler<GetPartyQuery, Result<GetPartyQueryResult>>,
  ICommandHandler<CreatePartyCommand, Result<CreatedResult>>,
  ICommandHandler<UpdatePartyCommand, Result<UpdatedResult>>,
  ICommandHandler<SetPartyActivationCommand, Result<UpdatedResult>>,
  ICommandHandler<DeletePartyCommand, Result<UpdatedResult>>,
  IQueryHandler<QuoteFeeQuery, Result<FeeQuoteDto>>,
  ICommandHandler<ChargeFeeCommand, Result<ChargeFeeResult>>,
  IQueryHandler<GetFeesQuery, Result<GetFeesQueryResult>>,
  IQueryHandler<GetFeeQuery, Result<GetFeeQueryResult>>,
  ICommandHandler<PayFeeCommand, Result<PayFeeResult>>,
  ICommandHandler<WaiveFeeCommand, Result<UpdatedResult>>,
  ICommandHandler<IssueClearanceCommand, Result<CreatedResult>>,
  IQueryHandler<GetClearancesQuery, Result<GetClearancesQueryResult>>,
  IQueryHandler<GetClearanceQuery, Result<GetClearanceQueryResult>>,
  IQueryHandler<GetClearanceByNumberQuery, Result<GetClearanceQueryResult>>,
  ICommandHandler<ConsumeClearanceCommand, Result<UpdatedResult>>,
  ICommandHandler<RevokeClearanceCommand, Result<UpdatedResult>>
{
  // ---- parties ----

  public async Task<Result<GetPartiesQueryResult>> Handle(GetPartiesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Parties.AsNoTracking();
    if (!query.IncludeInactive)
      rows = rows.Where(p => p.IsActive);
    if (query.PartyType is { } type)
      rows = rows.Where(p => p.PartyType == type);
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      // a CNIC or a mobile is found however its dashes were typed
      var digits = new string(s.Where(char.IsAsciiDigit).ToArray());
      var byDigits = digits.Length >= 4 && digits.Length == s.Count(c => c != '-' && !char.IsWhiteSpace(c));
      rows = rows.Where(p => p.Name.ToUpper().Contains(s)
        || (p.CnicNtn != null && (p.CnicNtn.Contains(s) || (byDigits && p.CnicNtn.Replace("-", "").Contains(digits))))
        || (p.Mobile != null && (p.Mobile.Contains(s) || (byDigits && p.Mobile.Replace("-", "").Replace(" ", "").Contains(digits)))));
    }
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderBy(p => p.Name).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    return Result<GetPartiesQueryResult>.Success(new(new PaginatedResult<PartyDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total,
      await PartyDtosAsync(page, cancellationToken))));
  }

  public async Task<Result<GetPartyQueryResult>> Handle(GetPartyQuery query, CancellationToken cancellationToken)
  {
    var party = await context.LoadPartyAsync(query.Id, cancellationToken);
    return Result<GetPartyQueryResult>.Success(new((await PartyDtosAsync([party], cancellationToken))[0]));
  }

  public async Task<Result<CreatedResult>> Handle(CreatePartyCommand command, CancellationToken cancellationToken)
  {
    var p = command.Party;
    var party = Party.Create(PartyId.New(), p.PartyType, p.Name, p.CnicNtn, p.Mobile, p.Address, p.OwnerId, p.EmployeeId);
    if (await SameCnicAsync(party, cancellationToken) is { } other)
      return Result<CreatedResult>.Failure($"{other.Name} is already registered with CNIC / NTN {party.CnicNtn}. Charge the fee to that party.");
    context.Parties.Add(party);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(party.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdatePartyCommand command, CancellationToken cancellationToken)
  {
    var party = await context.LoadPartyAsync(command.Id, cancellationToken);
    var p = command.Party;
    if (p.PartyType != party.PartyType)
      return Result<UpdatedResult>.Failure("The kind of party cannot change.");
    party.Update(p.Name, p.CnicNtn, p.Mobile, p.Address, p.OwnerId, p.EmployeeId);
    if (await SameCnicAsync(party, cancellationToken) is { } other)
      return Result<UpdatedResult>.Failure($"{other.Name} is already registered with CNIC / NTN {party.CnicNtn}.");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetPartyActivationCommand command, CancellationToken cancellationToken)
  {
    var party = await context.LoadPartyAsync(command.Id, cancellationToken);
    if (!command.IsActive && await context.Fees.AnyAsync(f => f.PartyId == party.Id && f.Status == FeeStatus.Due, cancellationToken))
      return Result<UpdatedResult>.Failure($"{party.Name} still has fees due; take or waive them first.");
    party.SetActive(command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeletePartyCommand command, CancellationToken cancellationToken)
  {
    var party = await context.LoadPartyAsync(command.Id, cancellationToken);
    if (await context.Fees.AnyAsync(f => f.PartyId == party.Id, cancellationToken) || await context.Payments.AnyAsync(p => p.PartyId == party.Id, cancellationToken))
      return Result<UpdatedResult>.Failure($"{party.Name} has fees on record; deactivate the party instead.");
    context.Parties.Remove(party);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<Party?> SameCnicAsync(Party party, CancellationToken cancellationToken)
  {
    if (party.CnicNtn is not { } cnic)
      return null;
    var digits = new string(cnic.Where(char.IsAsciiDigit).ToArray());
    return await context.Parties.AsNoTracking()
      .FirstOrDefaultAsync(p => p.Id != party.Id && p.CnicNtn != null && p.CnicNtn.Replace("-", "") == digits, cancellationToken);
  }

  private async Task<List<PartyDto>> PartyDtosAsync(IReadOnlyList<Party> parties, CancellationToken cancellationToken)
  {
    var ids = parties.Select(p => p.Id).ToList();
    var fees = await context.Fees.AsNoTracking().Where(f => f.PartyId != null && ids.Contains(f.PartyId))
      .GroupBy(f => f.PartyId).Select(g => new { g.Key, Count = g.Count(), Due = g.Where(f => f.Status == FeeStatus.Due).Sum(f => f.Amount) })
      .ToListAsync(cancellationToken);
    return parties.Select(p =>
    {
      var f = fees.FirstOrDefault(x => x.Key == p.Id);
      return new PartyDto(p.Id.Value, p.PartyType, p.Name, p.CnicNtn, p.Mobile, p.Address, p.OwnerId, p.EmployeeId, p.IsActive, f?.Count ?? 0, f?.Due ?? 0m, p.CreatedAt);
    }).ToList();
  }

  // ---- fees ----

  public async Task<Result<FeeQuoteDto>> Handle(QuoteFeeQuery query, CancellationToken cancellationToken)
  {
    var (head, _, quote, set) = await QuoteAsync(query.Basis, cancellationToken);
    return Result<FeeQuoteDto>.Success(new FeeQuoteDto(head.Code, head.Name, quote.Amount, quote.Explanation, quote.Quantity, quote.Unit, quote.RowId?.Value, set.Name));
  }

  public async Task<Result<ChargeFeeResult>> Handle(ChargeFeeCommand command, CancellationToken cancellationToken)
  {
    var exempt = !string.IsNullOrWhiteSpace(command.ExemptionReason);
    if (exempt && !currentUser.HasPermission(PermissionCatalog.Tax.Waive))
      return Result<ChargeFeeResult>.Failure("Charging a fee as exempt waives it, which needs the right to waive fees (TAX.WAIVE). Charge it due and ask an officer to waive it.");
    if (exempt && await dutyRules.SeparationRefusalAsync(DutyRules.TaxFeeWaive, currentUser.UserId, currentUser.UserId, "exempt a fee you are charging",
          cancellationToken) is { } refusal)
      return Result<ChargeFeeResult>.Failure(refusal + " Charge it due; another officer waives it.");
    if (command.PayNow is not null && !currentUser.HasPermission(PermissionCatalog.Tax.Post))
      return Result<ChargeFeeResult>.Failure("Taking the fee's receipt needs the cashier's right (TAX.POST). Charge it due; the cashier takes the receipt.");

    var (head, file, quote, _) = await QuoteAsync(command.Basis, cancellationToken);
    Party? party = command.PartyId is { } pid ? await context.LoadPartyAsync(pid, cancellationToken) : null;
    if (party is { IsActive: false })
      return Result<ChargeFeeResult>.Failure($"{party.Name} is deactivated; activate the party to charge it a fee.");
    var fee = Fee.Charge(FeeId.New(), Numbers.FeeNo(await codes.NextAsync(NumberSequence.FeeNo, cancellationToken)), head, file?.Id, party?.Id, command.SourceType,
      command.SourceId, command.SourceNo, quote.Quantity, quote.Unit, quote.RowId, quote.Amount, command.ExemptionReason);
    context.Fees.Add(fee);

    Payment? payment = null;
    if (command.PayNow is { } pay && fee.Status == FeeStatus.Due)
      payment = await TakeReceiptAsync(fee, file, party, pay, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<ChargeFeeResult>.Success(new(fee.Id.Value, fee.FeeNo, fee.Amount, fee.Status, payment?.Id.Value, payment?.ReceiptNo));
  }

  public async Task<Result<GetFeesQueryResult>> Handle(GetFeesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Fees.AsNoTracking().InScope(context);
    if (query.TaxFileId is { } f)
    {
      var fileId = TaxFileId.Of(f);
      rows = rows.Where(x => x.TaxFileId == fileId);
    }
    if (query.PartyId is { } p)
    {
      var partyId = PartyId.Of(p);
      rows = rows.Where(x => x.PartyId == partyId);
    }
    if (!string.IsNullOrWhiteSpace(query.HeadCode))
    {
      var code = query.HeadCode.Trim().ToUpperInvariant();
      var headIds = context.TaxHeads.Where(h => h.Code == code).Select(h => h.Id);
      rows = rows.Where(x => headIds.Contains(x.HeadId));
    }
    if (query.Status is { } status)
      rows = rows.Where(x => x.Status == status);
    if (query.From is { } from)
    {
      var start = Pkt.StartOf(from);
      rows = rows.Where(x => x.CreatedAt >= start);
    }
    if (query.To is { } to)
    {
      var end = Pkt.StartOf(to.AddDays(1));
      rows = rows.Where(x => x.CreatedAt < end);
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      var files = context.TaxFiles.Where(x => x.FileNo.ToUpper().Contains(s) || x.PropertyCode.ToUpper().Contains(s)).Select(x => x.Id);
      var parties = context.Parties.Where(x => x.Name.ToUpper().Contains(s)).Select(x => x.Id);
      rows = rows.Where(x => x.FeeNo.ToUpper().Contains(s) || (x.SourceNo != null && x.SourceNo.ToUpper().Contains(s))
        || (x.TaxFileId != null && files.Contains(x.TaxFileId)) || (x.PartyId != null && parties.Contains(x.PartyId)));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(x => x.CreatedAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    return Result<GetFeesQueryResult>.Success(new(new PaginatedResult<FeeDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total,
      await FeeDtosAsync(page, cancellationToken))));
  }

  public async Task<Result<GetFeeQueryResult>> Handle(GetFeeQuery query, CancellationToken cancellationToken)
  {
    var fee = await context.LoadFeeAsync(query.Id, cancellationToken);
    return Result<GetFeeQueryResult>.Success(new((await FeeDtosAsync([fee], cancellationToken))[0]));
  }

  public async Task<Result<PayFeeResult>> Handle(PayFeeCommand command, CancellationToken cancellationToken)
  {
    var fee = await context.LoadFeeAsync(command.Id, cancellationToken);
    var file = fee.TaxFileId is { } fileId ? await context.LoadTaxFileAsync(fileId.Value, cancellationToken) : null;
    var party = fee.PartyId is { } partyId ? await context.LoadPartyAsync(partyId.Value, cancellationToken) : null;
    var payment = await TakeReceiptAsync(fee, file, party, command.Payment, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<PayFeeResult>.Success(new(fee.Id.Value, payment.Id.Value, payment.ReceiptNo, payment.Amount));
  }

  public async Task<Result<UpdatedResult>> Handle(WaiveFeeCommand command, CancellationToken cancellationToken)
  {
    var fee = await context.LoadFeeAsync(command.Id, cancellationToken);
    var actor = Guard.Actor(currentUser.UserId, "waive a fee");
    if (await dutyRules.SeparationRefusalAsync(DutyRules.TaxFeeWaive, fee.CreatedBy, actor, "waive a fee you charged", cancellationToken) is { } refusal)
      return Result<UpdatedResult>.Failure(refusal);
    fee.Waive(command.Reason);
    audit.Record("Waived", fee, $"{Rupees.Text(fee.Amount)}: {command.Reason.Trim()}");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// The fee's receipt: a payment for its whole amount (the payer as on the day), and what paying it starts (the water
  /// connection fee starts the water charges from today, unless a date is already recorded).
  private async Task<Payment> TakeReceiptAsync(Fee fee, TaxFile? file, Party? party, FeePaymentInput pay, CancellationToken cancellationToken)
  {
    var manual = Payment.IsManual(pay.Channel);
    string? receipt = null;
    if (manual)
    {
      receipt = string.IsNullOrWhiteSpace(pay.ReceiptNo) ? null : pay.ReceiptNo.Trim();
      if (receipt is not null && await context.Payments.AnyAsync(p => p.ReceiptNo == receipt, cancellationToken))
        throw new DomainException($"Receipt no. {receipt} is already used. Each cash or counter receipt has its own number.");
      while (receipt is null)
      {
        var next = Numbers.ReceiptNo(await codes.NextAsync(NumberSequence.ReceiptNo, cancellationToken));
        if (!await context.Payments.AnyAsync(p => p.ReceiptNo == next, cancellationToken))
          receipt = next;
      }
    }
    var reference = string.IsNullOrWhiteSpace(pay.TransactionRef) ? null : pay.TransactionRef.Trim();
    if (reference is not null && await context.Payments.AnyAsync(p => p.TransactionRef == reference, cancellationToken))
      throw new DomainException($"Transaction {reference} is already posted.");

    var payerName = party?.Name ?? file?.Addressee(clock.Today)?.Name ?? "";
    var payment = Payment.ForFee(PaymentId.New(), fee, file, payerName, pay.Channel, reference, receipt, pay.BankCode, pay.PaidAt ?? clock.UtcNow,
      Guard.Actor(currentUser.UserId, "take a fee's receipt"), clock.UtcNow);
    context.Payments.Add(payment);

    if (file is not null)
    {
      var head = (await setup.HeadsAsync(cancellationToken)).First(h => h.Id == fee.HeadId);
      if (head.Code == HeadCodes.WaterConnection)
      {
        var water = (await setup.HeadsAsync(cancellationToken)).FirstOrDefault(h => h.Code == HeadCodes.Water && h.ChargedFrom == ChargedFrom.Facility);
        if (water is not null)
          file.TakeFacility(water, clock.Today);
      }
    }
    return payment;
  }

  private async Task<List<FeeDto>> FeeDtosAsync(IReadOnlyList<Fee> page, CancellationToken cancellationToken)
  {
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var fileIds = page.Where(x => x.TaxFileId is not null).Select(x => x.TaxFileId!).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(x => fileIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FileNo, cancellationToken);
    var partyIds = page.Where(x => x.PartyId is not null).Select(x => x.PartyId!).Distinct().ToList();
    var parties = await context.Parties.AsNoTracking().Where(x => partyIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    var feeIds = page.Select(x => x.Id).ToList();
    var receipts = await context.Payments.AsNoTracking().Where(p => p.FeeId != null && feeIds.Contains(p.FeeId) && p.Status == PaymentStatus.Posted)
      .Select(p => new { p.FeeId, p.Id, p.ReceiptNo, p.PaidAt }).ToListAsync(cancellationToken);
    return page.Select(x =>
    {
      var receipt = receipts.Where(r => r.FeeId == x.Id).OrderByDescending(r => r.PaidAt).FirstOrDefault();
      return new FeeDto(x.Id.Value, x.FeeNo, x.HeadId.Value, heads.GetValueOrDefault(x.HeadId)?.Code ?? "", heads.GetValueOrDefault(x.HeadId)?.Name ?? "",
        x.TaxFileId?.Value, x.TaxFileId is { } fid ? files.GetValueOrDefault(fid) : null, x.PartyId?.Value, x.PartyId is { } pid ? parties.GetValueOrDefault(pid) : null,
        x.SourceType, x.SourceId, x.SourceNo, x.BasisQty, x.BasisUnit, x.Amount, x.ExemptionReason, x.Status, x.CreatedAt, x.CreatedBy,
        receipt?.Id.Value, receipt?.ReceiptNo, receipt?.PaidAt);
    }).ToList();
  }

  private sealed record Quote(decimal Amount, string Explanation, decimal? Quantity, string? Unit, RateBandId? RowId);

  /// The fee of a head from the rate set in force: a flat amount, a rate per unit (from a band table when it has one), or a
  /// percentage of a value. A file's town and category pick its own rates when the rate set has them. Round up: a part
  /// unit counts as a whole one.
  private async Task<(TaxHead Head, TaxFile? File, Quote Quote, RateSet Set)> QuoteAsync(FeeBasisInput input, CancellationToken cancellationToken)
  {
    var head = await setup.HeadAsync(input.HeadCode.Trim().ToUpperInvariant(), cancellationToken);
    if (head.HeadKind != HeadKind.Other || head.ChargedOnNotice)
      throw new DomainException($"{head.Name} is not a fee{(head.HeadKind == HeadKind.Regular ? "; it is part of the annual demand" : "; it is charged on notices")}.");
    var file = input.TaxFileId is { } fid ? await context.LoadTaxFileAsync(fid, cancellationToken) : null;
    var set = await setup.RateSetOnAsync(clock.Today, cancellationToken);
    var category = file?.TaxCategory is TaxCategory.Com or TaxCategory.Res ? file.TaxCategory : (TaxCategory?)null;
    var rows = set.HeadRows(head.Id, category, file?.TownId);
    if (rows.Count == 0)
      throw new DomainException($"'{set.Name}' has no rate for {head.Name}{(file is null ? "" : " for this property")}.");

    var first = rows[0];
    var unit = first.Basis switch { RateBasis.Fixed or RateBasis.PercentOfValue => null, RateBasis.Custom => "CUSTOM", _ => EnumText.UpperSnake(first.Basis) };
    var unitText = first.Basis == RateBasis.Custom ? first.UnitLabel ?? "units" : EnumText.Words(first.Basis);
    switch (first.RateType)
    {
      case RateType.Percent:
      {
        var value = input.Value ?? throw new DomainException($"Give the value {head.Name} is charged on.");
        var rate = first.Rate;
        return (head, file, new Quote(Rupees.Round(value * rate / 100m), $"{Rupees.Number(rate)} % of {Rupees.Text(value)}", value, null, first.Id), set);
      }
      case RateType.Flat when first.Basis == RateBasis.Fixed && rows.Count == 1:
        return (head, file, new Quote(Rupees.Round(first.Rate), "fixed", null, null, first.Id), set);
      default:
      {
        var given = input.Quantity ?? throw new DomainException($"Give the {unitText} {head.Name} is charged on.");
        var qty = first.RoundUp ? Math.Ceiling(given) : given;
        var shown = $"{Rupees.Number(given)} {unitText}{(qty != given ? $" (rounded up to {Rupees.Number(qty)})" : "")}";
        if (rows.All(r => r.RateType == RateType.Flat))
        {
          // a band table: the band's amount, and the rule above its top band
          var (amount, row, note) = TaxCalculator.BandAbove(rows, qty);
          return (head, file, new Quote(Rupees.Round(amount), $"{shown}: band amount{(note.Length > 0 ? "; " + note : "")}", given, unit, row.Id), set);
        }
        var band = rows.FirstOrDefault(b => b.BandTo is null || qty <= b.BandTo) ?? rows[^1];
        if (band.RateType == RateType.Flat)
          return (head, file, new Quote(Rupees.Round(band.Rate), $"{shown}: band amount", given, unit, band.Id), set);
        if (head.Code == HeadCodes.NonUtilization && input.ExtensionMonths is { } months)
          return (head, file, new Quote(Rupees.Round(band.Rate * qty * months / 12m),
            $"plan extension only: {shown} × {Rupees.Number(band.Rate)} × {months}/12 months", given, unit, band.Id), set);
        return (head, file, new Quote(Rupees.Round(band.Rate * qty), $"{shown} × {Rupees.Number(band.Rate)}", given, unit, band.Id), set);
      }
    }
  }

  // ---- clearances ----

  public async Task<Result<CreatedResult>> Handle(IssueClearanceCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.TaxFileId, cancellationToken);
    var dues = await ledger.DuesAsync(file, cancellationToken);
    var feesDue = await ledger.FeesDueAsync(file.Id, cancellationToken);
    FeeId? feeId = command.FeeId is { } f ? (await context.LoadFeeAsync(f, cancellationToken)).Id : null;
    var today = clock.Today;
    var settings = await setup.SettingsAsync(today, cancellationToken);
    var payer = file.Addressee(today);
    var clearance = Clearance.Issue(ClearanceId.New(), Numbers.ClearanceNo(await codes.NextAsync(NumberSequence.ClearanceNo, cancellationToken)), file, payer?.Name,
      command.Purpose, dues, feesDue, command.PropertyRefId, command.PropertyRefNo, feeId, today, settings.ClearanceValidDays, Guard.Actor(currentUser.UserId, "issue a clearance"));
    context.Clearances.Add(clearance);
    audit.Record("Issued", clearance, $"For {EnumText.Words(command.Purpose).ToLowerInvariant()}{(command.PropertyRefNo is { } refNo ? " " + refNo : "")}, valid until {clearance.ValidUntil:dd'/'MM'/'yyyy}");

    if (payer is not null)
    {
      await messenger.SmsAsync(MessageCodes.Clearance, file, null, null, payer.Mobile, payer.OwnerId, new Dictionary<string, string>
      {
        ["name"] = payer.Name,
        ["code"] = file.FileNo,
        ["no"] = clearance.ClearanceNo,
        ["purpose"] = EnumText.Words(command.Purpose),
        ["valid"] = clearance.ValidUntil.ToString("dd/MM/yyyy")
      }, cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(clearance.Id);
  }

  public async Task<Result<GetClearancesQueryResult>> Handle(GetClearancesQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var rows = context.Clearances.AsNoTracking().InScope(context);
    if (query.TaxFileId is { } f)
    {
      var fileId = TaxFileId.Of(f);
      rows = rows.Where(c => c.TaxFileId == fileId);
    }
    if (query.PropertyId is { } property)
    {
      var files = context.TaxFiles.Where(x => x.PropertyId == property).Select(x => x.Id);
      rows = rows.Where(c => files.Contains(c.TaxFileId));
    }
    // the status as it stands today: an issued certificate past its last day is expired
    rows = query.Status switch
    {
      ClearanceStatus.Issued => rows.Where(c => c.Status == ClearanceStatus.Issued && c.ValidUntil >= today),
      ClearanceStatus.Expired => rows.Where(c => c.Status == ClearanceStatus.Issued && c.ValidUntil < today),
      { } other => rows.Where(c => c.Status == other),
      null => rows
    };
    if (query.ExpiringWithinDays is { } days)
    {
      var last = today.AddDays(days);
      rows = rows.Where(c => c.Status == ClearanceStatus.Issued && c.ValidUntil >= today && c.ValidUntil <= last);
    }
    if (query.From is { } from)
      rows = rows.Where(c => c.IssuedOn >= from);
    if (query.To is { } to)
      rows = rows.Where(c => c.IssuedOn <= to);
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      var files = context.TaxFiles.Where(x => x.FileNo.ToUpper().Contains(s) || x.PropertyCode.ToUpper().Contains(s)).Select(x => x.Id);
      rows = rows.Where(c => c.ClearanceNo.ToUpper().Contains(s) || (c.PropertyRefNo != null && c.PropertyRefNo.ToUpper().Contains(s))
        || (c.PayerName != null && c.PayerName.ToUpper().Contains(s)) || files.Contains(c.TaxFileId));
    }

    var count = await rows.LongCountAsync(cancellationToken);
    var page = query.Pagination ?? new PaginationRequest(0, 500);
    var list = await rows.OrderByDescending(c => c.IssuedOn).ThenByDescending(c => c.CreatedAt)
      .Skip(page.Pageindex * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
    return Result<GetClearancesQueryResult>.Success(new(await ClearanceDtosAsync(list, cancellationToken), count));
  }

  public async Task<Result<GetClearanceQueryResult>> Handle(GetClearanceQuery query, CancellationToken cancellationToken)
  {
    var c = await context.LoadClearanceAsync(query.Id, cancellationToken);
    return Result<GetClearanceQueryResult>.Success(new((await ClearanceDtosAsync([c], cancellationToken))[0]));
  }

  public async Task<Result<GetClearanceQueryResult>> Handle(GetClearanceByNumberQuery query, CancellationToken cancellationToken)
  {
    var no = (query.ClearanceNo ?? "").Trim().ToUpperInvariant();
    var c = await context.Clearances.AsNoTracking().InScope(context).FirstOrDefaultAsync(x => x.ClearanceNo == no, cancellationToken)
      ?? throw new ClearanceNotFoundException($"There is no clearance {query.ClearanceNo}.");
    return Result<GetClearanceQueryResult>.Success(new((await ClearanceDtosAsync([c], cancellationToken))[0]));
  }

  private async Task<List<ClearanceDto>> ClearanceDtosAsync(IReadOnlyList<Clearance> list, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var fileIds = list.Select(c => c.TaxFileId).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(x => fileIds.Contains(x.Id))
      .ToDictionaryAsync(x => x.Id, x => new { x.FileNo, x.PropertyId, x.PropertyCode }, cancellationToken);
    return list.Select(c =>
    {
      var f = files.GetValueOrDefault(c.TaxFileId);
      return new ClearanceDto(c.Id.Value, c.ClearanceNo, c.TaxFileId.Value, f?.FileNo ?? "", f?.PropertyId ?? Guid.Empty, f?.PropertyCode ?? "", c.PayerName, c.Purpose,
        c.PropertyRefId, c.PropertyRefNo, c.OutstandingChecked, c.FeeId?.Value, c.IssuedOn, c.ValidUntil, c.IssuedBy, c.ConsumedAt, c.ConsumedBy, c.RevokedAt, c.RevokedBy,
        c.StatusOn(today));
    }).ToList();
  }

  public async Task<Result<UpdatedResult>> Handle(ConsumeClearanceCommand command, CancellationToken cancellationToken)
  {
    var clearance = await context.LoadClearanceAsync(command.Id, cancellationToken);
    if (command.PropertyId is { } property)
    {
      var file = await context.TaxFiles.AsNoTracking().Where(x => x.Id == clearance.TaxFileId).Select(x => new { x.PropertyId, x.PropertyCode }).FirstAsync(cancellationToken);
      if (file.PropertyId != property)
        return Result<UpdatedResult>.Failure($"Clearance {clearance.ClearanceNo} is for property {file.PropertyCode}, not this one.");
    }
    clearance.Consume(clock.Today, clock.UtcNow, currentUser.UserId, command.Purpose, command.PropertyRefId, command.PropertyRefNo);
    // who used it, and for what: Property's officer when the transfer or plan is approved with it
    var by = currentUser.HasPermission(PermissionCatalog.Tax.Issue) || currentUser.HasPermission(PermissionCatalog.Tax.Approve) ? "Taxation" : "Property";
    audit.Record("Used", clearance, $"By {currentUser.AuditName} ({by}) for {EnumText.Words(clearance.Purpose).ToLowerInvariant()}"
      + ((command.PropertyRefNo ?? clearance.PropertyRefNo) is { } used ? " " + used : ""));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(RevokeClearanceCommand command, CancellationToken cancellationToken)
  {
    var clearance = await context.LoadClearanceAsync(command.Id, cancellationToken);
    clearance.Revoke(clock.Today, Guard.Actor(currentUser.UserId, "revoke a clearance"), clock.UtcNow);
    audit.Record("Revoked", clearance, command.Reason.Trim());
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}
