using Microsoft.EntityFrameworkCore;

/// Shared by the handlers that change a record hanging off a property (lease, case, plan ...): the record's
/// property must be active (Property.EnsureActive), and "today" for the date rules.
public static class PropertyWriteGuard
{
  /// Today's date for the "cannot be in the future" and "only after the end date" rules.
  public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

  /// Refuses the change when the record's property is inactive ("Property PROP-00001 is inactive ...").
  public static async Task<Property> EnsurePropertyActiveAsync(this IApplicationDbContext context, PropertyId propertyId, CancellationToken cancellationToken)
  {
    var property = await context.Properties.FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
      ?? throw new PropertyNotFoundException($"Property {propertyId.Value} was not found.");

    property.EnsureActive();
    return property;
  }
}
