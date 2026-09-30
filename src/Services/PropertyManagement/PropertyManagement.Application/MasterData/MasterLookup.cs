using Microsoft.EntityFrameworkCore;

/// Typed access to master rows from handlers: load-and-check by id, find by code, and batch-resolve ids
/// into {id, code, name} for DTOs.
public class MasterLookup(IApplicationDbContext context)
{
  public async Task<T> GetAsync<T>(Guid id, CancellationToken cancellationToken) where T : MasterData
  {
    var masterId = MasterId.Of(id);

    return await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == masterId, cancellationToken)
      ?? throw new MasterDataNotFoundException($"{MasterRegistry.For<T>().Label} {id} was not found.");
  }

  public async Task<T?> GetOptionalAsync<T>(Guid? id, CancellationToken cancellationToken) where T : MasterData =>
      id is null || id == Guid.Empty ? null : await GetAsync<T>(id.Value, cancellationToken);

  /// Rule 9: code, never id. Used for the few codes the application relies on (SystemMasterCodes).
  public async Task<T> GetByCodeAsync<T>(string code, CancellationToken cancellationToken) where T : MasterData
  {
    var masterCode = MasterCode.Of(code);

    return await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(m => m.Code == masterCode, cancellationToken)
      ?? throw new MasterDataNotFoundException($"{MasterRegistry.For<T>().Label} with code {masterCode.Value} was not found. Add it under master data.");
  }

  public MasterRefsBuilder Refs() => new(context);
}

/// Collects master ids from a page of results, then resolves them with one query per master table.
public sealed class MasterRefsBuilder(IApplicationDbContext context)
{
  private readonly Dictionary<MasterDescriptor, HashSet<MasterId>> _wanted = new();

  public MasterRefsBuilder Add<T>(IEnumerable<MasterId?> ids) where T : MasterData
  {
    var descriptor = MasterRegistry.For<T>();

    if (!_wanted.TryGetValue(descriptor, out var set))
      _wanted[descriptor] = set = new HashSet<MasterId>();

    foreach (var id in ids)
      if (id is not null)
        set.Add(id);

    return this;
  }

  public MasterRefsBuilder Add<T>(MasterId? id) where T : MasterData => Add<T>(new[] { id });

  public async Task<MasterRefs> LoadAsync(CancellationToken cancellationToken)
  {
    var refs = new Dictionary<MasterId, MasterRef>();

    foreach (var (descriptor, ids) in _wanted.Where(w => w.Value.Count > 0))
      foreach (var master in await descriptor.LoadAsync(context, ids, cancellationToken))
        refs[master.Id] = master.ToRef();

    return new MasterRefs(refs);
  }
}

public sealed class MasterRefs(IReadOnlyDictionary<MasterId, MasterRef> refs)
{
  public MasterRef? this[MasterId? id] => id is null ? null : refs.GetValueOrDefault(id);
}
