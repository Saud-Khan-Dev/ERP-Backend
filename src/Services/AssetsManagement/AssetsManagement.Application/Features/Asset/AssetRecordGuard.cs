using Microsoft.EntityFrameworkCore;

/// A disposed asset (or one in a final status) has a closed record: what was on file when it left is what stays -
/// no files, finance or lifecycle changes. A deleted asset takes no changes at all.
public static class AssetRecordGuard
{
  /// The sentence to refuse a change with, or null while the record is open.
  public static async Task<string?> ClosedReasonAsync(IApplicationDbContext context, Asset asset, CancellationToken cancellationToken)
  {
    if (asset.IsDeleted)
      return "This asset has been deleted.";

    if (await context.AssetDisposals.AsNoTracking().AnyAsync(d => d.AssetId == asset.Id && d.State == DisposalState.Approved, cancellationToken))
      return "This asset has been disposed of. Its record is closed and cannot change any more.";

    var status = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == asset.StatusId, cancellationToken);
    return status is { IsTerminal: true }
      ? $"This asset is '{status.Name.Value}'. Its record is closed and cannot change any more."
      : null;
  }

  /// Refuses the change (a 400 with the sentence) when the record is closed.
  public static async Task EnsureOpenAsync(IApplicationDbContext context, Asset asset, CancellationToken cancellationToken)
  {
    if (await ClosedReasonAsync(context, asset, cancellationToken) is { } closed)
      throw new DomainException(closed);
  }

  /// The asset (not deleted) or a 404.
  public static async Task<Asset> LoadAsync(IApplicationDbContext context, AssetId id, bool track, CancellationToken cancellationToken)
  {
    var query = track ? context.Assets : context.Assets.AsNoTracking();
    return await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AssetNotFoundException("This asset was not found. It may have been deleted.");
  }

  /// The asset a schedule belongs to, which must be open.
  public static async Task<Asset> EnsureScheduleAssetOpenAsync(IApplicationDbContext context, AssetId assetId, CancellationToken cancellationToken)
  {
    var asset = await context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
      ?? throw new AssetNotFoundException("The asset of this schedule was not found. It may have been deleted.");
    await EnsureOpenAsync(context, asset, cancellationToken);
    return asset;
  }
}

/// The SYSTEM timeline types the acts write, found by their purpose (never by code).
public static class SystemEvents
{
  public static async Task<LifecycleEventType> TypeAsync(IApplicationDbContext context, EventPurpose purpose, CancellationToken cancellationToken) =>
      await context.LifecycleEventTypes.AsNoTracking().FirstOrDefaultAsync(t => t.IsSystem && t.Purpose == purpose, cancellationToken)
      ?? throw new DomainException("The register's timeline types are missing - restart the Assets service so it can set them up.");

  /// Adds the event of the purpose to the context (the caller saves).
  public static async Task<AssetLifecycleEvent> AddAsync(
      IApplicationDbContext context, AssetId assetId, EventPurpose purpose, DateTime eventDate, ActorRef actor,
      string? notes = null, System.Text.Json.JsonElement? details = null,
      AssetStatusId? fromStatusId = null, AssetStatusId? toStatusId = null, CancellationToken cancellationToken = default)
  {
    var type = await TypeAsync(context, purpose, cancellationToken);
    var lifecycleEvent = AssetLifecycleEvent.Create(
      AssetLifecycleEventId.Of(Guid.NewGuid()), assetId, type, eventDate, fromStatusId, toStatusId, actor, notes, details);
    await context.AssetLifecycleEvents.AddAsync(lifecycleEvent, cancellationToken);
    return lifecycleEvent;
  }

  /// "What changed" for a details-changed event: { "Name": "old → new", … }, or null when nothing changed.
  public static System.Text.Json.JsonElement? Changes(IReadOnlyList<(string Field, string? Before, string? After)> changes)
  {
    var changed = changes.Where(c => !string.Equals(c.Before ?? "", c.After ?? "", StringComparison.Ordinal)).ToList();
    if (changed.Count == 0)
      return null;
    var map = changed.ToDictionary(c => c.Field, c => $"{(string.IsNullOrWhiteSpace(c.Before) ? "—" : c.Before)} → {(string.IsNullOrWhiteSpace(c.After) ? "—" : c.After)}");
    return System.Text.Json.JsonSerializer.SerializeToElement(map);
  }
}

/// Custodian and department, checked against HRM's staff directory, with the names shown for them.
public static class CustodyResolver
{
  /// `departmentId` defaults to the custodian's unit. A person who has left service cannot hold assets.
  public static async Task<CustodyRef> ResolveAsync(IHrmDirectory hrm, Guid? custodianId, Guid? departmentId, CancellationToken cancellationToken)
  {
    if (custodianId is null && departmentId is null)
      return CustodyRef.None;

    StaffMember? person = null;
    if (custodianId is { } personId)
    {
      person = await hrm.EmployeeAsync(personId, cancellationToken)
        ?? throw new DomainException("The custodian is not on HR's staff list. Choose the person again.");
      if (!person.InService)
        throw new DomainException($"{person.Name} ({person.EmployeeNumber}) is no longer in service ({Plain(person.Status)}) and cannot be given assets.");
    }

    OrgUnitRef? unit = null;
    var unitId = departmentId ?? person?.OrgUnitId;
    if (unitId is { } wanted)
    {
      var units = await hrm.OrgUnitsAsync(cancellationToken);
      unit = units.FirstOrDefault(u => u.Id == wanted);
      if (unit is null && departmentId is not null)
        throw new DomainException("The department is not on the organisation's structure in HR. Choose it again.");
    }

    return new CustodyRef(unit?.Id, unit?.Name, person?.Id, person?.Name, person?.EmployeeNumber);
  }

  private static string Plain(string status) =>
      string.Concat(status.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : (i == 0 ? char.ToLowerInvariant(c).ToString() : c.ToString())));
}

/// Finance rights a handler checks beyond the endpoint's own permission.
public static class FinanceRights
{
  public static void Require(ICurrentUser user, string permission, string sentence)
  {
    if (!user.HasPermission(permission))
      throw new AccessDeniedException(sentence);
  }

  /// Any of the permissions, or a 403 with the sentence.
  public static void RequireAny(ICurrentUser user, string sentence, params string[] permissions)
  {
    if (!permissions.Any(user.HasPermission))
      throw new AccessDeniedException(sentence);
  }
}
