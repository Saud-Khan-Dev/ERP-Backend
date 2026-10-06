using Microsoft.EntityFrameworkCore;

public class OrgUnitHandlers(IApplicationDbContext context) :
  ICommandHandler<CreateOrgUnitCommand, Result<CreatedResult>>,
  ICommandHandler<RestructureOrgUnitCommand, Result<CreatedResult>>,
  ICommandHandler<CorrectOrgUnitCommand, Result<UpdatedResult>>,
  ICommandHandler<SetOrgUnitActivationCommand, Result<CreatedResult>>
{
  public async Task<Result<CreatedResult>> Handle(CreateOrgUnitCommand command, CancellationToken cancellationToken)
  {
    var code = Guard.Code(command.Code, OrganizationUnit.CodeMaxLength, "Unit code");
    if (await context.OrganizationUnits.AnyAsync(u => u.Code == code, cancellationToken))
      return Result<CreatedResult>.Failure($"An org unit with code {code} already exists.");

    var unitId = OrganizationUnitId.New();
    var details = await ResolveAsync(unitId, command.Details, command.EffectiveFrom, previous: null, cancellationToken);
    var unit = OrganizationUnit.Create(unitId, code, details, command.EffectiveFrom);

    context.OrganizationUnits.Add(unit);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(unit.Id);
  }

  public async Task<Result<CreatedResult>> Handle(RestructureOrgUnitCommand command, CancellationToken cancellationToken)
  {
    var unit = await context.LoadOrgUnitAsync(command.Id, cancellationToken);
    var details = await ResolveAsync(unit.Id, command.Details, command.EffectiveFrom, unit.Latest.Details, cancellationToken);
    var version = unit.Restructure(details, command.EffectiveFrom);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(version.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(CorrectOrgUnitCommand command, CancellationToken cancellationToken)
  {
    var unit = await context.LoadOrgUnitAsync(command.Id, cancellationToken);
    var details = await ResolveAsync(unit.Id, command.Details, unit.Latest.EffectiveFrom, unit.Latest.Details, cancellationToken);
    unit.CorrectLatest(details);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(SetOrgUnitActivationCommand command, CancellationToken cancellationToken)
  {
    var unit = await context.LoadOrgUnitAsync(command.Id, cancellationToken);
    var date = command.EffectiveFrom;

    if (!command.IsActive)
    {
      var children = await context.OrganizationUnitVersions.AsNoTracking()
        .Where(v => v.ParentUnitId == unit.Id && v.Status == RecordStatus.Active && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
        .CountAsync(cancellationToken);
      if (children > 0)
        return Result<CreatedResult>.Failure($"Unit {unit.Code} still has {children} active sub-unit(s) on {date:yyyy-MM-dd}. Move or close them first.");

      var posts = await context.PostVersions.AsNoTracking()
        .Where(v => v.OrgUnitId == unit.Id && v.LifecycleStatus != PostLifecycle.Abolished && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
        .CountAsync(cancellationToken);
      if (posts > 0)
        return Result<CreatedResult>.Failure($"Unit {unit.Code} still has {posts} post(s) on {date:yyyy-MM-dd}. Move or abolish them first.");
    }
    else if (unit.Latest.ParentUnitId is { } parentId)
    {
      var parent = await context.LoadOrgUnitAsync(parentId.Value, cancellationToken);
      if (!parent.IsActiveOn(date))
        return Result<CreatedResult>.Failure($"The parent unit {parent.Code} is not active on {date:yyyy-MM-dd}.");
    }

    var version = command.IsActive ? unit.Reactivate(date) : unit.Deactivate(date);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(version.Id);
  }

  /// Checks what the details point at, on the date they take effect. Values carried over unchanged from the previous
  /// version are not re-checked (an inactive location may stay on a unit, it just cannot be newly chosen).
  private async Task<OrganizationUnitDetails> ResolveAsync(OrganizationUnitId unitId, OrgUnitDetailsInput input, DateOnly date, OrganizationUnitDetails? previous, CancellationToken cancellationToken)
  {
    var typeId = OrganizationUnitTypeId.Of(input.UnitTypeId);
    if (previous?.UnitTypeId != typeId)
      (await context.LoadUnitTypeAsync(input.UnitTypeId, cancellationToken)).EnsureActive();

    LocationId? locationId = input.LocationId is { } location ? LocationId.Of(location) : null;
    if (locationId is not null && previous?.LocationId != locationId)
      (await context.LoadLocationAsync(input.LocationId!.Value, cancellationToken)).EnsureActive();

    OrganizationUnitId? parentId = input.ParentUnitId is { } parent ? OrganizationUnitId.Of(parent) : null;
    if (parentId is not null)
    {
      if (parentId == unitId)
        throw new DomainException("A unit cannot be its own parent.");

      var parentUnit = await context.LoadOrgUnitAsync(parentId.Value, cancellationToken);
      if (!parentUnit.IsActiveOn(date))
        throw new DomainException($"The parent unit {parentUnit.Code} is not active on {date:yyyy-MM-dd}.");

      var parentOf = (await context.OrganizationUnitVersions.AsNoTracking()
          .Where(v => v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
          .Select(v => new { v.OrgUnitId, v.ParentUnitId })
          .ToListAsync(cancellationToken))
        .ToDictionary(v => v.OrgUnitId, v => v.ParentUnitId);
      OrganizationTree.EnsureNoCycle(unitId, parentId, parentOf);
    }

    PostId? headPostId = input.HeadPostId is { } head ? PostId.Of(head) : null;
    if (headPostId is not null)
    {
      var post = await context.LoadPostAsync(input.HeadPostId!.Value, cancellationToken);
      var version = post.VersionOn(date)
        ?? throw new DomainException($"Post {post.PostCode} does not exist on {date:yyyy-MM-dd}.");
      if (version.OrgUnitId != unitId)
        throw new DomainException($"Post {post.PostCode} belongs to another unit; the head post must be one of this unit's posts.");
      if (version.LifecycleStatus == PostLifecycle.Abolished)
        throw new DomainException($"Post {post.PostCode} is abolished.");
    }

    return new OrganizationUnitDetails(input.Name, typeId, parentId, locationId, headPostId);
  }
}
