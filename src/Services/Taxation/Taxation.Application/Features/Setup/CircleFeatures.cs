using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- circles ----

public sealed record CircleInput(string Name, Guid? OrgUnitId, Guid? OfficerPostId, string? OfficeAddress, string? OfficeFax, string? OfficePhone, int SortOrder);

public sealed record GetCirclesQueryResult(IReadOnlyList<CircleDto> Circles);
public sealed record GetCirclesQuery(bool IncludeInactive) : IQuery<Result<GetCirclesQueryResult>>;
public sealed record CreateCircleCommand(string Code, CircleInput Circle) : ICommand<Result<CreatedResult>>;
public sealed record UpdateCircleCommand(Guid Id, CircleInput Circle) : ICommand<Result<UpdatedResult>>;
public sealed record SetCircleActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

/// Brings a Property town into a circle from a date (its tax files move with it from that date).
public sealed record AssignTownCommand(Guid CircleId, Guid TownId, string? ShortCode, DateOnly EffectiveFrom) : ICommand<Result<AssignTownResult>>;
public sealed record AssignTownResult(Guid AssignmentId, string TownCode, int TaxFilesMoved);
public sealed record SetTownShortCodeCommand(Guid CircleId, Guid AssignmentId, string? ShortCode) : ICommand<Result<UpdatedResult>>;

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
  ICommandHandler<AssignTownCommand, Result<AssignTownResult>>,
  ICommandHandler<SetTownShortCodeCommand, Result<UpdatedResult>>,
  ICommandHandler<AddCircleStaffCommand, Result<CreatedResult>>,
  ICommandHandler<EndCircleStaffCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetCirclesQueryResult>> Handle(GetCirclesQuery query, CancellationToken cancellationToken)
  {
    var circles = await context.TaxCircles.AsNoTracking().Include(c => c.Towns).Where(c => query.IncludeInactive || c.IsActive)
      .OrderBy(c => c.SortOrder).ThenBy(c => c.Code).ToListAsync(cancellationToken);
    var staff = await context.CircleStaff.AsNoTracking().OrderBy(s => s.TaxRole).ThenByDescending(s => s.EffectiveFrom).ToListAsync(cancellationToken);
    var today = clock.Today;
    return Result<GetCirclesQueryResult>.Success(new(circles.Select(c => c.ToDto(staff.Where(s => s.CircleId == c.Id), today)).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateCircleCommand command, CancellationToken cancellationToken)
  {
    var c = command.Circle;
    var circle = TaxCircle.Create(TaxCircleId.New(), command.Code, new CircleDetails(c.Name, c.OrgUnitId, c.OfficerPostId, c.OfficeAddress, c.OfficeFax, c.OfficePhone, c.SortOrder));
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
    circle.Update(new CircleDetails(c.Name, c.OrgUnitId, c.OfficerPostId, c.OfficeAddress, c.OfficeFax, c.OfficePhone, c.SortOrder));
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
public sealed record OtherHeadInput(string Name, bool IsRefundable, int SortOrder, string? AccountsHeadCode);
public sealed record CreateOtherHeadCommand(string Code, OtherHeadInput Head) : ICommand<Result<CreatedResult>>;
public sealed record UpdateOtherHeadCommand(Guid Id, OtherHeadInput Head) : ICommand<Result<UpdatedResult>>;
public sealed record SetHeadActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

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
  ICommandHandler<SetHeadActivationCommand, Result<UpdatedResult>>
{
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

    // the new head gets its own row on the notice table (a new layout version from today), before security,
    // surcharge and withholding, as the Section's notice lists the heads first
    var layout = await setup.LayoutAsync(clock.Today, cancellationToken);
    if (!layout.OrderedRows.SelectMany(r => r.Sources).Any(s => s.SourceCode == head.Code))
    {
      var rows = layout.OrderedRows.Select(r => new TableRowInput(r.Label, r.Sources.Select(s => s.SourceCode).ToList())).ToList();
      var tail = rows.FindIndex(r => r.HeadCodes.Any(c => c is HeadCodes.Security or HeadCodes.Surcharge or HeadCodes.Wht236A));
      rows.Insert(tail < 0 ? rows.Count : tail, new TableRowInput(head.Name, [head.Code]));
      var known = codes.Append(head.Code).ToHashSet();
      var next = NoticeTableLayout.Create(NoticeTableLayoutId.New(),
        new TableLayoutInput(rows, layout.OrderedColumns.Select(c => new TableColumnInput(c.ColumnKey, c.Heading, c.PrintedByDefault)).ToList(), layout.OtherLabel,
          layout.HideZeroRows, $"New head {head.Code}"), known, clock.Today);
      layout.EndBefore(clock.Today);
      context.TableLayouts.Add(next);
    }

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
    var head = TaxHead.Other(TaxHeadId.New(), command.Code, command.Head.Name, command.Head.IsRefundable, command.Head.SortOrder, command.Head.AccountsHeadCode);
    if (await context.TaxHeads.AnyAsync(h => h.Code == head.Code, cancellationToken))
      return Result<CreatedResult>.Failure($"A head with the code {head.Code} already exists.");
    context.TaxHeads.Add(head);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(head.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateOtherHeadCommand command, CancellationToken cancellationToken)
  {
    var head = await context.LoadHeadAsync(command.Id, cancellationToken);
    head.UpdateOther(command.Head.Name, command.Head.IsRefundable, command.Head.SortOrder, command.Head.AccountsHeadCode);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetHeadActivationCommand command, CancellationToken cancellationToken)
  {
    var head = await context.LoadHeadAsync(command.Id, cancellationToken);
    head.SetActive(command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

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
