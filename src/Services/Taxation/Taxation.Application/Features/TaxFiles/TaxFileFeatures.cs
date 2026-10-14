using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record GetTaxFilesQueryResult(PaginatedResult<TaxFileListItemDto> TaxFiles);
public sealed record GetTaxFilesQuery(PaginationRequest Pagination, string? Search, Guid? CircleId, Guid? TownId, TaxCategory? Category, TaxStatus? Status, bool? WithDues)
  : IQuery<Result<GetTaxFilesQueryResult>>;

public sealed record GetTaxFileQueryResult(TaxFileDto TaxFile);
public sealed record GetTaxFileQuery(Guid Id) : IQuery<Result<GetTaxFileQueryResult>>;

/// Opens the tax file of a Property property: its facts, payers and agreements are read from Property; the category
/// comes from the property type (or is given); the 1Link reference is issued now and never changes.
public sealed record OpenTaxFileCommand(Guid PropertyId, TaxCategory? TaxCategory, TaxFileDetails Details) : ICommand<Result<CreatedResult>>;

/// Reads the property again from Property: a new basis when the facts changed, payers after a transfer, new agreements.
public sealed record SyncTaxFileCommand(Guid Id) : ICommand<Result<SyncTaxFileResult>>;
public sealed record SyncTaxFileResult(bool BasisChanged, int CurrentPayers, int Agreements);

public sealed record UpdateTaxFileCommand(Guid Id, TaxFileDetails Details) : ICommand<Result<UpdatedResult>>;
public sealed record ChangeTaxCategoryCommand(Guid Id, TaxCategory TaxCategory, string? Reason) : ICommand<Result<UpdatedResult>>;
public sealed record ChangeTaxStatusCommand(Guid Id, TaxStatus Status, string? Reason) : ICommand<Result<UpdatedResult>>;
public sealed record ChooseAddresseeCommand(Guid Id, Guid PayerId) : ICommand<Result<UpdatedResult>>;
public sealed record SetHeadValueCommand(Guid Id, Guid HeadId, DateOnly? FacilityFrom, decimal? Quantity) : ICommand<Result<UpdatedResult>>;

public sealed record AddOverrideCommand(Guid Id, Guid HeadId, OverrideMode Mode, Guid? PolicyId, decimal? Amount, bool FollowsIncrease, string Reason, string? Reference,
  DateOnly EffectiveFrom) : ICommand<Result<CreatedResult>>;
public sealed record EndOverrideCommand(Guid Id, Guid OverrideId, DateOnly? LastDay) : ICommand<Result<UpdatedResult>>;

public sealed record AddHoldCommand(Guid Id, string Reason, string? Reference, Guid? SourceLitigationId, DateOnly StartedOn, bool SuspendsDemand, bool ExcludesCompletionClock)
  : ICommand<Result<CreatedResult>>;
public sealed record EndHoldCommand(Guid Id, Guid HoldId, DateOnly EndedOn) : ICommand<Result<UpdatedResult>>;

public sealed record SetAgreementTermsCommand(Guid Id, Guid AgreementId, decimal StepPct, int StepIntervalYears, bool IsDirect99Year, DateOnly? NextRenewalDate)
  : ICommand<Result<UpdatedResult>>;
public sealed record ReviseAgreementStepCommand(Guid Id, Guid AgreementId, DateOnly EffectiveFrom, decimal StepPct, int StepIntervalYears, string? BoardReference)
  : ICommand<Result<CreatedResult>>;

public sealed record OpeningArrearsRow(string HeadCode, int? FiscalYear, decimal Amount, string? Description);

/// The old register's arrears by head (and year when known), entered before the file's first demand; they are billed on it.
public sealed record AddOpeningArrearsCommand(Guid Id, IReadOnlyList<OpeningArrearsRow> Rows) : ICommand<Result<UpdatedResult>>;

/// The year's computation for one file, without saving anything.
public sealed record PreviewAssessmentQuery(Guid Id, int FiscalYear) : IQuery<Result<AssessmentDto>>;

public sealed record GetFileDemandsQueryResult(IReadOnlyList<DemandSummaryDto> Demands, decimal Dues);
public sealed record GetFileDemandsQuery(Guid Id) : IQuery<Result<GetFileDemandsQueryResult>>;

public sealed record GetReferenceSyncsQueryResult(PaginatedResult<ReferenceSyncDto> Syncs);
public sealed record GetReferenceSyncsQuery(PaginationRequest Pagination, Guid? TaxFileId, PsidSyncStatus? Status) : IQuery<Result<GetReferenceSyncsQueryResult>>;

public class TaxFileDetailsValidator : AbstractValidator<TaxFileDetails>
{
  public TaxFileDetailsValidator()
  {
    RuleFor(x => x.Rooms).InclusiveBetween(0, 5000).When(x => x.Rooms.HasValue);
    RuleFor(x => x.UpliftPct).InclusiveBetween(0, 1000).When(x => x.UpliftPct.HasValue);
    RuleFor(x => x.OldReferenceNo).MaximumLength(50);
    RuleFor(x => x.PlotNo).MaximumLength(30);
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

public class OpenTaxFileCommandValidator : AbstractValidator<OpenTaxFileCommand>
{
  public OpenTaxFileCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Details).NotNull().SetValidator(new TaxFileDetailsValidator());
  }
}

public class UpdateTaxFileCommandValidator : AbstractValidator<UpdateTaxFileCommand>
{
  public UpdateTaxFileCommandValidator() => RuleFor(x => x.Details).NotNull().SetValidator(new TaxFileDetailsValidator());
}

public class AddOverrideCommandValidator : AbstractValidator<AddOverrideCommand>
{
  public AddOverrideCommandValidator()
  {
    RuleFor(x => x.HeadId).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    RuleFor(x => x.Reference).MaximumLength(100);
    RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue);
  }
}

public class AddHoldCommandValidator : AbstractValidator<AddHoldCommand>
{
  public AddHoldCommandValidator()
  {
    RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    RuleFor(x => x.Reference).MaximumLength(100);
  }
}

public class AddOpeningArrearsCommandValidator : AbstractValidator<AddOpeningArrearsCommand>
{
  public AddOpeningArrearsCommandValidator()
  {
    RuleFor(x => x.Rows).NotEmpty();
    RuleForEach(x => x.Rows).ChildRules(r =>
    {
      r.RuleFor(x => x.HeadCode).NotEmpty();
      r.RuleFor(x => x.Amount).GreaterThan(0);
      r.RuleFor(x => x.Description).MaximumLength(300);
    });
  }
}

public class TaxFileHandlers(
  IApplicationDbContext context,
  TaxFileImporter importer,
  TaxSetup setup,
  Ledger ledger,
  Assessor assessor,
  ICurrentUser currentUser,
  IClock clock) :
  IQueryHandler<GetTaxFilesQuery, Result<GetTaxFilesQueryResult>>,
  IQueryHandler<GetTaxFileQuery, Result<GetTaxFileQueryResult>>,
  ICommandHandler<OpenTaxFileCommand, Result<CreatedResult>>,
  ICommandHandler<SyncTaxFileCommand, Result<SyncTaxFileResult>>,
  ICommandHandler<UpdateTaxFileCommand, Result<UpdatedResult>>,
  ICommandHandler<ChangeTaxCategoryCommand, Result<UpdatedResult>>,
  ICommandHandler<ChangeTaxStatusCommand, Result<UpdatedResult>>,
  ICommandHandler<ChooseAddresseeCommand, Result<UpdatedResult>>,
  ICommandHandler<SetHeadValueCommand, Result<UpdatedResult>>,
  ICommandHandler<AddOverrideCommand, Result<CreatedResult>>,
  ICommandHandler<EndOverrideCommand, Result<UpdatedResult>>,
  ICommandHandler<AddHoldCommand, Result<CreatedResult>>,
  ICommandHandler<EndHoldCommand, Result<UpdatedResult>>,
  ICommandHandler<SetAgreementTermsCommand, Result<UpdatedResult>>,
  ICommandHandler<ReviseAgreementStepCommand, Result<CreatedResult>>,
  ICommandHandler<AddOpeningArrearsCommand, Result<UpdatedResult>>,
  IQueryHandler<PreviewAssessmentQuery, Result<AssessmentDto>>,
  IQueryHandler<GetFileDemandsQuery, Result<GetFileDemandsQueryResult>>,
  IQueryHandler<GetReferenceSyncsQuery, Result<GetReferenceSyncsQueryResult>>
{
  // ---- lists ----

  public async Task<Result<GetTaxFilesQueryResult>> Handle(GetTaxFilesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.TaxFiles.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      var digits = new string(s.Where(char.IsAsciiDigit).ToArray());
      var payerFiles = context.TaxFilePayers.Where(p => p.EffectiveTo == null && p.Name.ToUpper().Contains(s)).Select(p => p.TaxFileId);
      rows = rows.Where(f => f.FileNo.ToUpper().Contains(s) || f.PropertyCode.ToUpper().Contains(s) || (f.OldReferenceNo != null && f.OldReferenceNo.ToUpper().Contains(s))
        || (digits.Length >= 4 && f.Psid != null && f.Psid.Contains(digits)) || payerFiles.Contains(f.Id));
    }
    if (query.CircleId is { } circle)
    {
      var circleId = TaxCircleId.Of(circle);
      rows = rows.Where(f => f.CircleId == circleId);
    }
    if (query.TownId is { } town)
      rows = rows.Where(f => f.TownId == town);
    if (query.Category is { } category)
      rows = rows.Where(f => f.TaxCategory == category);
    if (query.Status is { } status)
      rows = rows.Where(f => f.TaxStatus == status);

    var owing = context.Demands.Where(d => d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted);
    if (query.WithDues == true)
      rows = rows.Where(f => owing.Any(d => d.TaxFileId == f.Id && d.Balance > 0));

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderBy(f => f.FileNo)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
      .Select(f => new
      {
        File = f,
        Dues = owing.Where(d => d.TaxFileId == f.Id).Sum(d => (decimal?)d.Balance) ?? 0m,
        Addressee = context.TaxFilePayers.Where(p => p.TaxFileId == f.Id && p.EffectiveTo == null && p.IsAddressee).Select(p => p.Name).FirstOrDefault()
      })
      .ToListAsync(cancellationToken);

    var ids = page.Select(p => p.File.Id).ToList();
    var today = clock.Today;
    var held = await context.Holds.Where(h => ids.Contains(h.TaxFileId) && h.SuspendsDemand && h.StartedOn <= today && (h.EndedOn == null || h.EndedOn >= today))
      .Select(h => h.TaxFileId).ToListAsync(cancellationToken);
    var circles = await context.TaxCircles.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);

    var data = page.Select(p => new TaxFileListItemDto(p.File.Id.Value, p.File.FileNo, p.File.PropertyId, p.File.PropertyCode, p.File.CircleId.Value,
      circles.GetValueOrDefault(p.File.CircleId), p.File.TownId, p.File.TaxCategory, p.File.HoldingKind, p.File.Psid, p.Addressee, p.File.TaxStatus,
      held.Contains(p.File.Id), p.Dues, p.File.OpenedOn)).ToList();
    return Result<GetTaxFilesQueryResult>.Success(new(new PaginatedResult<TaxFileListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetTaxFileQueryResult>> Handle(GetTaxFileQuery query, CancellationToken cancellationToken) =>
      Result<GetTaxFileQueryResult>.Success(new(await DtoAsync(await context.LoadTaxFileAsync(query.Id, cancellationToken), cancellationToken)));

  private async Task<TaxFileDto> DtoAsync(TaxFile file, CancellationToken cancellationToken)
  {
    var circle = await setup.CircleAsync(file.CircleId, cancellationToken);
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var policies = (await setup.PoliciesAsync(cancellationToken)).ToDictionary(p => p.Id, p => p.Name);
    var lastSync = await context.PsidSyncs.AsNoTracking().Where(s => s.TaxFileId == file.Id).OrderByDescending(s => s.SyncNo).FirstOrDefaultAsync(cancellationToken);
    return file.ToDto(circle.Code, await setup.TownLabelAsync(file, clock.Today, cancellationToken), await ledger.DuesAsync(file, cancellationToken), heads, policies,
      lastSync, clock.Today);
  }

  // ---- open and sync ----

  public async Task<Result<CreatedResult>> Handle(OpenTaxFileCommand command, CancellationToken cancellationToken)
  {
    var file = await importer.OpenAsync(command.PropertyId, command.TaxCategory, command.Details, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(file.Id);
  }

  public async Task<Result<SyncTaxFileResult>> Handle(SyncTaxFileCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var changed = await importer.SyncAsync(file, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<SyncTaxFileResult>.Success(new(changed, file.CurrentPayers(clock.Today).Count, file.Agreements.Count));
  }

  // ---- details ----

  public async Task<Result<UpdatedResult>> Handle(UpdateTaxFileCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    file.Update(command.Details);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeTaxCategoryCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    file.ChangeCategory(command.TaxCategory, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeTaxStatusCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    if (command.Status == TaxStatus.Closed && await ledger.DuesAsync(file, cancellationToken) > 0)
      return Result<UpdatedResult>.Failure($"{file.FileNo} still owes {Rupees.Text(await ledger.DuesAsync(file, cancellationToken))}; a file is closed only when nothing is due.");
    file.ChangeStatus(command.Status, command.Reason, clock.Today);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChooseAddresseeCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    file.ChooseAddressee(TaxFilePayerId.Of(command.PayerId));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetHeadValueCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var head = await context.LoadHeadAsync(command.HeadId, cancellationToken);
    file.SetHeadValue(head, command.FacilityFrom, command.Quantity);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- overrides and holds ----

  public async Task<Result<CreatedResult>> Handle(AddOverrideCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var head = await context.LoadHeadAsync(command.HeadId, cancellationToken);
    var policy = command.PolicyId is { } p ? await context.LoadPolicyAsync(p, cancellationToken) : null;
    var created = file.AddOverride(head, command.Mode, policy, command.Amount, command.FollowsIncrease, command.Reason, command.Reference, command.EffectiveFrom,
      Guard.Actor(currentUser.UserId, "set an override"), clock.UtcNow);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(created.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(EndOverrideCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    file.EndOverride(TaxFileOverrideId.Of(command.OverrideId), command.LastDay ?? clock.Today);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(AddHoldCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var hold = file.AddHold(command.Reason, command.Reference, command.SourceLitigationId, command.StartedOn, command.SuspendsDemand, command.ExcludesCompletionClock);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(hold.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(EndHoldCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    file.EndHold(LitigationHoldId.Of(command.HoldId), command.EndedOn);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- agreements ----

  public async Task<Result<UpdatedResult>> Handle(SetAgreementTermsCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    file.Agreement(AgreementScheduleId.Of(command.AgreementId)).SetTerms(command.StepPct, command.StepIntervalYears, command.IsDirect99Year, command.NextRenewalDate);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(ReviseAgreementStepCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var revision = file.Agreement(AgreementScheduleId.Of(command.AgreementId)).Revise(command.EffectiveFrom, command.StepPct, command.StepIntervalYears, command.BoardReference);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(revision.Id);
  }

  // ---- opening arrears ----

  public async Task<Result<UpdatedResult>> Handle(AddOpeningArrearsCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    if (await context.Demands.AnyAsync(d => d.TaxFileId == file.Id && d.Status != DemandStatus.Draft && d.Status != DemandStatus.Cancelled, cancellationToken))
      return Result<UpdatedResult>.Failure($"{file.FileNo} already has an issued demand. Add earlier dues with a notice instead.");

    var actor = Guard.Actor(currentUser.UserId, "enter opening arrears");
    foreach (var row in command.Rows)
    {
      var head = await setup.HeadAsync(row.HeadCode.Trim().ToUpperInvariant(), cancellationToken);
      if (row.FiscalYear is { } fy && fy >= FiscalYear.Of(clock.Today))
        return Result<UpdatedResult>.Failure($"Opening arrears are of earlier years; FY {FiscalYear.Label(fy)} is not.");
      context.ArrearsRows.Add(Arrears.Imported(ArrearsId.New(), file.Id, head, row.FiscalYear, row.Amount, row.Description, null, actor, clock.UtcNow));
    }
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- reads ----

  public async Task<Result<AssessmentDto>> Handle(PreviewAssessmentQuery query, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(query.Id, cancellationToken);
    var ctx = await assessor.ContextAsync(query.FiscalYear, cancellationToken);
    var a = Assessor.Assess(ctx, file);
    var arrears = await ledger.ArrearsPreviewAsync(file, query.FiscalYear, cancellationToken);
    return Result<AssessmentDto>.Success(new AssessmentDto(file.Id.Value, file.FileNo, query.FiscalYear, FiscalYear.Label(query.FiscalYear), ctx.RateSet.Id.Value,
      ctx.RateSet.Name, a.Suspended, a.SuspendedReason, a.CategoryUsed, a.UpliftPct, a.IncreaseYears, a.Lines.Select(l => l.ToDto()).ToList(), a.Notes,
      a.Total, arrears, a.Total + arrears));
  }

  public async Task<Result<GetFileDemandsQueryResult>> Handle(GetFileDemandsQuery query, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(query.Id, cancellationToken);
    var demands = await ledger.DemandsAsync(file.Id, cancellationToken);
    return Result<GetFileDemandsQueryResult>.Success(new(
      demands.Where(d => d.Status != DemandStatus.Cancelled).OrderByDescending(d => d.FiscalYear).Select(d => d.ToSummary(file.FileNo, file.Psid)).ToList(),
      await ledger.DuesAsync(file, cancellationToken)));
  }

  public async Task<Result<GetReferenceSyncsQueryResult>> Handle(GetReferenceSyncsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.PsidSyncs.AsNoTracking();
    if (query.TaxFileId is { } f)
    {
      var fileId = TaxFileId.Of(f);
      rows = rows.Where(s => s.TaxFileId == fileId);
    }
    if (query.Status is { } status)
      rows = rows.Where(s => s.Status == status);
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(s => s.SyncedAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);
    return Result<GetReferenceSyncsQueryResult>.Success(new(new PaginatedResult<ReferenceSyncDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total,
      page.Select(s => s.ToDto()).ToList())));
  }
}
