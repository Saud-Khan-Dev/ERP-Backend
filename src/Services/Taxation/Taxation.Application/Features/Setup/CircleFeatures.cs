using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- circles ----

public sealed record CircleInput(string Name, Guid? OrgUnitId, string? OfficeAddress, string? OfficeFax, string? OfficePhone, int SortOrder);

/// SectionStaff: the posts that work for the whole Section rather than one circle (the Deputy Director (Taxation)).
public sealed record GetCirclesQueryResult(IReadOnlyList<CircleDto> Circles, IReadOnlyList<CircleStaffDto> SectionStaff);
public sealed record GetCirclesQuery(bool IncludeInactive) : IQuery<Result<GetCirclesQueryResult>>;
public sealed record CreateCircleCommand(string Code, CircleInput Circle) : ICommand<Result<CreatedResult>>;
public sealed record UpdateCircleCommand(Guid Id, CircleInput Circle) : ICommand<Result<UpdatedResult>>;
public sealed record SetCircleActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

/// Deletes a circle made by mistake: only while no tax file, run or notice refers to it and no town is in it now or later.
public sealed record DeleteCircleCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

/// Brings a Property town into a circle from a date (its tax files move with it from that date).
public sealed record AssignTownCommand(Guid CircleId, Guid TownId, string? ShortCode, DateOnly EffectiveFrom) : ICommand<Result<AssignTownResult>>;
public sealed record AssignTownResult(Guid AssignmentId, string TownCode, int TaxFilesMoved);
public sealed record SetTownShortCodeCommand(Guid CircleId, Guid AssignmentId, string? ShortCode) : ICommand<Result<UpdatedResult>>;

/// The town leaves the circle after a last day (refused while it has open tax files - assign it to another circle instead).
public sealed record EndTownCommand(Guid CircleId, Guid AssignmentId, DateOnly LastDay) : ICommand<Result<UpdatedResult>>;

/// Takes back an assignment made from a later date by mistake (before it is in force).
public sealed record VoidTownCommand(Guid CircleId, Guid AssignmentId) : ICommand<Result<UpdatedResult>>;

public sealed record AddCircleStaffCommand(Guid? CircleId, Guid PostId, TaxRole TaxRole, DateOnly EffectiveFrom) : ICommand<Result<CreatedResult>>;
public sealed record EndCircleStaffCommand(Guid Id, DateOnly LastDay) : ICommand<Result<UpdatedResult>>;

public class CircleInputValidator : AbstractValidator<CircleInput>
{
  public CircleInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.OfficeAddress).MaximumLength(300);
    RuleFor(x => x.OfficeFax).MaximumLength(30);
    RuleFor(x => x.OfficePhone).MaximumLength(30);
  }
}

public class CreateCircleCommandValidator : AbstractValidator<CreateCircleCommand>
{
  public CreateCircleCommandValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
    RuleFor(x => x.Circle).NotNull().SetValidator(new CircleInputValidator());
  }
}

public class UpdateCircleCommandValidator : AbstractValidator<UpdateCircleCommand>
{
  public UpdateCircleCommandValidator() => RuleFor(x => x.Circle).NotNull().SetValidator(new CircleInputValidator());
}

public class AssignTownCommandValidator : AbstractValidator<AssignTownCommand>
{
  public AssignTownCommandValidator()
  {
    RuleFor(x => x.TownId).NotEmpty();
    RuleFor(x => x.ShortCode).MaximumLength(5);
  }
}

public class CircleHandlers(IApplicationDbContext context, IPropertyDirectory property, IClock clock) :
  IQueryHandler<GetCirclesQuery, Result<GetCirclesQueryResult>>,
  ICommandHandler<CreateCircleCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateCircleCommand, Result<UpdatedResult>>,
  ICommandHandler<SetCircleActivationCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteCircleCommand, Result<UpdatedResult>>,
  ICommandHandler<AssignTownCommand, Result<AssignTownResult>>,
  ICommandHandler<SetTownShortCodeCommand, Result<UpdatedResult>>,
  ICommandHandler<EndTownCommand, Result<UpdatedResult>>,
  ICommandHandler<VoidTownCommand, Result<UpdatedResult>>,
  ICommandHandler<AddCircleStaffCommand, Result<CreatedResult>>,
  ICommandHandler<EndCircleStaffCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetCirclesQueryResult>> Handle(GetCirclesQuery query, CancellationToken cancellationToken)
  {
    var circles = await context.TaxCircles.AsNoTracking().Include(c => c.Towns).Where(c => query.IncludeInactive || c.IsActive)
      .OrderBy(c => c.SortOrder).ThenBy(c => c.Code).ToListAsync(cancellationToken);
    var staff = await context.CircleStaff.AsNoTracking().OrderBy(s => s.TaxRole).ThenByDescending(s => s.EffectiveFrom).ToListAsync(cancellationToken);
    var used = await UsedCirclesAsync(cancellationToken);
    var today = clock.Today;
    return Result<GetCirclesQueryResult>.Success(new(
      circles.Select(c => c.ToDto(staff.Where(s => s.CircleId == c.Id), today, used.Contains(c.Id))).ToList(),
      staff.Where(s => s.CircleId == null).Select(s => s.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateCircleCommand command, CancellationToken cancellationToken)
  {
    var c = command.Circle;
    var circle = TaxCircle.Create(TaxCircleId.New(), command.Code, new CircleDetails(c.Name, c.OrgUnitId, c.OfficeAddress, c.OfficeFax, c.OfficePhone, c.SortOrder));
    if (await context.TaxCircles.AnyAsync(x => x.Code == circle.Code, cancellationToken))
      return Result<CreatedResult>.Failure($"A circle with the code {circle.Code} already exists.");
    context.TaxCircles.Add(circle);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(circle.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateCircleCommand command, CancellationToken cancellationToken)
  {
    var circle = await context.LoadCircleAsync(command.Id, cancellationToken);
    var c = command.Circle;
    circle.Update(new CircleDetails(c.Name, c.OrgUnitId, c.OfficeAddress, c.OfficeFax, c.OfficePhone, c.SortOrder));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetCircleActivationCommand command, CancellationToken cancellationToken)
  {
    var circle = await context.LoadCircleAsync(command.Id, cancellationToken);
    var hasFiles = await context.TaxFiles.AnyAsync(f => f.CircleId == circle.Id && f.TaxStatus != TaxStatus.Closed, cancellationToken);
    circle.SetActive(command.IsActive, hasFiles);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteCircleCommand command, CancellationToken cancellationToken)
  {
    var circle = await context.LoadCircleAsync(command.Id, cancellationToken);
    if ((await UsedCirclesAsync(cancellationToken)).Contains(circle.Id))
      return Result<UpdatedResult>.Failure($"Circle {circle.Code} has tax files, runs or notices; deactivate it instead.");
    var today = clock.Today;
    if (circle.Towns.FirstOrDefault(t => t.EffectiveTo is null || t.EffectiveTo >= today) is { } town)
      return Result<UpdatedResult>.Failure($"{town.TownCode} is in circle {circle.Code}{(town.EffectiveFrom > today ? $" from {town.EffectiveFrom:dd MMM yyyy}" : "")}. Take the town out first.");
    var staff = await context.CircleStaff.Where(s => s.CircleId == circle.Id).ToListAsync(cancellationToken);
    context.CircleStaff.RemoveRange(staff);
    context.TaxCircles.Remove(circle);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(EndTownCommand command, CancellationToken cancellationToken)
  {
    var circle = await context.LoadCircleAsync(command.CircleId, cancellationToken);
    var assignment = circle.Towns.FirstOrDefault(t => t.Id.Value == command.AssignmentId)
      ?? throw new TaxCircleNotFoundException($"Town assignment {command.AssignmentId} was not found in circle {circle.Code}.");
    var open = await context.TaxFiles.CountAsync(f => f.TownId == assignment.TownId && f.CircleId == circle.Id && f.TaxStatus != TaxStatus.Closed, cancellationToken);
    if (open > 0)
      return Result<UpdatedResult>.Failure($"{assignment.TownCode} has {open} open tax file{(open == 1 ? "" : "s")} in circle {circle.Code}. Assign the town to another circle instead; its files move with it.");
    var history = await context.CircleTowns.Where(t => t.TownId == assignment.TownId).ToListAsync(cancellationToken);
    circle.EndTown(assignment.Id, command.LastDay, history);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(VoidTownCommand command, CancellationToken cancellationToken)
  {
    var circle = await context.LoadCircleAsync(command.CircleId, cancellationToken);
    var assignment = circle.Towns.FirstOrDefault(t => t.Id.Value == command.AssignmentId)
      ?? throw new TaxCircleNotFoundException($"Town assignment {command.AssignmentId} was not found in circle {circle.Code}.");
    var history = await context.CircleTowns.Where(t => t.TownId == assignment.TownId).ToListAsync(cancellationToken);
    circle.VoidTown(assignment.Id, clock.Today, history);
    context.CircleTowns.Remove(assignment);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// Circles a tax file, a demand or surcharge run, or a notice's endorsement series refers to.
  private async Task<HashSet<TaxCircleId>> UsedCirclesAsync(CancellationToken cancellationToken)
  {
    var files = await context.TaxFiles.Select(f => f.CircleId).Distinct().ToListAsync(cancellationToken);
    var runs = await context.DemandRuns.Where(r => r.CircleId != null).Select(r => r.CircleId!).Distinct().ToListAsync(cancellationToken);
    var surcharges = await context.PenaltyRuns.Where(r => r.CircleId != null).Select(r => r.CircleId!).Distinct().ToListAsync(cancellationToken);
    return files.Concat(runs).Concat(surcharges).ToHashSet();
  }

  public async Task<Result<AssignTownResult>> Handle(AssignTownCommand command, CancellationToken cancellationToken)
  {
    var circle = await context.LoadCircleAsync(command.CircleId, cancellationToken);
    var town = await property.TownAsync(command.TownId, cancellationToken)
      ?? throw new DomainException("Property has no such town. Add the town in Property's master data first.");
    var history = await context.CircleTowns.Where(t => t.TownId == command.TownId).ToListAsync(cancellationToken);

    var assignment = circle.AssignTown(TaxCircleTownId.New(), town.Id, town.Code, command.ShortCode, command.EffectiveFrom, history);

    // files of the town move with it once the assignment is in force (a future move is applied by the next sync)
    var moved = 0;
    if (command.EffectiveFrom <= clock.Today)
    {
      var files = await context.TaxFiles.Where(f => f.TownId == town.Id && f.CircleId != circle.Id).ToListAsync(cancellationToken);
      foreach (var file in files)
        file.MoveToCircle(circle.Id, town.Id);
      moved = files.Count;
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<AssignTownResult>.Success(new(assignment.Id.Value, town.Code, moved));
  }

  public async Task<Result<UpdatedResult>> Handle(SetTownShortCodeCommand command, CancellationToken cancellationToken)
  {
    var circle = await context.LoadCircleAsync(command.CircleId, cancellationToken);
    var assignment = circle.Towns.FirstOrDefault(t => t.Id.Value == command.AssignmentId)
      ?? throw new TaxCircleNotFoundException($"Town assignment {command.AssignmentId} was not found in circle {circle.Code}.");
    assignment.SetShortCode(command.ShortCode);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(AddCircleStaffCommand command, CancellationToken cancellationToken)
  {
    TaxCircleId? circleId = null;
    if (command.CircleId is { } id)
      circleId = (await context.LoadCircleAsync(id, cancellationToken)).Id;
    if (await context.CircleStaff.AnyAsync(s => s.PostId == command.PostId && s.EffectiveTo == null, cancellationToken))
      return Result<CreatedResult>.Failure("The post already works for a circle. End that first.");
    var staff = TaxCircleStaff.Create(TaxCircleStaffId.New(), circleId, command.PostId, command.TaxRole, command.EffectiveFrom);
    context.CircleStaff.Add(staff);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(staff.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(EndCircleStaffCommand command, CancellationToken cancellationToken)
  {
    var staffId = TaxCircleStaffId.Of(command.Id);
    var staff = await context.CircleStaff.FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken)
      ?? throw new TaxCircleNotFoundException($"Circle staff post {command.Id} was not found.");
    staff.End(command.LastDay);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

// ---- tax heads ----

public sealed record GetHeadsQueryResult(IReadOnlyList<HeadDto> Heads);
public sealed record GetHeadsQuery(HeadKind? Kind, bool IncludeInactive) : IQuery<Result<GetHeadsQueryResult>>;

/// A regular head the admin defines; its code comes from the name unless given (SIGNBOARD_FEE).
public sealed record CreateRegularHeadCommand(string? Code, RegularHeadInput Head) : ICommand<Result<CreatedResult>>;
public sealed record UpdateRegularHeadCommand(Guid Id, RegularHeadInput Head) : ICommand<Result<UpdatedResult>>;
/// ChargedOnNotice: a charge a notice can carry (an earlier due by year, like lease money) rather than a fee charged on its own.
public sealed record OtherHeadInput(string Name, bool IsRefundable, int SortOrder, string? AccountsHeadCode, bool ChargedOnNotice = false);
public sealed record CreateOtherHeadCommand(string Code, OtherHeadInput Head) : ICommand<Result<CreatedResult>>;
public sealed record UpdateOtherHeadCommand(Guid Id, OtherHeadInput Head) : ICommand<Result<UpdatedResult>>;
public sealed record SetHeadActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

/// Deletes a head made by mistake: only while nothing was ever charged, set or paid under it.
public sealed record DeleteHeadCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

/// "Test": what a head being defined (not saved yet) would charge one file for a year. HeadId: the head being edited (its
/// rates in the set in force are used unless Rates is given); Rates: the rates to try, for the file's category, all towns.
public sealed record PreviewHeadQuery(RegularHeadInput Head, Guid TaxFileId, int FiscalYear, HeadRateInput? Rates, Guid? HeadId = null) : IQuery<Result<HeadPreviewDto>>;

public class RegularHeadInputValidator : AbstractValidator<RegularHeadInput>
{
  public RegularHeadInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.FacilityLabel).MaximumLength(60);
    RuleFor(x => x.FormulaCom).MaximumLength(500);
    RuleFor(x => x.FormulaRes).MaximumLength(500);
    RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000);
    RuleFor(x => x.BoardReference).MaximumLength(100);
  }
}

public class CreateRegularHeadCommandValidator : AbstractValidator<CreateRegularHeadCommand>
{
  public CreateRegularHeadCommandValidator()
  {
    RuleFor(x => x.Code).MaximumLength(30);
    RuleFor(x => x.Head).NotNull().SetValidator(new RegularHeadInputValidator());
  }
}

public class UpdateRegularHeadCommandValidator : AbstractValidator<UpdateRegularHeadCommand>
{
  public UpdateRegularHeadCommandValidator() => RuleFor(x => x.Head).NotNull().SetValidator(new RegularHeadInputValidator());
}

public class CreateOtherHeadCommandValidator : AbstractValidator<CreateOtherHeadCommand>
{
  public CreateOtherHeadCommandValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
    RuleFor(x => x.Head.Name).NotEmpty().MaximumLength(100);
  }
}

public class HeadHandlers(IApplicationDbContext context, TaxSetup setup, IClock clock) :
  IQueryHandler<GetHeadsQuery, Result<GetHeadsQueryResult>>,
  ICommandHandler<CreateRegularHeadCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateRegularHeadCommand, Result<UpdatedResult>>,
  ICommandHandler<CreateOtherHeadCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateOtherHeadCommand, Result<UpdatedResult>>,
  ICommandHandler<SetHeadActivationCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteHeadCommand, Result<UpdatedResult>>,
  IQueryHandler<PreviewHeadQuery, Result<HeadPreviewDto>>
{
  public async Task<Result<HeadPreviewDto>> Handle(PreviewHeadQuery query, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(query.TaxFileId, cancellationToken);
    var basis = file.CurrentBasis ?? throw new DomainException($"{file.FileNo} has no Property data yet. Sync it from Property.");
    var fy = FiscalYear.Valid(query.FiscalYear);
    var start = FiscalYear.Start(fy);
    var inForce = await setup.RateSetOnAsync(start, cancellationToken);
    var settings = await setup.SettingsAsync(start, cancellationToken);

    // the head as entered and a scratch copy of the rate set in force (neither is saved)
    var existing = query.HeadId is { } id ? await context.TaxHeads.AsNoTracking().FirstOrDefaultAsync(h => h.Id == TaxHeadId.Of(id), cancellationToken) : null;
    var head = TaxHead.Regular(existing?.Id ?? TaxHeadId.New(), existing?.Code ?? "PREVIEW", query.Head with { IsActive = true });
    var scratch = RateSet.Create(RateSetId.New(), inForce.Name, inForce.BoardReference, inForce.BoardDecisionDate, inForce.EffectiveFrom, inForce.BaseFiscalYear);
    scratch.CopyRatesFrom(inForce);
    if (query.Rates is { } rates)
      scratch.SetHeadRates(head, file.TaxCategory is TaxCategory.Com or TaxCategory.Res ? file.TaxCategory : null, null, rates);

    var assessment = TaxCalculator.Assess(new AssessmentInput(file, basis, fy, [head], scratch, settings, await setup.PoliciesAsync(cancellationToken)));
    var line = assessment.Lines.FirstOrDefault(l => l.HeadId == head.Id);
    return Result<HeadPreviewDto>.Success(new HeadPreviewDto(file.Id.Value, file.FileNo, fy, FiscalYear.Label(fy), inForce.Name, assessment.Suspended,
      assessment.SuspendedReason, line?.Amount ?? 0m, line?.Explanation, line?.BasisQty, line?.BasisUnit, assessment.Notes));
  }

  public async Task<Result<GetHeadsQueryResult>> Handle(GetHeadsQuery query, CancellationToken cancellationToken)
  {
    var heads = await context.TaxHeads.AsNoTracking()
      .Where(h => (query.Kind == null || h.HeadKind == query.Kind) && (query.IncludeInactive || h.IsActive))
      .OrderBy(h => h.HeadKind).ThenBy(h => h.SortOrder).ThenBy(h => h.Name).ToListAsync(cancellationToken);
    return Result<GetHeadsQueryResult>.Success(new(heads.Select(h => h.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateRegularHeadCommand command, CancellationToken cancellationToken)
  {
    var codes = await context.TaxHeads.Select(h => h.Code).ToListAsync(cancellationToken);
    var code = string.IsNullOrWhiteSpace(command.Code) ? UniqueCode(command.Head.Name, codes) : command.Code.Trim().ToUpperInvariant();
    if (codes.Contains(code))
      return Result<CreatedResult>.Failure($"A head with the code {code} already exists.");
    if (await context.TaxHeads.AnyAsync(h => h.Name.ToLower() == command.Head.Name.Trim().ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A head called \"{command.Head.Name.Trim()}\" already exists.");

    var head = TaxHead.Regular(TaxHeadId.New(), code, command.Head);
    context.TaxHeads.Add(head);

    // the new head gets its own row on the notice table
    var heads = (await setup.HeadsAsync(cancellationToken)).Append(head).ToList();
    await setup.EnsureLayoutRowAsync(head, heads, clock.Today, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(head.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateRegularHeadCommand command, CancellationToken cancellationToken)
  {
    var head = await context.LoadHeadAsync(command.Id, cancellationToken);
    if (await context.TaxHeads.AnyAsync(h => h.Id != head.Id && h.Name.ToLower() == command.Head.Name.Trim().ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another head is called \"{command.Head.Name.Trim()}\".");
    var calcChanged = !head.IsBuiltin && head.CalcMethod != command.Head.CalcMethod;
    head.UpdateRegular(command.Head);
    if (calcChanged && await context.RateBands.AnyAsync(b => b.HeadId == head.Id, cancellationToken))
    {
      var drafts = await context.RateSets.Where(s => s.Status == RateSetStatus.Draft).Include(s => s.RateBands).ToListAsync(cancellationToken);
      foreach (var draft in drafts)
        foreach (var group in draft.RateBands.Where(b => b.HeadId == head.Id).Select(b => (b.TaxCategory, b.TownId)).Distinct().ToList())
          draft.RemoveHeadRates(head.Id, group.TaxCategory, group.TownId);
    }
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(CreateOtherHeadCommand command, CancellationToken cancellationToken)
  {
    var h = command.Head;
    var head = TaxHead.Other(TaxHeadId.New(), command.Code, h.Name, h.IsRefundable, h.SortOrder, h.AccountsHeadCode, h.ChargedOnNotice);
    if (await context.TaxHeads.AnyAsync(x => x.Code == head.Code, cancellationToken))
      return Result<CreatedResult>.Failure($"A head with the code {head.Code} already exists.");
    if (await context.TaxHeads.AnyAsync(x => x.Name.ToLower() == head.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A head called \"{head.Name}\" already exists.");
    context.TaxHeads.Add(head);
    // a charge a notice can carry gets its own row on the notice table
    var heads = (await setup.HeadsAsync(cancellationToken)).Append(head).ToList();
    await setup.EnsureLayoutRowAsync(head, heads, clock.Today, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(head.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateOtherHeadCommand command, CancellationToken cancellationToken)
  {
    var head = await context.LoadHeadAsync(command.Id, cancellationToken);
    var h = command.Head;
    if (await context.TaxHeads.AnyAsync(x => x.Id != head.Id && x.Name.ToLower() == h.Name.Trim().ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another head is called \"{h.Name.Trim()}\".");
    if (head.ChargedOnNotice && !h.ChargedOnNotice && await ChargedOnNoticesAsync(head.Id, cancellationToken))
      return Result<UpdatedResult>.Failure($"{head.Name} is already on demands as an earlier due; it stays a charge notices carry.");
    head.UpdateOther(h.Name, h.IsRefundable, h.SortOrder, h.AccountsHeadCode, h.ChargedOnNotice);
    await setup.EnsureLayoutRowAsync(head, await setup.HeadsAsync(cancellationToken), clock.Today, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetHeadActivationCommand command, CancellationToken cancellationToken)
  {
    var head = await context.LoadHeadAsync(command.Id, cancellationToken);
    head.SetActive(command.IsActive);
    // a head brought back gets its row on the notice table again
    await setup.EnsureLayoutRowAsync(head, await setup.HeadsAsync(cancellationToken), clock.Today, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteHeadCommand command, CancellationToken cancellationToken)
  {
    var head = await context.LoadHeadAsync(command.Id, cancellationToken);
    if (head.IsSystem || head.IsBuiltin)
      return Result<UpdatedResult>.Failure($"{head.Name} is used by the module itself and cannot be deleted.");
    var id = head.Id;
    var used = await context.DemandLines.AnyAsync(l => l.HeadId == id, cancellationToken)
      || await context.ArrearsRows.AnyAsync(a => a.HeadId == id, cancellationToken)
      || await context.PaymentAllocations.AnyAsync(a => a.HeadId == id, cancellationToken)
      || await context.Fees.AnyAsync(f => f.HeadId == id, cancellationToken)
      || await context.SpecialPolicies.AnyAsync(p => p.HeadId == id, cancellationToken)
      || await context.Overrides.AnyAsync(o => o.HeadId == id, cancellationToken)
      || await context.HeadValues.AnyAsync(v => v.HeadId == id, cancellationToken)
      || await context.RateSets.AnyAsync(s => s.Status != RateSetStatus.Draft && s.RateBands.Any(b => b.HeadId == id), cancellationToken);
    if (used)
      return Result<UpdatedResult>.Failure($"{head.Name} is already used (charged, paid, set on a property or in an approved rate set); deactivate it instead.");

    var drafts = await context.RateSets.Where(s => s.Status == RateSetStatus.Draft && s.RateBands.Any(b => b.HeadId == id)).Include(s => s.RateBands).ToListAsync(cancellationToken);
    foreach (var draft in drafts)
      foreach (var group in draft.RateBands.Where(b => b.HeadId == id).Select(b => (b.TaxCategory, b.TownId)).Distinct().ToList())
        draft.RemoveHeadRates(id, group.TaxCategory, group.TownId);
    await setup.RemoveLayoutRowAsync(head, await setup.HeadsAsync(cancellationToken), clock.Today, cancellationToken);
    context.TaxHeads.Remove(head);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<bool> ChargedOnNoticesAsync(TaxHeadId id, CancellationToken cancellationToken) =>
      await context.ArrearsRows.AnyAsync(a => a.HeadId == id, cancellationToken);

  /// SIGNBOARD_FEE from "Signboard fee"; a number is added when taken.
  private static string UniqueCode(string name, IReadOnlyCollection<string> used)
  {
    var stem = new string(name.Trim().ToUpperInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
    while (stem.Contains("__")) stem = stem.Replace("__", "_");
    if (stem.Length == 0) stem = "HEAD";
    if (stem.Length > 26) stem = stem[..26].Trim('_');
    var code = stem;
    for (var i = 2; used.Contains(code); i++)
      code = $"{stem}_{i}";
    return code;
  }
}

// ---- category map ----

public sealed record GetCategoryMapsQueryResult(IReadOnlyList<CategoryMapDto> Maps);
public sealed record GetCategoryMapsQuery : IQuery<Result<GetCategoryMapsQueryResult>>;
public sealed record SetCategoryMapCommand(Guid PropertyTypeId, TaxCategory TaxCategory) : ICommand<Result<CreatedResult>>;
public sealed record DeleteCategoryMapCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class CategoryMapHandlers(IApplicationDbContext context, IPropertyDirectory property) :
  IQueryHandler<GetCategoryMapsQuery, Result<GetCategoryMapsQueryResult>>,
  ICommandHandler<SetCategoryMapCommand, Result<CreatedResult>>,
  ICommandHandler<DeleteCategoryMapCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetCategoryMapsQueryResult>> Handle(GetCategoryMapsQuery query, CancellationToken cancellationToken)
  {
    var maps = await context.CategoryMaps.AsNoTracking().OrderBy(m => m.PropertyTypeCode).ToListAsync(cancellationToken);
    return Result<GetCategoryMapsQueryResult>.Success(new(maps.Select(m => new CategoryMapDto(m.Id.Value, m.PropertyTypeId, m.PropertyTypeCode, m.TaxCategory)).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(SetCategoryMapCommand command, CancellationToken cancellationToken)
  {
    var existing = await context.CategoryMaps.FirstOrDefaultAsync(m => m.PropertyTypeId == command.PropertyTypeId, cancellationToken);
    if (existing is not null)
    {
      existing.Change(command.TaxCategory);
      await context.SaveChangesAsync(cancellationToken);
      return CommandResults.Created(existing.Id);
    }

    var type = await property.PropertyTypeAsync(command.PropertyTypeId, cancellationToken)
      ?? throw new DomainException("Property has no such property type.");
    var map = CategoryMap.Create(CategoryMapId.New(), type.Id, type.Code, command.TaxCategory);
    context.CategoryMaps.Add(map);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(map.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteCategoryMapCommand command, CancellationToken cancellationToken)
  {
    var id = CategoryMapId.Of(command.Id);
    var map = await context.CategoryMaps.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
      ?? throw new TemplateNotFoundException($"Category mapping {command.Id} was not found.");
    context.CategoryMaps.Remove(map);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}
