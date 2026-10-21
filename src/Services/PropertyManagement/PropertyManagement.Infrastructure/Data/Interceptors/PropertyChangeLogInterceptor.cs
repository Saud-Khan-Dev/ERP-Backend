using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// Writes the change log (PropertyChange) in the same save as the change: for every record hanging off a property (it has
/// a PropertyId, or is the property) and every owner record, an "Added" / "Removed" line, or one "Changed" line per fact
/// that changed, old -> new. A value pointing at a list value or an owner is written as its name, never an id. The
/// bookkeeping columns (created / updated stamps, row version) are left out.
public sealed class PropertyChangeLogInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
  private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal)
  {
    nameof(IEntity.CreatedAt), nameof(IEntity.CreatedBy), nameof(IEntity.UpdatedAt), nameof(IEntity.UpdatedBy), "RowVersion", "Id"
  };

  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    if (eventData.Context is { } context) Write(context);
    return base.SavingChanges(eventData, result);
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    if (eventData.Context is { } context) Write(context);
    return base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private void Write(DbContext context)
  {
    context.ChangeTracker.DetectChanges();
    var now = DateTime.UtcNow;
    var lines = new List<PropertyChange>();

    foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
    {
      if (entry.Entity is PropertyChange)
        continue;

      var (propertyId, ownerId) = Scope(entry);
      if (propertyId is null && ownerId is null)
        continue;

      var entity = Words(entry.Metadata.ClrType.Name.StartsWith("Property") && entry.Metadata.ClrType != typeof(Property)
        ? entry.Metadata.ClrType.Name["Property".Length..] : entry.Metadata.ClrType.Name);
      var entityId = Guid.TryParse(Provider(entry.Property("Id")), out var id) ? id : Guid.Empty;
      var code = CodeOf(entry);

      if (entry.State is EntityState.Added or EntityState.Deleted)
      {
        lines.Add(PropertyChange.Log(propertyId, ownerId, entity, entityId, code, entry.State == EntityState.Added ? "Added" : "Removed",
          null, null, null, currentUser.UserId, currentUser.Username, now));
        continue;
      }

      foreach (var property in entry.Properties.Where(p => p.IsModified && !Skipped.Contains(p.Metadata.Name)))
      {
        var before = Display(context, entry, property, property.OriginalValue);
        var after = Display(context, entry, property, property.CurrentValue);
        if (before == after)
          continue;

        lines.Add(PropertyChange.Log(propertyId, ownerId, entity, entityId, code, "Changed", Words(property.Metadata.Name),
          before, after, currentUser.UserId, currentUser.Username, now));
      }
    }

    if (lines.Count > 0)
      context.Set<PropertyChange>().AddRange(lines);
  }

  /// The property (or owner) the change is logged under.
  private static (Guid? PropertyId, Guid? OwnerId) Scope(EntityEntry entry)
  {
    if (entry.Entity is Property property)
      return (property.Id.Value, null);

    if (entry.Entity is PropertyOwner owner)
      return (null, owner.Id.Value);

    var propertyId = entry.Metadata.FindProperty("PropertyId");
    if (propertyId is not null && Guid.TryParse(Provider(entry.Property("PropertyId")), out var id))
      return (id, null);

    return (null, null);
  }

  private static string? CodeOf(EntityEntry entry) =>
      entry.Properties.FirstOrDefault(p => p.Metadata.ClrType == typeof(BusinessCode))?.CurrentValue is BusinessCode code ? code.Value
      : entry.Entity is PropertyLitigation litigation ? litigation.CaseNo : null;

  private static string? Provider(PropertyEntry property)
  {
    var value = property.CurrentValue;
    var converter = property.Metadata.GetValueConverter();
    var raw = value is null ? null : converter is null ? value : converter.ConvertToProvider(value);
    return raw?.ToString();
  }

  /// A value as a person reads it: a list value or owner by name, a date as yyyy-MM-dd, a yes / no.
  private static string? Display(DbContext context, EntityEntry entry, PropertyEntry property, object? value)
  {
    if (value is null)
      return null;

    var foreignKey = entry.Metadata.FindForeignKeys(property.Metadata).FirstOrDefault();
    if (foreignKey is not null)
    {
      var target = context.Find(foreignKey.PrincipalEntityType.ClrType, value);
      return target switch
      {
        MasterData master => master.Name.Value,
        PropertyOwner owner => $"{owner.OwnerName.Value} ({owner.OwnerCode.Value})",
        // another record (the transfer or allotment a share came from ...) by its code
        { } other when other.GetType().GetProperties().FirstOrDefault(p => p.PropertyType == typeof(BusinessCode))?.GetValue(other) is BusinessCode code => code.Value,
        _ => Text(property, value)
      };
    }

    return Text(property, value);
  }

  private static string? Text(PropertyEntry property, object value) => value switch
  {
    DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    DateTime at => at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
    bool yes => yes ? "Yes" : "No",
    decimal number => number.ToString("0.####", CultureInfo.InvariantCulture),
    Name name => name.Value,
    BusinessCode code => code.Value,
    MasterCode code => code.Value,
    Enum e => Words(e.ToString()),
    System.Collections.IEnumerable list and not string => string.Join(", ", list.Cast<object>()),
    _ => property.Metadata.GetValueConverter() is { } converter ? converter.ConvertToProvider(value)?.ToString() : value.ToString()
  };

  /// "LeaseEndDate" -> "Lease end date".
  private static string Words(string name)
  {
    var spaced = Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", " $1").ToLowerInvariant();
    return char.ToUpperInvariant(spaced[0]) + spaced[1..];
  }
}
