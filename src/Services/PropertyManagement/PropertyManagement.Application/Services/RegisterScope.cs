using System.Linq.Expressions;

/// The property and search filters the cross-property lists (agreements, legal matters, transfers) share.
/// Everything here is a sub-query, so each list still filters in SQL.
public sealed class RegisterScope
{
  /// The properties the lists may draw from: the active ones (a deactivated property has left the register,
  /// so its agreements and cases no longer count), narrowed by the propertyId / townId filters. A property
  /// asked for by its id is included even when deactivated.
  public IQueryable<PropertyId>? Properties { get; private init; }

  /// The search term, upper- and lower-cased; null = no search.
  public string? Upper { get; private init; }
  public string? Lower { get; private init; }

  /// Properties whose code or name matches the search, and owners whose code or name does.
  public IQueryable<PropertyId> SearchProperties { get; private init; } = default!;
  public IQueryable<OwnerId> SearchOwners { get; private init; } = default!;

  public bool HasSearch => Upper is not null;

  public static RegisterScope Create(IApplicationDbContext context, Guid? propertyId, Guid? townId, string? search)
  {
    var properties = context.Properties.AsQueryable();
    if (propertyId is { } pid)
    {
      var id = PropertyId.Of(pid);
      properties = properties.Where(p => p.Id == id);
    }
    else
    {
      properties = properties.Where(p => p.IsActive);
    }

    if (townId is { } tid)
    {
      var town = MasterId.Of(tid);
      properties = properties.Where(p => p.TownId == town);
    }

    IQueryable<PropertyId>? allowed = properties.Select(p => p.Id);

    if (string.IsNullOrWhiteSpace(search))
      return new RegisterScope { Properties = allowed };

    var upper = search.Trim().ToUpperInvariant();
    var lower = search.Trim().ToLowerInvariant();

    // codes are value-converted BusinessCodes: the cast through object lets EF match part of the column text
    return new RegisterScope
    {
      Properties = allowed,
      Upper = upper,
      Lower = lower,
      SearchProperties = context.Properties
        .Where(p => ((string)(object)p.PropertyCode).Contains(upper) || p.PropertyName.Value.ToLower().Contains(lower))
        .Select(p => p.Id),
      SearchOwners = context.Owners
        .Where(o => ((string)(object)o.OwnerCode).Contains(upper) || o.OwnerName.Value.ToLower().Contains(lower))
        .Select(o => o.Id)
    };
  }
}

public static class DateRangeFilter
{
  /// Keeps the rows whose date (an expression over the row, translated to SQL) lies in [from, to]; a row
  /// without that date is left out as soon as either end is given.
  public static IQueryable<T> InRange<T>(this IQueryable<T> source, Expression<Func<T, DateOnly?>> date, DateOnly? from, DateOnly? to)
  {
    if (from is { } start)
      source = source.Where(Compare(date, start, ExpressionType.GreaterThanOrEqual));

    if (to is { } end)
      source = source.Where(Compare(date, end, ExpressionType.LessThanOrEqual));

    return source;
  }

  private static Expression<Func<T, bool>> Compare<T>(Expression<Func<T, DateOnly?>> date, DateOnly bound, ExpressionType comparison)
  {
    // read through a closure so EF sends the bound as a parameter, not a literal
    Expression<Func<DateOnly?>> value = () => bound;
    return Expression.Lambda<Func<T, bool>>(Expression.MakeBinary(comparison, date.Body, value.Body), date.Parameters);
  }
}
