using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record GetTaxFilesQueryResult(PaginatedResult<TaxFileListItemDto> TaxFiles);
public sealed record GetTaxFilesQuery(PaginationRequest Pagination, string? Search, Guid? CircleId, Guid? TownId, TaxCategory? Category, TaxStatus? Status, bool? WithDues,
  Guid? PropertyId = null) : IQuery<Result<GetTaxFilesQueryResult>>;

public sealed record GetTaxFileQueryResult(TaxFileDto TaxFile);
public sealed record GetTaxFileQuery(Guid Id) : IQuery<Result<GetTaxFileQueryResult>>;

/// Opens the tax file of a Property property: its facts, payers and agreements are read from Property; the category
/// comes from the property type (or is given); the 1Link reference is issued now and never changes.
public sealed record OpenTaxFileCommand(Guid PropertyId, TaxCategory? TaxCategory, TaxFileDetails Details) : ICommand<Result<CreatedResult>>;

/// Reads the property again from Property: a new basis when the facts changed, payers after a transfer, new agreements.
public sealed record SyncTaxFileCommand(Guid Id) : ICommand<Result<SyncTaxFileResult>>;
/// MappedCategory: the category the property type maps to now, when it is not the file's (change it with approval);
/// PropertyActive: false when Property deactivated the property (close the file once nothing is owed).
public sealed record SyncTaxFileResult(bool BasisChanged, int CurrentPayers, int Agreements, TaxCategory? MappedCategory, bool PropertyActive);

public sealed record UpdateTaxFileCommand(Guid Id, TaxFileDetails Details) : ICommand<Result<UpdatedResult>>;
public sealed record ChangeTaxCategoryCommand(Guid Id, TaxCategory TaxCategory, string? Reason) : ICommand<Result<UpdatedResult>>;
public sealed record ChangeTaxStatusCommand(Guid Id, TaxStatus Status, string? Reason) : ICommand<Result<UpdatedResult>>;
public sealed record ChooseAddresseeCommand(Guid Id, Guid PayerId) : ICommand<Result<UpdatedResult>>;
public sealed record SetHeadValueCommand(Guid Id, Guid HeadId, DateOnly? FacilityFrom, decimal? Quantity) : ICommand<Result<UpdatedResult>>;

public sealed record AddOverrideCommand(Guid Id, Guid HeadId, OverrideMode Mode, Guid? PolicyId, decimal? Amount, bool FollowsIncrease, string Reason, string? Reference,
  DateOnly EffectiveFrom) : ICommand<Result<CreatedResult>>;
public sealed record EndOverrideCommand(Guid Id, Guid OverrideId, DateOnly? LastDay) : ICommand<Result<UpdatedResult>>;

/// Takes back an override set from a later date by mistake, before it is in force (it never charged anything).
public sealed record VoidOverrideCommand(Guid Id, Guid OverrideId) : ICommand<Result<UpdatedResult>>;

public sealed record AddHoldCommand(Guid Id, string Reason, string? Reference, Guid? SourceLitigationId, DateOnly StartedOn, bool SuspendsDemand, bool ExcludesCompletionClock)
  : ICommand<Result<CreatedResult>>;
public sealed record EndHoldCommand(Guid Id, Guid HoldId, DateOnly EndedOn) : ICommand<Result<UpdatedResult>>;

/// Takes back a hold put from a later date by mistake, before it begins.
public sealed record VoidHoldCommand(Guid Id, Guid HoldId) : ICommand<Result<UpdatedResult>>;

public sealed record SetAgreementTermsCommand(Guid Id, Guid AgreementId, decimal StepPct, int StepIntervalYears, bool IsDirect99Year, DateOnly? NextRenewalDate)
  : ICommand<Result<UpdatedResult>>;
public sealed record ReviseAgreementStepCommand(Guid Id, Guid AgreementId, DateOnly EffectiveFrom, decimal StepPct, int StepIntervalYears, string? BoardReference)
  : ICommand<Result<CreatedResult>>;

public sealed record OpeningArrearsRow(string HeadCode, int? FiscalYear, decimal Amount, string? Description);

/// The old register's arrears by head (and year when known), entered before the file's first demand; they are billed on it
/// (and can be paid against the reference before it).
public sealed record AddOpeningArrearsCommand(Guid Id, IReadOnlyList<OpeningArrearsRow> Rows) : ICommand<Result<UpdatedResult>>;

/// The old register's dues on a file, billed or not (the Dues tab lists them; an unbilled one can be corrected or removed).
public sealed record GetOpeningArrearsQueryResult(IReadOnlyList<ArrearsDto> Rows, decimal Outstanding, bool Billed);
public sealed record GetOpeningArrearsQuery(Guid Id) : IQuery<Result<GetOpeningArrearsQueryResult>>;

/// Corrects an opening due not billed yet (never below what was already paid on it), with the reason.
public sealed record CorrectOpeningArrearsCommand(Guid Id, Guid ArrearsId, OpeningArrearsRow Row, string Reason) : ICommand<Result<UpdatedResult>>;

/// Removes an opening due entered by mistake: not billed and nothing paid on it.
public sealed record RemoveOpeningArrearsCommand(Guid Id, Guid ArrearsId, string Reason) : ICommand<Result<UpdatedResult>>;

/// Every override on every file (the Overrides register), with what it charges and who set it.
public sealed record GetOverridesQueryResult(PaginatedResult<OverrideListItemDto> Overrides);
public sealed record GetOverridesQuery(PaginationRequest Pagination, Guid? HeadId, Guid? PolicyId, OverrideMode? Mode, bool? InForce, Guid? CircleId, string? Search)
  : IQuery<Result<GetOverridesQueryResult>>;

/// The year's computation for one file, without saving anything.
public sealed record PreviewAssessmentQuery(Guid Id, int FiscalYear) : IQuery<Result<AssessmentDto>>;

public sealed record GetFileDemandsQueryResult(IReadOnlyList<DemandSummaryDto> Demands, decimal Dues);
public sealed record GetFileDemandsQuery(Guid Id) : IQuery<Result<GetFileDemandsQueryResult>>;

/// The file's History: every line of the audit trail about the file and what hangs under it (Property facts, payers,
/// overrides, holds, agreements, arrears, demands, notices, surcharges, payments, fees, clearances), newest first.
public sealed record GetTaxFileHistoryQuery(Guid Id, AuditFilter Filter) : IQuery<Result<AuditResponse>>;

public sealed record GetReferenceSyncsQueryResult(PaginatedResult<ReferenceSyncDto> Syncs);
public sealed record GetReferenceSyncsQuery(PaginationRequest Pagination, Guid? TaxFileId, PsidSyncStatus? Status, PsidSyncReason? Reason = null, string? Search = null,
  DateOnly? From = null, DateOnly? To = null) : IQuery<Result<GetReferenceSyncsQueryResult>>;

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

public class CorrectOpeningArrearsCommandValidator : AbstractValidator<CorrectOpeningArrearsCommand>
{
  public CorrectOpeningArrearsCommandValidator()
  {
    RuleFor(x => x.Row.HeadCode).NotEmpty();
    RuleFor(x => x.Row.Amount).GreaterThan(0);
    RuleFor(x => x.Row.Description).MaximumLength(300);
    RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
  }
}

public class RemoveOpeningArrearsCommandValidator : AbstractValidator<RemoveOpeningArrearsCommand>
{
  public RemoveOpeningArrearsCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
}

public class TaxFileHandlers(
  IApplicationDbContext context,
  TaxFileImporter importer,
  TaxSetup setup,
  Ledger ledger,
  Assessor assessor,
  Messenger messenger,
  ICurrentUser currentUser,
  IAuditTrail audit,
  IClock clock) :
  IQueryHandler<GetTaxFilesQuery, Result<GetTaxFilesQueryResult>>,
  IQueryHandler<GetTaxFileHistoryQuery, Result<AuditResponse>>,
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
  ICommandHandler<VoidOverrideCommand, Result<UpdatedResult>>,
  ICommandHandler<AddHoldCommand, Result<CreatedResult>>,
  ICommandHandler<EndHoldCommand, Result<UpdatedResult>>,
  ICommandHandler<VoidHoldCommand, Result<UpdatedResult>>,
  ICommandHandler<SetAgreementTermsCommand, Result<UpdatedResult>>,
  ICommandHandler<ReviseAgreementStepCommand, Result<CreatedResult>>,
  ICommandHandler<AddOpeningArrearsCommand, Result<UpdatedResult>>,
  IQueryHandler<GetOpeningArrearsQuery, Result<GetOpeningArrearsQueryResult>>,
  ICommandHandler<CorrectOpeningArrearsCommand, Result<UpdatedResult>>,
  ICommandHandler<RemoveOpeningArrearsCommand, Result<UpdatedResult>>,
  IQueryHandler<GetOverridesQuery, Result<GetOverridesQueryResult>>,
  IQueryHandler<PreviewAssessmentQuery, Result<AssessmentDto>>,
  IQueryHandler<GetFileDemandsQuery, Result<GetFileDemandsQueryResult>>,
  IQueryHandler<GetReferenceSyncsQuery, Result<GetReferenceSyncsQueryResult>>
{
  // ---- lists ----

  public async Task<Result<GetTaxFilesQueryResult>> Handle(GetTaxFilesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.ScopedFiles().AsNoTracking();
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
    if (query.PropertyId is { } property)
      rows = rows.Where(f => f.PropertyId == property);
    if (query.Category is { } category)
      rows = rows.Where(f => f.TaxCategory == category);
    if (query.Status is { } status)
      rows = rows.Where(f => f.TaxStatus == status);

    var owing = context.Demands.Where(d => d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted);
    if (query.WithDues == true)
      rows = rows.Where(f => owing.Any(d => d.TaxFileId == f.Id && d.Balance > 0)
        || (!context.Demands.Any(d => d.TaxFileId == f.Id && d.Status != DemandStatus.Draft && d.Status != DemandStatus.Cancelled)
          && context.ArrearsRows.Any(a => a.TaxFileId == f.Id && a.AddedToDemandId == null && a.Outstanding > 0)));

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderBy(f => f.FileNo)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
      .Select(f => new
      {
        File = f,
        // what is behind the reference: the open demands, or before the first demand the old register's dues
        Dues = (owing.Where(d => d.TaxFileId == f.Id).Sum(d => (decimal?)d.Balance) ?? 0m)
          + (context.Demands.Any(d => d.TaxFileId == f.Id && d.Status != DemandStatus.Draft && d.Status != DemandStatus.Cancelled) ? 0m
            : context.ArrearsRows.Where(a => a.TaxFileId == f.Id && a.AddedToDemandId == null).Sum(a => (decimal?)a.Outstanding) ?? 0m),
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

  public async Task<Result<AuditResponse>> Handle(GetTaxFileHistoryQuery query, CancellationToken cancellationToken)
  {
    var fileId = TaxFileId.Of(query.Id);
    if (!await context.ScopedFiles().AnyAsync(f => f.Id == fileId, cancellationToken))
      throw new TaxFileNotFoundException($"Tax file {query.Id} was not found.");
    var id = query.Id;
    var trail = context.AuditTrail.Where(x => x.ParentId == id || x.RecordId == id);
    return Result<AuditResponse>.Success(new(await AuditQueries.ListAsync(trail, query.Filter with { ParentId = null, RecordId = null }, cancellationToken)));
  }

  private async Task<TaxFileDto> DtoAsync(TaxFile file, CancellationToken cancellationToken)
  {
    var circle = await setup.CircleAsync(file.CircleId, cancellationToken);
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var policies = (await setup.PoliciesAsync(cancellationToken)).ToDictionary(p => p.Id, p => p.Name);
    var lastSync = await context.PsidSyncs.AsNoTracking().Where(s => s.TaxFileId == file.Id).OrderByDescending(s => s.SyncNo).FirstOrDefaultAsync(cancellationToken);
    return file.ToDto(circle.Code, await setup.TownLabelAsync(file, clock.Today, cancellationToken), await ledger.DuesAsync(file, cancellationToken), heads, policies,
      lastSync, clock.Today, await ledger.FeesDueAsync(file.Id, cancellationToken), await ledger.CreditAsync(file.Id, cancellationToken));
  }

  // ---- open and sync ----

  public async Task<Result<CreatedResult>> Handle(OpenTaxFileCommand command, CancellationToken cancellationToken)
  {
    var file = await importer.OpenAsync(command.PropertyId, command.TaxCategory, command.Details, currentUser.HasPermission(PermissionCatalog.Tax.Approve), cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(file.Id);
  }

  public async Task<Result<SyncTaxFileResult>> Handle(SyncTaxFileCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var outcome = await importer.SyncAsync(file, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<SyncTaxFileResult>.Success(new(outcome.BasisChanged, file.CurrentPayers(clock.Today).Count, file.Agreements.Count, outcome.MappedCategory,
      outcome.PropertyActive));
  }

  // ---- details ----

  public async Task<Result<UpdatedResult>> Handle(UpdateTaxFileCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    if (file.UpliftDiffers(command.Details) && !currentUser.HasPermission(PermissionCatalog.Tax.Approve))
      return Result<UpdatedResult>.Failure("The file's completion-certificate uplift changes its tax like an override; changing it needs approval rights (TAX.APPROVE).");
    file.Update(command.Details);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeTaxCategoryCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var before = file.TaxCategory;
    file.ChangeCategory(command.TaxCategory, command.Reason);
    if (file.TaxCategory != before)
      audit.Record("Recategorised", file, Reason(command.Reason), "Category", AuditValues.Text(before), AuditValues.Text(file.TaxCategory));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeTaxStatusCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    if (command.Status == TaxStatus.Closed && await ledger.DuesAsync(file, cancellationToken) > 0)
      return Result<UpdatedResult>.Failure($"{file.FileNo} still owes {Rupees.Text(await ledger.DuesAsync(file, cancellationToken))}; a file is closed only when nothing is due.");
    var before = file.TaxStatus;
    file.ChangeStatus(command.Status, command.Reason, clock.Today);
    if (file.TaxStatus != before)
      audit.Record(file.TaxStatus switch { TaxStatus.Closed => "Closed", TaxStatus.Exempt => "Exempted", _ => "Reopened" }, file, Reason(command.Reason), "Status",
        AuditValues.Text(before), AuditValues.Text(file.TaxStatus));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChooseAddresseeCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    file.ChooseAddressee(TaxFilePayerId.Of(command.PayerId));
    await importer.ReaddressAsync(file, cancellationToken);
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

  public async Task<Result<UpdatedResult>> Handle(VoidOverrideCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var voided = file.VoidOverride(TaxFileOverrideId.Of(command.OverrideId), clock.Today);
    audit.Record("Taken back", voided, $"Set by mistake, before it was in force (from {voided.EffectiveFrom:dd MMM yyyy})");
    context.Overrides.Remove(voided);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(VoidHoldCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var voided = file.VoidHold(LitigationHoldId.Of(command.HoldId), clock.Today);
    audit.Record("Taken back", voided, $"Put by mistake, before it began (from {voided.StartedOn:dd MMM yyyy})");
    context.Holds.Remove(voided);
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
    var total = 0m;
    foreach (var row in command.Rows)
    {
      var head = await setup.HeadAsync(row.HeadCode.Trim().ToUpperInvariant(), cancellationToken);
      if (row.FiscalYear is { } fy && fy >= FiscalYear.Of(clock.Today))
        return Result<UpdatedResult>.Failure($"Opening arrears are of earlier years; FY {FiscalYear.Label(fy)} is not.");
      if (!head.IsActive)
        return Result<UpdatedResult>.Failure($"{head.Name} is inactive.");
      context.ArrearsRows.Add(Arrears.Imported(ArrearsId.New(), file.Id, head, row.FiscalYear, row.Amount, row.Description, null, actor, clock.UtcNow));
      total += Rupees.Round(row.Amount);
    }
    // the amount behind the reference changed
    await messenger.SyncAsync(file, PsidSyncReason.Correction, null, null, null, $"{Rupees.Text(total)} of opening arrears entered", cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<GetOpeningArrearsQueryResult>> Handle(GetOpeningArrearsQuery query, CancellationToken cancellationToken)
  {
    var fileId = TaxFileId.Of(query.Id);
    if (!await context.ScopedFiles().AnyAsync(f => f.Id == fileId, cancellationToken))
      throw new TaxFileNotFoundException($"Tax file {query.Id} was not found.");
    var rows = await context.ArrearsRows.AsNoTracking().Where(a => a.TaxFileId == fileId && a.Source == ArrearsSource.Import)
      .OrderBy(a => a.FiscalYear ?? 0).ThenBy(a => a.EnteredAt).ToListAsync(cancellationToken);
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var billed = rows.Any(a => a.AddedToDemandId is not null);
    return Result<GetOpeningArrearsQueryResult>.Success(new(rows.Select(a => a.ToDto(heads)).ToList(),
      rows.Where(a => a.AddedToDemandId is null).Sum(a => a.Outstanding), billed));
  }

  public async Task<Result<UpdatedResult>> Handle(CorrectOpeningArrearsCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var row = await OpeningRowAsync(file, command.ArrearsId, cancellationToken);
    var head = await setup.HeadAsync(command.Row.HeadCode.Trim().ToUpperInvariant(), cancellationToken);
    if (command.Row.FiscalYear is { } fy && fy >= FiscalYear.Of(clock.Today))
      return Result<UpdatedResult>.Failure($"Opening arrears are of earlier years; FY {FiscalYear.Label(fy)} is not.");
    var before = row.OriginalAmount;
    row.CorrectOpening(head, command.Row.FiscalYear, command.Row.Amount, command.Row.Description, command.Reason, Guard.Actor(currentUser.UserId, "correct opening arrears"),
      clock.UtcNow);
    audit.Record("Corrected", row, command.Reason.Trim(), "Amount", Rupees.Number(before), Rupees.Number(row.OriginalAmount));
    await messenger.SyncAsync(file, PsidSyncReason.Correction, null, null, null,
      $"opening arrears corrected {Rupees.Text(before)} → {Rupees.Text(row.OriginalAmount)}: {command.Reason.Trim()}", cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(RemoveOpeningArrearsCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.Id, cancellationToken);
    var row = await OpeningRowAsync(file, command.ArrearsId, cancellationToken);
    if (!row.IsUnbilledOpening)
      return Result<UpdatedResult>.Failure("This due is billed on a demand now; correct it with a notice instead.");
    if (row.Settled > 0)
      return Result<UpdatedResult>.Failure($"{Rupees.Text(row.Settled)} was already paid on it; correct its amount instead of removing it.");
    audit.Record("Taken back", row, $"Entered by mistake: {command.Reason.Trim()}");
    context.ArrearsRows.Remove(row);
    await messenger.SyncAsync(file, PsidSyncReason.Correction, null, null, null, $"opening arrears of {Rupees.Text(row.OriginalAmount)} removed: {command.Reason.Trim()}",
      cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<Arrears> OpeningRowAsync(TaxFile file, Guid arrearsId, CancellationToken cancellationToken)
  {
    var id = ArrearsId.Of(arrearsId);
    var row = await context.ArrearsRows.FirstOrDefaultAsync(a => a.Id == id && a.TaxFileId == file.Id, cancellationToken)
      ?? throw new TaxFileNotFoundException($"The opening due {arrearsId} was not found on {file.FileNo}.");
    if (row.Source != ArrearsSource.Import)
      throw new DomainException("Only an opening due of the old register is corrected here.");
    return row;
  }

  // ---- the overrides register ----

  public async Task<Result<GetOverridesQueryResult>> Handle(GetOverridesQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var rows = context.Overrides.AsNoTracking().InScope(context);
    if (query.HeadId is { } h)
    {
      var headId = TaxHeadId.Of(h);
      rows = rows.Where(o => o.HeadId == headId);
    }
    if (query.PolicyId is { } p)
    {
      var policyId = SpecialPolicyId.Of(p);
      rows = rows.Where(o => o.PolicyId == policyId);
    }
    if (query.Mode is { } mode)
      rows = rows.Where(o => o.Mode == mode);
    if (query.InForce is { } inForce)
      rows = inForce ? rows.Where(o => o.EffectiveFrom <= today && (o.EffectiveTo == null || o.EffectiveTo >= today))
        : rows.Where(o => o.EffectiveFrom > today || (o.EffectiveTo != null && o.EffectiveTo < today));
    if (query.CircleId is { } c)
    {
      var circleId = TaxCircleId.Of(c);
      var inCircle = context.TaxFiles.Where(f => f.CircleId == circleId).Select(f => f.Id);
      rows = rows.Where(o => inCircle.Contains(o.TaxFileId));
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      var files = context.TaxFiles.Where(f => f.FileNo.ToUpper().Contains(s) || f.PropertyCode.ToUpper().Contains(s)).Select(f => f.Id);
      rows = rows.Where(o => files.Contains(o.TaxFileId) || o.Reason.ToUpper().Contains(s) || (o.Reference != null && o.Reference.ToUpper().Contains(s)));
    }
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(o => o.EffectiveFrom).ThenBy(o => o.TaxFileId)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var fileIds = page.Select(o => o.TaxFileId).Distinct().ToList();
    var files2 = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(x => x.Id);
    var policies = (await setup.PoliciesAsync(cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
    var data = page.Select(o => new OverrideListItemDto(o.Id.Value, o.TaxFileId.Value, files2.GetValueOrDefault(o.TaxFileId) ?? "", o.HeadId.Value,
      heads.GetValueOrDefault(o.HeadId)?.Code ?? "", heads.GetValueOrDefault(o.HeadId)?.Name ?? "", o.Mode, o.PolicyId?.Value,
      o.PolicyId is { } pid ? policies.GetValueOrDefault(pid) : null, o.Amount, o.FollowsIncrease, o.Reason, o.Reference, o.EffectiveFrom, o.EffectiveTo,
      o.ApprovedBy, o.ApprovedAt, o.Covers(today))).ToList();
    return Result<GetOverridesQueryResult>.Success(new(new PaginatedResult<OverrideListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
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
    var rows = context.PsidSyncs.AsNoTracking().InScope(context);
    if (query.TaxFileId is { } f)
    {
      var fileId = TaxFileId.Of(f);
      rows = rows.Where(s => s.TaxFileId == fileId);
    }
    if (query.Status is { } status)
      rows = rows.Where(s => s.Status == status);
    if (query.Reason is { } reason)
      rows = rows.Where(s => s.Reason == reason);
    if (query.From is { } from)
    {
      var start = Pkt.StartOf(from);
      rows = rows.Where(s => s.SyncedAt >= start);
    }
    if (query.To is { } to)
    {
      var end = Pkt.StartOf(to.AddDays(1));
      rows = rows.Where(s => s.SyncedAt < end);
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var q = query.Search.Trim().ToUpperInvariant();
      var digits = new string(q.Where(char.IsAsciiDigit).ToArray());
      var files = context.TaxFiles.Where(f => f.FileNo.ToUpper().Contains(q)).Select(f => f.Id);
      rows = rows.Where(s => files.Contains(s.TaxFileId) || (digits.Length >= 4 && s.Psid.Contains(digits)) || (s.Note != null && s.Note.ToUpper().Contains(q)));
    }
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(s => s.SyncedAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);
    var fileIds = page.Select(s => s.TaxFileId).Distinct().ToList();
    var fileNos = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    return Result<GetReferenceSyncsQueryResult>.Success(new(new PaginatedResult<ReferenceSyncDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total,
      page.Select(s => s.ToDto(fileNos.GetValueOrDefault(s.TaxFileId))).ToList())));
  }

  private static string? Reason(string? reason) => string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}
