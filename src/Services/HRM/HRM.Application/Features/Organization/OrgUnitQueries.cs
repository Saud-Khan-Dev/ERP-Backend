using Microsoft.EntityFrameworkCore;

public sealed record GetOrgUnitsQueryResult(DateOnly AsOf, IReadOnlyList<OrgUnitListItemDto> OrgUnits);

/// The structure on a date (default today). IncludeInactive also lists units closed or not yet started on that date
/// (with their latest version).
public sealed record GetOrgUnitsQuery(DateOnly? AsOf, bool IncludeInactive, string? Search, Guid? ParentUnitId, Guid? UnitTypeId)
  : IQuery<Result<GetOrgUnitsQueryResult>>;

public sealed record GetOrgUnitTreeQueryResult(DateOnly AsOf, IReadOnlyList<OrgUnitNodeDto> Roots);

public sealed record GetOrgUnitTreeQuery(DateOnly? AsOf) : IQuery<Result<GetOrgUnitTreeQueryResult>>;

public sealed record GetOrgUnitQueryResult(OrgUnitDto OrgUnit);

public sealed record GetOrgUnitQuery(Guid Id) : IQuery<Result<GetOrgUnitQueryResult>>;

public class OrgUnitQueryHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock) :
  IQueryHandler<GetOrgUnitsQuery, Result<GetOrgUnitsQueryResult>>,
  IQueryHandler<GetOrgUnitTreeQuery, Result<GetOrgUnitTreeQueryResult>>,
  IQueryHandler<GetOrgUnitQuery, Result<GetOrgUnitQueryResult>>
{
  public async Task<Result<GetOrgUnitsQueryResult>> Handle(GetOrgUnitsQuery query, CancellationToken cancellationToken)
  {
    var date = query.AsOf ?? clock.Today;
    var rows = await UnitsOnAsync(date, query.IncludeInactive, cancellationToken);

    IEnumerable<OrgUnitListItemDto> filtered = rows;
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();
      filtered = filtered.Where(r => r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Code.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
    if (query.ParentUnitId is { } parent)
      filtered = filtered.Where(r => r.ParentUnitId == parent);
    if (query.UnitTypeId is { } type)
      filtered = filtered.Where(r => r.UnitTypeId == type);

    return Result<GetOrgUnitsQueryResult>.Success(new(date, filtered.ToList()));
  }

  public async Task<Result<GetOrgUnitTreeQueryResult>> Handle(GetOrgUnitTreeQuery query, CancellationToken cancellationToken)
  {
    var date = query.AsOf ?? clock.Today;
    var rows = await UnitsOnAsync(date, includeInactive: false, cancellationToken);
    var children = rows.Where(r => r.ParentUnitId is not null).GroupBy(r => r.ParentUnitId!.Value).ToDictionary(g => g.Key, g => g.ToList());
    var known = rows.Select(r => r.Id).ToHashSet();

    OrgUnitNodeDto Node(OrgUnitListItemDto unit, HashSet<Guid> path)
    {
      var next = new HashSet<Guid>(path) { unit.Id };
      var kids = (children.GetValueOrDefault(unit.Id) ?? []).Where(c => !next.Contains(c.Id)).OrderBy(c => c.Name).Select(c => Node(c, next)).ToList();
      return new OrgUnitNodeDto(unit.Id, unit.Code, unit.Name, unit.UnitType, unit.HeadPostId, unit.HeadPostCode,
        unit.SanctionedSeats + kids.Sum(k => k.SanctionedSeats), unit.FilledSeats + kids.Sum(k => k.FilledSeats), kids);
    }

    // a unit whose parent is not active on the date is shown as a root so nothing disappears
    var roots = rows.Where(r => r.ParentUnitId is null || !known.Contains(r.ParentUnitId.Value)).OrderBy(r => r.Name).Select(r => Node(r, [])).ToList();
    return Result<GetOrgUnitTreeQueryResult>.Success(new(date, roots));
  }

  public async Task<Result<GetOrgUnitQueryResult>> Handle(GetOrgUnitQuery query, CancellationToken cancellationToken)
  {
    var unitId = OrganizationUnitId.Of(query.Id);
    var unit = await context.OrganizationUnits.AsNoTracking().Include(u => u.Versions).FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken)
      ?? throw new OrganizationUnitNotFoundException($"Org unit {query.Id} was not found.");

    var today = clock.Today;
    var versions = unit.Versions.OrderByDescending(v => v.EffectiveFrom).ToList();
    var types = await context.OrganizationUnitTypes.AsNoTracking().ToDictionaryAsync(t => t.Id.Value, t => t.Name, cancellationToken);
    var parents = await lookup.UnitNamesAsync(versions.Select(v => v.ParentUnitId), today, cancellationToken);
    var locations = await lookup.LocationsAsync(versions.Select(v => v.LocationId), cancellationToken);
    var posts = await lookup.PostCodesAsync(versions.Select(v => v.HeadPostId), cancellationToken);

    OrgUnitVersionDto Map(OrganizationUnitVersion v) => new(
      v.Id.Value, v.UnitTypeId.Value, types.GetValueOrDefault(v.UnitTypeId.Value), v.ParentUnitId?.Value,
      v.ParentUnitId is null ? null : parents.GetValueOrDefault(v.ParentUnitId.Value), v.Name, v.LocationId?.Value,
      v.LocationId is null ? null : locations.GetValueOrDefault(v.LocationId.Value), v.HeadPostId?.Value,
      v.HeadPostId is null ? null : posts.GetValueOrDefault(v.HeadPostId.Value), v.EffectiveFrom, v.EffectiveTo, v.Status);

    var current = unit.VersionOn(today);
    return Result<GetOrgUnitQueryResult>.Success(new(new OrgUnitDto(unit.Id.Value, unit.Code, current is null ? null : Map(current), versions.Select(Map).ToList(), unit.CreatedAt)));
  }

  /// Every unit's version on the date (or its latest one when inactive ones are wanted), with depth and seats.
  private async Task<List<OrgUnitListItemDto>> UnitsOnAsync(DateOnly date, bool includeInactive, CancellationToken cancellationToken)
  {
    var units = await context.OrganizationUnits.AsNoTracking().Include(u => u.Versions).ToListAsync(cancellationToken);
    var picked = units
      .Select(u => (Unit: u, Version: u.VersionOn(date) ?? (includeInactive ? u.Latest : null)))
      .Where(x => x.Version is not null && (includeInactive || x.Version.Status == RecordStatus.Active))
      .ToList();

    var types = await context.OrganizationUnitTypes.AsNoTracking().ToDictionaryAsync(t => t.Id.Value, t => t.Name, cancellationToken);
    var locations = await lookup.LocationsAsync(picked.Select(x => x.Version!.LocationId), cancellationToken);
    var heads = await lookup.PostCodesAsync(picked.Select(x => x.Version!.HeadPostId), cancellationToken);
    var names = picked.ToDictionary(x => x.Unit.Id.Value, x => x.Version!.Name);
    var parentOf = picked.ToDictionary(x => x.Unit.Id.Value, x => x.Version!.ParentUnitId?.Value);

    // seats of the posts that sit in each unit on the date
    var postVersions = await context.PostVersions.AsNoTracking()
      .Where(v => v.LifecycleStatus != PostLifecycle.Abolished && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
      .Select(v => new { v.PostId, v.OrgUnitId, v.SanctionedCount })
      .ToListAsync(cancellationToken);
    var filled = (await context.PositionAssignments.AsNoTracking()
        .Where(a => a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
        .GroupBy(a => a.PostId)
        .Select(g => new { PostId = g.Key, Count = g.Count() })
        .ToListAsync(cancellationToken))
      .ToDictionary(x => x.PostId, x => x.Count);
    var seats = postVersions.GroupBy(v => v.OrgUnitId.Value).ToDictionary(
      g => g.Key,
      g => (Sanctioned: g.Sum(v => v.SanctionedCount), Filled: g.Sum(v => Math.Min(v.SanctionedCount, filled.GetValueOrDefault(v.PostId)))));

    int Depth(Guid id)
    {
      var depth = 1;
      var seen = new HashSet<Guid> { id };
      for (var parent = parentOf.GetValueOrDefault(id); parent is { } p && names.ContainsKey(p) && seen.Add(p); parent = parentOf.GetValueOrDefault(p))
        depth++;
      return depth;
    }

    return picked
      .Select(x =>
      {
        var v = x.Version!;
        var unitSeats = seats.GetValueOrDefault(x.Unit.Id.Value);
        return new OrgUnitListItemDto(
          x.Unit.Id.Value, x.Unit.Code, v.Name, v.UnitTypeId.Value, types.GetValueOrDefault(v.UnitTypeId.Value), v.ParentUnitId?.Value,
          v.ParentUnitId is null ? null : names.GetValueOrDefault(v.ParentUnitId.Value), v.LocationId?.Value,
          v.LocationId is null ? null : locations.GetValueOrDefault(v.LocationId.Value), v.HeadPostId?.Value,
          v.HeadPostId is null ? null : heads.GetValueOrDefault(v.HeadPostId.Value), v.Status, v.EffectiveFrom,
          Depth(x.Unit.Id.Value), unitSeats.Sanctioned, unitSeats.Filled);
      })
      .OrderBy(r => r.Depth).ThenBy(r => r.Name)
      .ToList();
  }
}
