using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// The service's audit trail, for what the interceptor cannot see on its own: an act with its own verb ("Approved",
/// "Reversed", "Exported"), a run over many records, the system acting. Lines join the DbContext and are saved with the
/// handler's next SaveChanges - in the same transaction as the change they describe.
public interface IAuditTrail
{
  /// One line. `record` is the entity the act was on (its kind, code, name and parent are read from it).
  void Record(string action, object record, string? detail = null, string? field = null, string? oldValue = null, string? newValue = null);

  /// One line about something that is not an entity (a register exported, a run).
  void Record(string action, string recordKind, Guid? recordId = null, string? recordCode = null, string? recordLabel = null,
    Guid? parentId = null, string? parentCode = null, string? detail = null, string? field = null, string? oldValue = null, string? newValue = null);

  /// Saves the lines now (for an act that saves nothing else, e.g. an export or a refused attempt).
  Task SaveAsync(CancellationToken cancellationToken = default);

  /// The automatic lines are not written inside this scope (a run's bulk rows); record one "Ran" line yourself.
  IDisposable Quietly();

  /// Inside this scope the actor is the system ("System · Daily job"), not a person - for scheduled and startup work.
  IDisposable ActingAs(string systemActor);
}

/// The ambient state of the audit trail in one async flow (a request, a job run).
public static class AuditScope
{
  private static readonly AsyncLocal<int> QuietDepth = new();
  private static readonly AsyncLocal<string?> SystemActor = new();

  public static bool IsQuiet => QuietDepth.Value > 0;
  public static string? Actor => SystemActor.Value;

  public static IDisposable Quietly()
  {
    QuietDepth.Value++;
    return new Restore(() => QuietDepth.Value--);
  }

  public static IDisposable ActingAs(string systemActor)
  {
    var previous = SystemActor.Value;
    SystemActor.Value = systemActor.StartsWith("System", StringComparison.Ordinal) ? systemActor : $"System · {systemActor}";
    return new Restore(() => SystemActor.Value = previous);
  }

  private sealed class Restore(Action restore) : IDisposable
  {
    private bool _done;
    public void Dispose()
    {
      if (_done) return;
      _done = true;
      restore();
    }
  }
}

/// Who an audit line names: the system when a scope says so, else the signed-in person, else "System".
public sealed class AuditActor(ICurrentUser currentUser, IRequestOrigin origin)
{
  public Guid? Id => AuditScope.Actor is null ? currentUser.UserId : null;
  public string Name => AuditScope.Actor ?? (currentUser.IsAuthenticated ? currentUser.AuditName : "System");
  public string? IpAddress => AuditScope.Actor is null ? origin.IpAddress : null;
  public string? Device => AuditScope.Actor is null ? origin.Device : null;
  public string? RequestId => origin.RequestId;
}

public sealed class AuditTrail<TContext>(TContext context, AuditProfile profile, AuditActor actor) : IAuditTrail where TContext : DbContext
{
  public void Record(string action, object record, string? detail = null, string? field = null, string? oldValue = null, string? newValue = null)
  {
    var type = record.GetType();
    var (code, label) = profile.Describe(record);
    var id = AuditValues.Guid(context.Entry(record).Metadata.FindPrimaryKey()?.Properties is [var key] ? context.Entry(record).Property(key.Name).CurrentValue : null);
    var parent = profile.ParentOf(record);
    Guid? parentId = parent is { } p ? AuditValues.Guid(p.Key) : null;
    string? parentCode = null;
    if (parent is { Key: not null } pk && context.Find(pk.Type, pk.Key) is { } parentEntity)
      parentCode = profile.Name(parentEntity);
    Add(action, profile.KindOf(type), id, code, label, parentId ?? id, parentCode, field, oldValue, newValue, detail);
  }

  public void Record(string action, string recordKind, Guid? recordId = null, string? recordCode = null, string? recordLabel = null,
      Guid? parentId = null, string? parentCode = null, string? detail = null, string? field = null, string? oldValue = null, string? newValue = null) =>
    Add(action, recordKind, recordId, recordCode, recordLabel, parentId, parentCode, field, oldValue, newValue, detail);

  public Task SaveAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);

  public IDisposable Quietly() => AuditScope.Quietly();

  public IDisposable ActingAs(string systemActor) => AuditScope.ActingAs(systemActor);

  private void Add(string action, string kind, Guid? recordId, string? code, string? label, Guid? parentId, string? parentCode, string? field,
      string? oldValue, string? newValue, string? detail) =>
    context.Set<AuditRecord>().Add(AuditRecord.Create(DateTimeOffset.UtcNow, profile.Module, actor.Id, actor.Name, action, kind, recordId, code, label,
      parentId, parentCode, field, oldValue, newValue, detail, actor.IpAddress, actor.Device, actor.RequestId));
}

public static class AuditTrailRegistration
{
  /// Registers the service's audit trail: the profile, `IAuditTrail`, and `AuditTrailInterceptor` (add it to the
  /// DbContext's interceptors: `options.AddInterceptors(sp.GetRequiredService<AuditTrailInterceptor>())`).
  public static IServiceCollection AddAuditTrail<TContext>(this IServiceCollection services, string module, Action<AuditProfile>? configure = null)
    where TContext : DbContext
  {
    var profile = new AuditProfile(module);
    configure?.Invoke(profile);
    services.AddSingleton(profile);
    services.AddScoped<AuditActor>();
    services.AddScoped<AuditTrailInterceptor>();
    services.AddScoped<IAuditTrail, AuditTrail<TContext>>();
    return services;
  }
}
