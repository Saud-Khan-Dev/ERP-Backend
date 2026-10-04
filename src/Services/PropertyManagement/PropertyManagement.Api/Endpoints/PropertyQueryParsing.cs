using Microsoft.AspNetCore.Mvc;

/// Query-string parsing shared by the register, the cross-property lists and the reports.
public static class PropertyQueryParsing
{
  /// Enum query values are case-insensitive (sortBy=area, kind=lease, status=completed).
  public static T? ParseEnum<T>(string? raw, string name) where T : struct, Enum
  {
    if (string.IsNullOrWhiteSpace(raw)) return null;
    if (Enum.TryParse<T>(raw.Trim(), ignoreCase: true, out var value) && Enum.IsDefined(value) && !int.TryParse(raw, out _)) return value;
    throw new BadHttpRequestException($"'{raw}' is not a valid {name}. Use one of: {string.Join(", ", Enum.GetNames<T>().Select(n => n.ToLowerInvariant()))}.");
  }

  /// A repeatable enum parameter: ?kind=lease&kind=rental (a comma list ?kind=lease,rental works too).
  public static List<T> ParseEnums<T>(IEnumerable<string?>? values, string name) where T : struct, Enum =>
      (values ?? [])
        .SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Select(v => ParseEnum<T>(v, name)!.Value)
        .Distinct()
        .ToList();

  /// sortDir=asc|desc; null when not given, so each list keeps its own default direction.
  public static bool? ParseDescending(string? sortDir) => sortDir?.Trim().ToLowerInvariant() switch
  {
    null or "" => null,
    "asc" => false,
    "desc" => true,
    _ => throw new BadHttpRequestException($"'{sortDir}' is not a valid sortDir. Use asc or desc.")
  };
}

/// The register's filters as query parameters (GET /properties and GET /property-reports/register); the names are
/// pinned so the OpenAPI document shows them as they are sent (search, townId ...).
public sealed record PropertyFilterParameters(
  [FromQuery(Name = "search")] string? Search = null,
  [FromQuery(Name = "townId")] Guid? TownId = null,
  [FromQuery(Name = "propertyTypeId")] Guid? PropertyTypeId = null,
  [FromQuery(Name = "propertyStatusId")] Guid? PropertyStatusId = null,
  [FromQuery(Name = "propertyClassificationId")] Guid? PropertyClassificationId = null,
  [FromQuery(Name = "includeInactive")] bool? IncludeInactive = null,
  [FromQuery(Name = "registeredFrom")] DateTime? RegisteredFrom = null,
  [FromQuery(Name = "registeredTo")] DateTime? RegisteredTo = null,
  [FromQuery(Name = "areaMin")] decimal? AreaMin = null,
  [FromQuery(Name = "areaMax")] decimal? AreaMax = null,
  [FromQuery(Name = "ownerId")] Guid? OwnerId = null,
  [FromQuery(Name = "hasOpenEncroachment")] bool? HasOpenEncroachment = null,
  [FromQuery(Name = "hasOpenCase")] bool? HasOpenCase = null,
  [FromQuery(Name = "sortBy")] string? SortBy = null,
  [FromQuery(Name = "sortDir")] string? SortDir = null)
{
  public GetPropertiesQuery ToQuery(PaginationRequest pagination) => new(
    pagination, Search, TownId, PropertyTypeId, PropertyStatusId, PropertyClassificationId, IncludeInactive ?? false,
    RegisteredFrom, RegisteredTo, AreaMin, AreaMax, OwnerId, HasOpenEncroachment, HasOpenCase,
    PropertyQueryParsing.ParseEnum<PropertySort>(SortBy, "sortBy") ?? PropertySort.Code,
    PropertyQueryParsing.ParseDescending(SortDir) ?? false);
}
