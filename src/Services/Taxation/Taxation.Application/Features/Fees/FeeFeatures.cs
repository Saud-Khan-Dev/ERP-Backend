using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- parties ----

public sealed record PartyInput(PartyType PartyType, string Name, string? CnicNtn, string? Mobile, string? Address, Guid? OwnerId, Guid? EmployeeId);

public sealed record GetPartiesQueryResult(PaginatedResult<PartyDto> Parties);
public sealed record GetPartiesQuery(PaginationRequest Pagination, string? Search, PartyType? PartyType) : IQuery<Result<GetPartiesQueryResult>>;
public sealed record CreatePartyCommand(PartyInput Party) : ICommand<Result<CreatedResult>>;
public sealed record UpdatePartyCommand(Guid Id, PartyInput Party) : ICommand<Result<UpdatedResult>>;

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

public sealed record QuoteFeeQuery(FeeBasisInput Basis) : IQuery<Result<FeeQuoteDto>>;

public sealed record ChargeFeeCommand(FeeBasisInput Basis, Guid? PartyId, FeeSourceType? SourceType, Guid? SourceId, string? SourceNo, string? ExemptionReason, bool Paid)
  : ICommand<Result<CreatedResult>>;

public sealed record GetFeesQueryResult(PaginatedResult<FeeDto> Fees);
public sealed record GetFeesQuery(PaginationRequest Pagination, Guid? TaxFileId, Guid? PartyId, string? HeadCode, FeeStatus? Status) : IQuery<Result<GetFeesQueryResult>>;
public sealed record MarkFeePaidCommand(Guid Id) : ICommand<Result<UpdatedResult>>;
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
  }
}

// ---- clearances ----

/// A clearance for a transfer or a building plan, issued only when nothing is outstanding on the file.
public sealed record IssueClearanceCommand(Guid TaxFileId, ClearancePurpose Purpose, Guid? PropertyRefId, string? PropertyRefNo, Guid? FeeId) : ICommand<Result<CreatedResult>>;
public sealed record GetClearancesQueryResult(IReadOnlyList<ClearanceDto> Clearances);
public sealed record GetClearancesQuery(Guid? TaxFileId, ClearanceStatus? Status) : IQuery<Result<GetClearancesQueryResult>>;
public sealed record GetClearanceQueryResult(ClearanceDto Clearance);
public sealed record GetClearanceQuery(Guid Id) : IQuery<Result<GetClearanceQueryResult>>;
public sealed record ConsumeClearanceCommand(Guid Id) : ICommand<Result<UpdatedResult>>;
public sealed record RevokeClearanceCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class FeeHandlers(
  IApplicationDbContext context,
  TaxSetup setup,
  Ledger ledger,
  Messenger messenger,
  CodeIssuer codes,
  ICurrentUser currentUser,
  IClock clock) :
  IQueryHandler<GetPartiesQuery, Result<GetPartiesQueryResult>>,
  ICommandHandler<CreatePartyCommand, Result<CreatedResult>>,
  ICommandHandler<UpdatePartyCommand, Result<UpdatedResult>>,
  IQueryHandler<QuoteFeeQuery, Result<FeeQuoteDto>>,
  ICommandHandler<ChargeFeeCommand, Result<CreatedResult>>,
  IQueryHandler<GetFeesQuery, Result<GetFeesQueryResult>>,
  ICommandHandler<MarkFeePaidCommand, Result<UpdatedResult>>,
  ICommandHandler<WaiveFeeCommand, Result<UpdatedResult>>,
  ICommandHandler<IssueClearanceCommand, Result<CreatedResult>>,
  IQueryHandler<GetClearancesQuery, Result<GetClearancesQueryResult>>,
  IQueryHandler<GetClearanceQuery, Result<GetClearanceQueryResult>>,
  ICommandHandler<ConsumeClearanceCommand, Result<UpdatedResult>>,
  ICommandHandler<RevokeClearanceCommand, Result<UpdatedResult>>
{
  // ---- parties ----

  public async Task<Result<GetPartiesQueryResult>> Handle(GetPartiesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Parties.AsNoTracking();
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
      page.Select(p => new PartyDto(p.Id.Value, p.PartyType, p.Name, p.CnicNtn, p.Mobile, p.Address, p.OwnerId, p.EmployeeId)).ToList())));
  }

  public async Task<Result<CreatedResult>> Handle(CreatePartyCommand command, CancellationToken cancellationToken)
  {
    var p = command.Party;
    var party = Party.Create(PartyId.New(), p.PartyType, p.Name, p.CnicNtn, p.Mobile, p.Address, p.OwnerId, p.EmployeeId);
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
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- fees ----

  public async Task<Result<FeeQuoteDto>> Handle(QuoteFeeQuery query, CancellationToken cancellationToken)
  {
    var (head, _, quote, set) = await QuoteAsync(query.Basis, cancellationToken);
    return Result<FeeQuoteDto>.Success(new FeeQuoteDto(head.Code, head.Name, quote.Amount, quote.Explanation, quote.Quantity, quote.Unit, quote.RowId?.Value, set.Name));
  }

  public async Task<Result<CreatedResult>> Handle(ChargeFeeCommand command, CancellationToken cancellationToken)
  {
    var (head, file, quote, _) = await QuoteAsync(command.Basis, cancellationToken);
    PartyId? partyId = command.PartyId is { } pid ? (await context.LoadPartyAsync(pid, cancellationToken)).Id : null;
    var fee = Fee.Charge(FeeId.New(), Numbers.FeeNo(await codes.NextAsync(NumberSequence.FeeNo, cancellationToken)), head, file?.Id, partyId, command.SourceType,
      command.SourceId, command.SourceNo, quote.Quantity, quote.Unit, quote.RowId, quote.Amount, command.ExemptionReason, command.Paid);
    context.Fees.Add(fee);

    // the water connection fee starts the water charges from today, unless a date is already recorded
    if (file is not null && head.Code == HeadCodes.WaterConnection && fee.Status == FeeStatus.Paid)
    {
      var water = (await setup.HeadsAsync(cancellationToken)).FirstOrDefault(h => h.Code == HeadCodes.Water && h.ChargedFrom == ChargedFrom.Facility);
      if (water is not null)
        file.TakeFacility(water, clock.Today);
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(fee.Id);
  }

  public async Task<Result<GetFeesQueryResult>> Handle(GetFeesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Fees.AsNoTracking();
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

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(x => x.CreatedAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var fileIds = page.Where(x => x.TaxFileId is not null).Select(x => x.TaxFileId!).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(x => fileIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FileNo, cancellationToken);
    var partyIds = page.Where(x => x.PartyId is not null).Select(x => x.PartyId!).Distinct().ToList();
    var parties = await context.Parties.AsNoTracking().Where(x => partyIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    var data = page.Select(x => new FeeDto(x.Id.Value, x.FeeNo, x.HeadId.Value, heads.GetValueOrDefault(x.HeadId)?.Code ?? "", heads.GetValueOrDefault(x.HeadId)?.Name ?? "",
      x.TaxFileId?.Value, x.TaxFileId is { } fid ? files.GetValueOrDefault(fid) : null, x.PartyId?.Value, x.PartyId is { } pid ? parties.GetValueOrDefault(pid) : null,
      x.SourceType, x.SourceId, x.SourceNo, x.BasisQty, x.BasisUnit, x.Amount, x.ExemptionReason, x.Status, x.CreatedAt)).ToList();
    return Result<GetFeesQueryResult>.Success(new(new PaginatedResult<FeeDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<UpdatedResult>> Handle(MarkFeePaidCommand command, CancellationToken cancellationToken)
  {
    var fee = await context.LoadFeeAsync(command.Id, cancellationToken);
    fee.MarkPaid();
    if (fee.TaxFileId is { } fileId)
    {
      var head = (await setup.HeadsAsync(cancellationToken)).First(h => h.Id == fee.HeadId);
      if (head.Code == HeadCodes.WaterConnection)
      {
        var water = (await setup.HeadsAsync(cancellationToken)).FirstOrDefault(h => h.Code == HeadCodes.Water && h.ChargedFrom == ChargedFrom.Facility);
        if (water is not null)
          (await context.LoadTaxFileAsync(fileId.Value, cancellationToken)).TakeFacility(water, clock.Today);
      }
    }
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(WaiveFeeCommand command, CancellationToken cancellationToken)
  {
    var fee = await context.LoadFeeAsync(command.Id, cancellationToken);
    fee.Waive(command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private sealed record Quote(decimal Amount, string Explanation, decimal? Quantity, string? Unit, RateBandId? RowId);

  /// The fee of a head from the rate set in force: a flat amount, a rate per unit (from a band table when it has one), or a
  /// percentage of a value. A file's town and category pick its own rates when the rate set has them.
  private async Task<(TaxHead Head, TaxFile? File, Quote Quote, RateSet Set)> QuoteAsync(FeeBasisInput input, CancellationToken cancellationToken)
  {
    var head = await setup.HeadAsync(input.HeadCode.Trim().ToUpperInvariant(), cancellationToken);
    if (head.HeadKind != HeadKind.Other)
      throw new DomainException($"{head.Name} is part of the annual demand, not a fee.");
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
        var qty = input.Quantity ?? throw new DomainException($"Give the {unitText} {head.Name} is charged on.");
        var band = rows.FirstOrDefault(b => b.BandTo is null || qty <= b.BandTo) ?? rows[^1];
        if (band.RateType == RateType.Flat)
          return (head, file, new Quote(Rupees.Round(band.Rate), $"{Rupees.Number(qty)} {unitText}: band amount", qty, unit, band.Id), set);
        if (head.Code == HeadCodes.NonUtilization && input.ExtensionMonths is { } months)
          return (head, file, new Quote(Rupees.Round(band.Rate * qty * months / 12m),
            $"plan extension only: {Rupees.Number(qty)} {unitText} × {Rupees.Number(band.Rate)} × {months}/12 months", qty, unit, band.Id), set);
        return (head, file, new Quote(Rupees.Round(band.Rate * qty), $"{Rupees.Number(qty)} {unitText} × {Rupees.Number(band.Rate)}", qty, unit, band.Id), set);
      }
    }
  }

  // ---- clearances ----

  public async Task<Result<CreatedResult>> Handle(IssueClearanceCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.TaxFileId, cancellationToken);
    var outstanding = await ledger.DuesAsync(file, cancellationToken);
    FeeId? feeId = command.FeeId is { } f ? (await context.LoadFeeAsync(f, cancellationToken)).Id : null;
    var settings = await setup.SettingsAsync(clock.Today, cancellationToken);
    var clearance = Clearance.Issue(ClearanceId.New(), Numbers.ClearanceNo(await codes.NextAsync(NumberSequence.ClearanceNo, cancellationToken)), file, command.Purpose,
      outstanding, command.PropertyRefId, command.PropertyRefNo, feeId, clock.Today, settings.ClearanceValidDays, Guard.Actor(currentUser.UserId, "issue a clearance"));
    context.Clearances.Add(clearance);

    if (file.Addressee(clock.Today) is { } payer)
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
    var rows = context.Clearances.AsNoTracking();
    if (query.TaxFileId is { } f)
    {
      var fileId = TaxFileId.Of(f);
      rows = rows.Where(c => c.TaxFileId == fileId);
    }
    var list = await rows.OrderByDescending(c => c.IssuedOn).ThenByDescending(c => c.CreatedAt).Take(500).ToListAsync(cancellationToken);
    var today = clock.Today;
    if (query.Status is { } status)
      list = list.Where(c => c.StatusOn(today) == status).ToList();
    var fileIds = list.Select(c => c.TaxFileId).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(x => fileIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FileNo, cancellationToken);
    return Result<GetClearancesQueryResult>.Success(new(list.Select(c => new ClearanceDto(c.Id.Value, c.ClearanceNo, c.TaxFileId.Value, files.GetValueOrDefault(c.TaxFileId) ?? "",
      c.Purpose, c.PropertyRefId, c.PropertyRefNo, c.OutstandingChecked, c.FeeId?.Value, c.IssuedOn, c.ValidUntil, c.IssuedBy, c.ConsumedAt, c.StatusOn(today))).ToList()));
  }

  public async Task<Result<GetClearanceQueryResult>> Handle(GetClearanceQuery query, CancellationToken cancellationToken)
  {
    var c = await context.LoadClearanceAsync(query.Id, cancellationToken);
    var fileNo = await context.TaxFiles.AsNoTracking().Where(x => x.Id == c.TaxFileId).Select(x => x.FileNo).FirstOrDefaultAsync(cancellationToken) ?? "";
    return Result<GetClearanceQueryResult>.Success(new(new ClearanceDto(c.Id.Value, c.ClearanceNo, c.TaxFileId.Value, fileNo, c.Purpose, c.PropertyRefId, c.PropertyRefNo,
      c.OutstandingChecked, c.FeeId?.Value, c.IssuedOn, c.ValidUntil, c.IssuedBy, c.ConsumedAt, c.StatusOn(clock.Today))));
  }

  public async Task<Result<UpdatedResult>> Handle(ConsumeClearanceCommand command, CancellationToken cancellationToken)
  {
    var clearance = await context.LoadClearanceAsync(command.Id, cancellationToken);
    clearance.Consume(clock.Today, clock.UtcNow);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(RevokeClearanceCommand command, CancellationToken cancellationToken)
  {
    var clearance = await context.LoadClearanceAsync(command.Id, cancellationToken);
    clearance.Revoke();
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

// ---- reports ----

public sealed record GetCollectionSummaryQuery(int FiscalYear) : IQuery<Result<CollectionSummaryDto>>;

public class ReportHandlers(IApplicationDbContext context) :
  IQueryHandler<GetCollectionSummaryQuery, Result<CollectionSummaryDto>>
{
  public async Task<Result<CollectionSummaryDto>> Handle(GetCollectionSummaryQuery query, CancellationToken cancellationToken)
  {
    var fy = FiscalYear.Valid(query.FiscalYear);
    var year = await context.Demands.AsNoTracking()
      .Where(d => d.FiscalYear == fy && d.Status != DemandStatus.Draft && d.Status != DemandStatus.Cancelled)
      .Select(d => new { d.TaxFileId, d.Status, d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount, d.PaidAmount, d.Balance })
      .ToListAsync(cancellationToken);
    var circleOf = await context.TaxFiles.AsNoTracking().Select(f => new { f.Id, f.CircleId }).ToDictionaryAsync(f => f.Id, f => f.CircleId, cancellationToken);
    var circles = await context.TaxCircles.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);

    var outstanding = await context.Demands.AsNoTracking()
      .Where(d => d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted)
      .SumAsync(d => (decimal?)d.Balance, cancellationToken) ?? 0m;
    var penalties = await context.Penalties.AsNoTracking().GroupBy(p => p.Kind).Select(g => new { g.Key, Total = g.Sum(p => p.Amount) }).ToListAsync(cancellationToken);
    var pending = await context.Notices.CountAsync(n => n.Status == NoticeStatus.Pending, cancellationToken);

    decimal Charged(decimal current, decimal arrears, decimal penalty) => current + arrears + penalty;
    var demanded = year.Sum(d => Charged(d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount));
    var recovered = year.Sum(d => d.PaidAmount);
    var byCircle = circles.Select(c =>
    {
      var mine = year.Where(d => circleOf.GetValueOrDefault(d.TaxFileId) == c.Id).ToList();
      return new CircleCollectionDto(c.Id.Value, c.Code, mine.Count, mine.Count(d => d.Status == DemandStatus.Paid),
        mine.Sum(d => Charged(d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount)), mine.Sum(d => d.PaidAmount), mine.Sum(d => d.Balance));
    }).ToList();

    return Result<CollectionSummaryDto>.Success(new CollectionSummaryDto(fy, FiscalYear.Label(fy), circleOf.Count, year.Count,
      year.Count(d => d.Status == DemandStatus.Paid), year.Count(d => d.Status == DemandStatus.PartPaid),
      year.Count(d => d.Status == DemandStatus.Issued), year.Count(d => d.Status == DemandStatus.Defaulted), demanded, recovered,
      demanded > 0 ? (int)Math.Round(100m * recovered / demanded) : 0, outstanding,
      penalties.FirstOrDefault(p => p.Key == PenaltyKind.IntraYear)?.Total ?? 0, penalties.FirstOrDefault(p => p.Key == PenaltyKind.YearEnd)?.Total ?? 0,
      pending, byCircle));
  }
}
