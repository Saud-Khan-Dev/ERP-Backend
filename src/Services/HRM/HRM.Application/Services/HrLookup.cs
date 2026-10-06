using Microsoft.EntityFrameworkCore;

/// Where an employee sits on a date: the regular post, its designation, grade and unit.
public sealed record Placement(
  Guid AssignmentId,
  Guid PostId,
  string PostCode,
  Guid DesignationId,
  string Designation,
  Guid GradeId,
  int Bps,
  Guid OrgUnitId,
  string OrgUnit,
  Guid? LocationId,
  DateOnly Since);

public sealed record EmployeeName(Guid Id, string EmployeeNumber, string FullName);

/// Batch reads that label ids for responses: a page of rows costs a handful of queries, never one per row.
public class HrLookup(IApplicationDbContext context)
{
  public async Task<Dictionary<Guid, EmployeeName>> EmployeesAsync(IEnumerable<EmployeeId?> ids, CancellationToken cancellationToken)
  {
    var list = ids.Where(i => i is not null).Select(i => i!).Distinct().ToList();
    if (list.Count == 0)
      return new();

    var rows = await context.Employees.AsNoTracking().Where(e => list.Contains(e.Id))
      .Select(e => new { e.Id, e.EmployeeNumber, e.FullName, e.FirstName }).ToListAsync(cancellationToken);
    return rows.ToDictionary(r => r.Id.Value, r => new EmployeeName(r.Id.Value, r.EmployeeNumber, r.FullName ?? r.FirstName));
  }

  public async Task<Dictionary<Guid, Placement>> PlacementsAsync(IEnumerable<EmployeeId> ids, DateOnly date, CancellationToken cancellationToken)
  {
    var list = ids.Distinct().ToList();
    if (list.Count == 0)
      return new();

    var assignments = await context.PositionAssignments.AsNoTracking()
      .Where(a => list.Contains(a.EmployeeId) && a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active
        && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
      .ToListAsync(cancellationToken);

    var placed = await PostsOnAsync(assignments.Select(a => a.PostId), date, cancellationToken);

    var result = new Dictionary<Guid, Placement>();
    foreach (var assignment in assignments)
    {
      if (!placed.TryGetValue(assignment.PostId.Value, out var post))
        continue;
      result[assignment.EmployeeId.Value] = new Placement(assignment.Id.Value, post.PostId, post.PostCode, post.DesignationId, post.Designation,
        post.GradeId, post.Bps, post.OrgUnitId, post.OrgUnit, post.LocationId, assignment.EffectiveFrom);
    }
    return result;
  }

  public sealed record PostOnDate(Guid PostId, string PostCode, Guid DesignationId, string Designation, Guid GradeId, int Bps, Guid OrgUnitId, string OrgUnit, Guid? LocationId, int SanctionedCount, PostLifecycle Lifecycle);

  /// Each post as it is on a date (designation, grade, unit), labelled.
  public async Task<Dictionary<Guid, PostOnDate>> PostsOnAsync(IEnumerable<PostId> ids, DateOnly date, CancellationToken cancellationToken)
  {
    var list = ids.Distinct().ToList();
    if (list.Count == 0)
      return new();

    var versions = await context.PostVersions.AsNoTracking()
      .Where(v => list.Contains(v.PostId) && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
      .ToListAsync(cancellationToken);
    var codes = await context.Posts.AsNoTracking().Where(p => list.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PostCode, cancellationToken);
    var designations = await DesignationsAsync(versions.Select(v => v.DesignationId), cancellationToken);
    var grades = await GradesAsync(cancellationToken);
    var units = await UnitNamesAsync(versions.Select(v => v.OrgUnitId), date, cancellationToken);

    return versions.ToDictionary(v => v.PostId.Value, v => new PostOnDate(
      v.PostId.Value, codes.GetValueOrDefault(v.PostId) ?? "", v.DesignationId.Value, designations.GetValueOrDefault(v.DesignationId.Value) ?? "",
      v.GradeId.Value, grades.TryGetValue(v.GradeId.Value, out var grade) ? grade.BpsNumber : 0,
      v.OrgUnitId.Value, units.GetValueOrDefault(v.OrgUnitId.Value) ?? "", v.LocationId?.Value, v.SanctionedCount, v.LifecycleStatus));
  }

  public async Task<Dictionary<Guid, string>> DesignationsAsync(IEnumerable<DesignationId?> ids, CancellationToken cancellationToken)
  {
    var list = ids.Where(i => i is not null).Select(i => i!).Distinct().ToList();
    if (list.Count == 0)
      return new();
    return await context.Designations.AsNoTracking().Where(d => list.Contains(d.Id)).ToDictionaryAsync(d => d.Id.Value, d => d.Title, cancellationToken);
  }

  public async Task<Dictionary<Guid, PayScaleGrade>> GradesAsync(CancellationToken cancellationToken) =>
      await context.PayScaleGrades.AsNoTracking().ToDictionaryAsync(g => g.Id.Value, cancellationToken);

  /// Unit names on a date; a unit without a version that day gets its latest name.
  public async Task<Dictionary<Guid, string>> UnitNamesAsync(IEnumerable<OrganizationUnitId?> ids, DateOnly date, CancellationToken cancellationToken)
  {
    var list = ids.Where(i => i is not null).Select(i => i!).Distinct().ToList();
    if (list.Count == 0)
      return new();

    var versions = await context.OrganizationUnitVersions.AsNoTracking().Where(v => list.Contains(v.OrgUnitId)).ToListAsync(cancellationToken);
    return versions.GroupBy(v => v.OrgUnitId).ToDictionary(
      g => g.Key.Value,
      g => (g.FirstOrDefault(v => v.Range.Contains(date)) ?? g.OrderByDescending(v => v.EffectiveFrom).First()).Name);
  }

  public async Task<Dictionary<Guid, string>> PostCodesAsync(IEnumerable<PostId?> ids, CancellationToken cancellationToken)
  {
    var list = ids.Where(i => i is not null).Select(i => i!).Distinct().ToList();
    if (list.Count == 0)
      return new();
    return await context.Posts.AsNoTracking().Where(p => list.Contains(p.Id)).ToDictionaryAsync(p => p.Id.Value, p => p.PostCode, cancellationToken);
  }

  public async Task<Dictionary<Guid, string>> LocationsAsync(IEnumerable<LocationId?> ids, CancellationToken cancellationToken)
  {
    var list = ids.Where(i => i is not null).Select(i => i!).Distinct().ToList();
    if (list.Count == 0)
      return new();
    return await context.Locations.AsNoTracking().Where(l => list.Contains(l.Id)).ToDictionaryAsync(l => l.Id.Value, l => l.Name, cancellationToken);
  }

  /// The employees whose regular post is in the unit (or under it) on a date, as a query other rows can be filtered by.
  public async Task<IQueryable<EmployeeId>> EmployeesInUnitAsync(OrganizationUnitId root, DateOnly date, CancellationToken cancellationToken)
  {
    var units = (await SubtreeAsync(root, date, cancellationToken)).ToList();
    return from a in context.PositionAssignments
           where a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active
             && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date)
           join v in context.PostVersions on a.PostId equals v.PostId
           where units.Contains(v.OrgUnitId) && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date)
           select a.EmployeeId;
  }

  /// Every org unit in force on a date with its parent, for subtree filters (the unit and everything under it).
  public async Task<HashSet<OrganizationUnitId>> SubtreeAsync(OrganizationUnitId root, DateOnly date, CancellationToken cancellationToken)
  {
    var versions = await context.OrganizationUnitVersions.AsNoTracking()
      .Where(v => v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date))
      .Select(v => new { v.OrgUnitId, v.ParentUnitId })
      .ToListAsync(cancellationToken);

    var children = versions.Where(v => v.ParentUnitId is not null).GroupBy(v => v.ParentUnitId!).ToDictionary(g => g.Key, g => g.Select(v => v.OrgUnitId).ToList());
    var result = new HashSet<OrganizationUnitId> { root };
    var queue = new Queue<OrganizationUnitId>([root]);
    while (queue.Count > 0)
    {
      foreach (var child in children.GetValueOrDefault(queue.Dequeue()) ?? [])
      {
        if (result.Add(child))
          queue.Enqueue(child);
      }
    }
    return result;
  }
}
