using MediatR;
using Microsoft.EntityFrameworkCore;

/// The employees the signed-in person may see, as the database filter reads it (ApplicationDbContext applies it to
/// every table that belongs to one employee: the employee, contacts, family, bank accounts, pay records, documents,
/// HR actions, separations, reviews, attendance, leave, salary overrides, tax, loans, GP Fund, pay slips, payments,
/// tasks and requests). Null means no limit. A person whose access is limited by office and whose scope has not been
/// worked out yet sees nobody: the filter fails closed.
public sealed class EmployeeScope(ICurrentUser currentUser)
{
  private int _open;
  private List<EmployeeId>? _employees;
  private List<Guid>? _employeeGuids;

  /// The person's access is limited to some offices.
  public bool IsLimited { get; } = currentUser.OfficeScope is not null;

  public bool IsLoaded => _employees is not null;

  /// The ids the filter keeps; null = no filter (no limit, or inside Unrestricted()).
  public List<EmployeeId>? Employees => _open > 0 || !IsLimited ? null : _employees ?? [];

  /// The same ids as Guids, for the read-only views.
  public List<Guid>? EmployeeGuids => _open > 0 || !IsLimited ? null : _employeeGuids ?? [];

  internal void Set(IEnumerable<EmployeeId> employees)
  {
    _employees = employees.Distinct().ToList();
    _employeeGuids = _employees.Select(e => e.Value).ToList();
  }

  /// Inside this scope nothing is filtered: self-service (/me is the person's own record), the staff directory, the
  /// numbering and uniqueness checks, and the scope's own working-out.
  public IDisposable Unrestricted()
  {
    _open++;
    return new Release(this);
  }

  private sealed class Release(EmployeeScope scope) : IDisposable
  {
    private bool _done;
    public void Dispose()
    {
      if (_done) return;
      _done = true;
      scope._open--;
    }
  }
}

/// Data scope by office (Administration › user › "Works on"): a person limited to some offices sees and acts on the
/// employees whose current post is in one of them or in a unit below them (an employee without a post today counts
/// where their last post was; one appointed from a later date where that post is). Everything about such an employee
/// follows. Records outside the scope answer 404, exactly as if they did not exist. Self-service is never limited.
public sealed class OfficeScope(IApplicationDbContext context, EmployeeScope scope, ICurrentUser currentUser, IClock clock)
{
  private HashSet<OrganizationUnitId>? _units;

  /// The person's access is limited to some offices.
  public bool IsLimited => scope.IsLimited;

  /// Works out the employees in scope once per request (the pipeline calls it before every handler).
  public async Task LoadAsync(CancellationToken cancellationToken)
  {
    if (!scope.IsLimited || scope.IsLoaded)
      return;

    using (scope.Unrestricted())
    {
      var today = clock.Today;
      var units = await UnitsAsync(cancellationToken);

      var assignments = await context.PositionAssignments.AsNoTracking()
        .Where(a => a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active)
        .Select(a => new { a.EmployeeId, a.PostId, a.EffectiveFrom, a.EffectiveTo })
        .ToListAsync(cancellationToken);

      // where each employee counts: the post held today, else the last one held, else the first one to come
      var chosen = assignments.GroupBy(a => a.EmployeeId).Select(g =>
          g.FirstOrDefault(a => a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
          ?? g.Where(a => a.EffectiveFrom <= today).OrderByDescending(a => a.EffectiveFrom).FirstOrDefault()
          ?? g.OrderBy(a => a.EffectiveFrom).First())
        .ToList();

      var postIds = chosen.Select(a => a.PostId).Distinct().ToList();
      var versions = await context.PostVersions.AsNoTracking().Where(v => postIds.Contains(v.PostId))
        .Select(v => new { v.PostId, v.OrgUnitId, v.EffectiveFrom, v.EffectiveTo })
        .ToListAsync(cancellationToken);
      var byPost = versions.GroupBy(v => v.PostId).ToDictionary(g => g.Key, g => g.ToList());

      var inScope = new List<EmployeeId>();
      foreach (var a in chosen)
      {
        // the post's unit on the day that counts: today, kept inside the holding
        var day = today < a.EffectiveFrom ? a.EffectiveFrom : a.EffectiveTo is { } to && to < today ? to : today;
        if (!byPost.TryGetValue(a.PostId, out var postVersions))
          continue;
        var version = postVersions.FirstOrDefault(v => v.EffectiveFrom <= day && (v.EffectiveTo == null || v.EffectiveTo >= day))
          ?? postVersions.OrderByDescending(v => v.EffectiveFrom).First();
        if (units.Contains(version.OrgUnitId))
          inScope.Add(a.EmployeeId);
      }

      scope.Set(inScope);
    }
  }

  /// The offices in scope with every unit below them, as the structure stands today; empty when not limited.
  public async Task<IReadOnlySet<OrganizationUnitId>> UnitsAsync(CancellationToken cancellationToken)
  {
    if (_units is not null)
      return _units;
    if (currentUser.OfficeScope is not { } offices)
      return _units = [];

    var today = clock.Today;
    var versions = await context.OrganizationUnitVersions.AsNoTracking()
      .Where(v => v.EffectiveFrom <= today && (v.EffectiveTo == null || v.EffectiveTo >= today))
      .Select(v => new { v.OrgUnitId, v.ParentUnitId })
      .ToListAsync(cancellationToken);
    var children = versions.Where(v => v.ParentUnitId is not null).GroupBy(v => v.ParentUnitId!)
      .ToDictionary(g => g.Key, g => g.Select(v => v.OrgUnitId).ToList());

    var result = offices.Select(OrganizationUnitId.Of).ToHashSet();
    var queue = new Queue<OrganizationUnitId>(result);
    while (queue.Count > 0)
    {
      foreach (var child in children.GetValueOrDefault(queue.Dequeue()) ?? [])
      {
        if (result.Add(child))
          queue.Enqueue(child);
      }
    }
    return _units = result;
  }

  /// The employee is in scope (always true when not limited). Out of scope reads as not found.
  public async Task<bool> CoversAsync(EmployeeId employeeId, CancellationToken cancellationToken) =>
      !scope.IsLimited || await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken);

  /// Refuses (404, as if absent) an employee outside the scope.
  public async Task EnsureAsync(EmployeeId employeeId, CancellationToken cancellationToken)
  {
    if (!await CoversAsync(employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {employeeId.Value} was not found.");
  }

  /// The unit is one the person works on (or below one); always true when not limited.
  public async Task<bool> CoversUnitAsync(OrganizationUnitId unitId, CancellationToken cancellationToken) =>
      !scope.IsLimited || (await UnitsAsync(cancellationToken)).Contains(unitId);

  /// The refusal for an act over a whole payroll run or month, which needs every office; null when not limited.
  public async Task<string?> WholeOrganizationRefusalAsync(string act, CancellationToken cancellationToken)
  {
    if (!scope.IsLimited)
      return null;
    var names = await OfficeNamesAsync(cancellationToken);
    return $"You cannot {act}: it covers every office, and your access is limited to {names}. Ask someone who works on all offices.";
  }

  /// "Finance Wing, Nathia Gali Office" - the offices the person works on, for messages.
  public async Task<string> OfficeNamesAsync(CancellationToken cancellationToken)
  {
    if (currentUser.OfficeScope is not { Count: > 0 } offices)
      return "no office";
    using (scope.Unrestricted())
    {
      var ids = offices.Select(OrganizationUnitId.Of).ToList();
      var today = clock.Today;
      var versions = await context.OrganizationUnitVersions.AsNoTracking().Where(v => ids.Contains(v.OrgUnitId)).ToListAsync(cancellationToken);
      var names = versions.GroupBy(v => v.OrgUnitId)
        .Select(g => (g.FirstOrDefault(v => v.Range.Contains(today)) ?? g.OrderByDescending(v => v.EffectiveFrom).First()).Name)
        .OrderBy(n => n).ToList();
      return names.Count == 0 ? "offices that no longer exist" : string.Join(", ", names);
    }
  }
}

/// Works out the person's office scope before every request handler, so the database filter is ready.
public sealed class OfficeScopeBehavior<TRequest, TResponse>(OfficeScope scope) : IPipelineBehavior<TRequest, TResponse>
  where TRequest : notnull
{
  public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
  {
    await scope.LoadAsync(cancellationToken);
    return await next(cancellationToken);
  }
}
