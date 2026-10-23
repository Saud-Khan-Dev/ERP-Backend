using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

/// Writes the audit trail in the same save as the change: for every record added, changed or removed, who did it, when,
/// from where, and - for a change - each fact's value before and after, in words (a unit's name, not its id).
///
/// - Added: one line; Detail lists the facts it was created with.
/// - Changed: one line per fact that changed.
/// - Removed: one line; Detail keeps every fact it had, so a deletion never loses what was there.
/// A record owned by another (an address inside an employee) is written as a fact of its owner.
public sealed class AuditTrailInterceptor(AuditProfile profile, AuditActor actor) : SaveChangesInterceptor
{
  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    if (eventData.Context is { } context)
      Write(context);
    return base.SavingChanges(eventData, result);
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
      CancellationToken cancellationToken = default)
  {
    if (eventData.Context is { } context)
      Write(context);
    return base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private void Write(DbContext context)
  {
    if (AuditScope.IsQuiet)
      return;
    context.ChangeTracker.DetectChanges();
    var entries = context.ChangeTracker.Entries()
      .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && !profile.IsSkipped(e.Entity.GetType()))
      .ToList();
    if (entries.Count == 0)
      return;

    var now = DateTimeOffset.UtcNow;
    var names = new Dictionary<(Type, object), string?>();
    var lines = new List<AuditRecord>();

    foreach (var entry in entries)
    {
      // an owned record is a part of its owner: write it under the owner's name
      var (subject, prefix) = OwnerOf(context, entry);
      var subjectType = subject?.Entity.GetType() ?? entry.Entity.GetType();
      if (subject is not null && profile.IsSkipped(subjectType))
        continue;
      var kind = profile.KindOf(subjectType);
      var (code, label) = profile.Describe(subject?.Entity ?? entry.Entity);
      var recordId = KeyOf(subject ?? entry);
      var (parentId, parentCode) = ParentOf(context, subject?.Entity ?? entry.Entity, names);
      parentId ??= recordId;

      void Line(string action, string? field, string? oldValue, string? newValue, string? detail) =>
        // one microsecond apart (Postgres keeps microseconds): every line has its own moment, so paging by `before`
        // never skips the rest of a save that a page boundary fell into
        lines.Add(AuditRecord.Create(now.AddTicks(-10 * lines.Count), profile.Module, actor.Id, actor.Name, action, kind, recordId, code, label, parentId, parentCode, field,
          oldValue, newValue, detail, actor.IpAddress, actor.Device, actor.RequestId));

      // plain columns, then the members of complex properties (a name held as { Value }), each with its words
      var entityType = entry.Entity.GetType();
      var properties = entry.Properties
        .Where(p => !p.Metadata.IsPrimaryKey() && !profile.IsIgnored(entityType, p.Metadata.Name) && !IsAccountId(p)
          && !(subject is not null && p.Metadata.IsForeignKey() && p.Metadata.GetContainingForeignKeys().Any(fk => fk.IsOwnership)))
        .Select(p => (Entry: p, Field: prefix + profile.FieldOf(entityType, p.Metadata.Name)))
        .Concat(ComplexMembers(entry.ComplexProperties, entityType, prefix))
        .ToList();

      switch (entry.State)
      {
        case EntityState.Added when subject is null:
          Line(AuditActions.Added, null, null, null, Facts(context, properties, p => p.CurrentValue, names));
          break;
        case EntityState.Added:
          // a part added to an existing owner reads as its facts being set
          foreach (var (p, field) in properties.Where(x => x.Entry.CurrentValue is not null))
            Line(AuditActions.Changed, field, null, Value(context, p, p.CurrentValue, names), null);
          break;
        case EntityState.Modified:
          foreach (var (p, field) in properties.Where(x => x.Entry.IsModified && !Equals(x.Entry.OriginalValue, x.Entry.CurrentValue)))
            Line(AuditActions.Changed, field, Value(context, p, p.OriginalValue, names), Value(context, p, p.CurrentValue, names), null);
          break;
        case EntityState.Deleted when subject is null:
          Line(AuditActions.Removed, null, null, null, Facts(context, properties, p => p.OriginalValue, names));
          break;
        case EntityState.Deleted:
          foreach (var (p, field) in properties.Where(x => x.Entry.OriginalValue is not null))
            Line(AuditActions.Changed, field, Value(context, p, p.OriginalValue, names), null, null);
          break;
      }
    }

    if (lines.Count > 0)
      context.Set<AuditRecord>().AddRange(lines);
  }

  /// The owner entry of an owned record and the words to put before its facts ("Address · "), or (null, "").
  private (EntityEntry? Owner, string Prefix) OwnerOf(DbContext context, EntityEntry entry)
  {
    var ownership = entry.Metadata.FindOwnership();
    if (ownership is null)
      return (null, "");
    var keyValues = ownership.Properties.Select(p => entry.Property(p.Name).CurrentValue).ToArray();
    var owner = context.ChangeTracker.Entries().FirstOrDefault(e => e.Metadata == ownership.PrincipalEntityType
      && ownership.PrincipalKey.Properties.Select(p => e.Property(p.Name).CurrentValue).SequenceEqual(keyValues));
    if (owner is null)
      return (null, "");
    var (outer, outerPrefix) = OwnerOf(context, owner);
    var navigation = ownership.PrincipalToDependent?.Name;
    var prefix = outerPrefix + (navigation is null ? "" : AuditProfile.Words(navigation) + " · ");
    return (outer ?? owner, prefix);
  }

  /// The members of complex properties, nested ones included: "Name" for `Name { Value }`, "Address · City" otherwise.
  private IEnumerable<(PropertyEntry Entry, string Field)> ComplexMembers(IEnumerable<ComplexPropertyEntry> complexes, Type entityType, string prefix)
  {
    foreach (var complex in complexes)
    {
      if (profile.IsIgnored(entityType, complex.Metadata.Name))
        continue;
      var words = prefix + profile.FieldOf(entityType, complex.Metadata.Name);
      foreach (var member in complex.Properties)
        yield return (member, member.Metadata.Name == "Value" ? words : $"{words} · {AuditProfile.Words(member.Metadata.Name)}");
      foreach (var inner in ComplexMembers(complex.ComplexProperties, entityType, words + " · "))
        yield return inner;
    }
  }

  private static Guid? KeyOf(EntityEntry entry) =>
    entry.Metadata.FindPrimaryKey()?.Properties is [var key] ? AuditValues.Guid(entry.Property(key.Name).CurrentValue) : null;

  private (Guid? Id, string? Code) ParentOf(DbContext context, object entity, Dictionary<(Type, object), string?> names)
  {
    if (profile.ParentOf(entity) is not { } parent || parent.Key is null)
      return (null, null);
    return (AuditValues.Guid(parent.Key), NameOf(context, parent.Type, parent.Key, names));
  }

  /// The facts of a whole record, "Field: value · Field: value", nulls left out.
  private string? Facts(DbContext context, IEnumerable<(PropertyEntry Entry, string Field)> properties, Func<PropertyEntry, object?> pick,
      Dictionary<(Type, object), string?> names)
  {
    var facts = properties
      .Select(p => (p.Field, Value: Value(context, p.Entry, pick(p.Entry), names)))
      .Where(f => !string.IsNullOrWhiteSpace(f.Value))
      .Select(f => $"{f.Field}: {f.Value}")
      .ToList();
    return facts.Count == 0 ? null : string.Join(" · ", facts);
  }

  /// A foreign key as the name of what it points to; any other value as text.
  private string? Value(DbContext context, PropertyEntry property, object? value, Dictionary<(Type, object), string?> names)
  {
    if (value is null)
      return null;
    var foreignKey = property.Metadata.GetContainingForeignKeys().FirstOrDefault(fk => !fk.IsOwnership && fk.Properties.Count == 1);
    if (foreignKey is not null)
      // a record with neither code nor name (an ownership share) reads as its kind, never as a bare id
      return NameOf(context, foreignKey.PrincipalEntityType.ClrType, value, names) ?? profile.KindOf(foreignKey.PrincipalEntityType.ClrType);
    return AuditValues.Text(value);
  }

  /// A sign-in account's id kept on a record ("ApprovedByUserId", "PostedById"): Identity's id means nothing to a reader,
  /// and the line's actor - or the name column beside it - already says who. Left out of the trail.
  private static bool IsAccountId(PropertyEntry property)
  {
    var type = Nullable.GetUnderlyingType(property.Metadata.ClrType) ?? property.Metadata.ClrType;
    if (type != typeof(Guid) || property.Metadata.IsForeignKey())
      return false;
    var name = property.Metadata.Name;
    return name.EndsWith("UserId", StringComparison.Ordinal) || name.EndsWith("ById", StringComparison.Ordinal)
      || (name.EndsWith("By", StringComparison.Ordinal) && name.Length > 2);
  }

  /// "EMP-004 · Ayesha Noor" for a key of `type`: from the records already loaded, else one lookup; remembered per save.
  private string? NameOf(DbContext context, Type type, object key, Dictionary<(Type, object), string?> names)
  {
    if (names.TryGetValue((type, key), out var known))
      return known;
    string? name = null;
    try
    {
      var entityType = context.Model.FindEntityType(type);
      if (entityType?.FindPrimaryKey()?.Properties is [var pk] && pk.ClrType == key.GetType())
      {
        var tracked = context.ChangeTracker.Entries().FirstOrDefault(e => e.Metadata == entityType && Equals(e.Property(pk.Name).CurrentValue, key));
        var entity = tracked?.Entity ?? context.Find(type, key);
        if (entity is not null)
          name = profile.Name(entity);
      }
    }
    catch (InvalidOperationException)
    {
      // a key the model cannot look up (a shadow key, a keyless type): the id itself is written
    }
    names[(type, key)] = name;
    return name;
  }
}
