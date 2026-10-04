using System.Text;

/// Ordering and paging for the cross-property lists that are merged in memory from several tables.
public static class ListSorting
{
  /// Rows without the value always come last, whichever direction.
  public static IOrderedEnumerable<T> NullsLast<T, TKey>(this IEnumerable<T> items, Func<T, TKey?> key, bool descending) where TKey : struct
  {
    var ordered = items.OrderBy(x => !key(x).HasValue);
    return descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
  }

  public static IOrderedEnumerable<T> ByText<T>(this IEnumerable<T> items, Func<T, string> key, bool descending) =>
      descending ? items.OrderByDescending(key, StringComparer.OrdinalIgnoreCase) : items.OrderBy(key, StringComparer.OrdinalIgnoreCase);

  public static List<T> Page<T>(this IEnumerable<T> items, PaginationRequest pagination) =>
      items.Skip(pagination.Pageindex * pagination.PageSize).Take(pagination.PageSize).ToList();

  /// A fixed status as {code, name}: the code is the value the rest of the API uses (UnderHearing), the name
  /// reads as words (Under hearing).
  public static StatusRef ToStatusRef<TEnum>(this TEnum value) where TEnum : struct, Enum
  {
    var code = value.ToString();
    var name = new StringBuilder(code.Length + 4);

    for (var i = 0; i < code.Length; i++)
    {
      if (i > 0 && char.IsUpper(code[i]))
        name.Append(' ').Append(char.ToLowerInvariant(code[i]));
      else
        name.Append(code[i]);
    }

    return new StatusRef(code, name.ToString());
  }

  public static StatusRef? ToStatusRef(this MasterRef? master) => master is null ? null : new StatusRef(master.Code, master.Name);
}
