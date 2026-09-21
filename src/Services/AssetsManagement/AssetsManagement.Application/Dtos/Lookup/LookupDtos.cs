public sealed record AssetStatusDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  bool IsTerminal,
  bool AllowsAssignment,
  string? Color,
  int? DisplayOrder,
  bool IsActive);

public sealed record CurrencyLookupDto(
  string Code,
  string Name,
  string? Symbol,
  short MinorUnits,
  bool IsActive);

public sealed record LocationDto(
  Guid Id,
  Guid? ParentLocationId,
  string Code,
  string Name,
  string? LocationType,
  string? Path,
  string? Address,
  decimal? Latitude,
  decimal? Longitude,
  bool IsActive);

public sealed record LifecycleEventTypeDto(
  Guid Id,
  string? Stage,
  string Code,
  string Name,
  string? Description,
  bool IsActive);

public sealed record DepreciationMethodDto(
  Guid Id,
  string Code,
  string Name,
  string? Description,
  bool IsActive);

public sealed record DisposalMethodDto(
  Guid Id,
  string Code,
  string Name,
  bool RequiresValue,
  bool IsActive);

public static class LookupMappings
{
  public static AssetStatusDto ToDto(this AssetStatus x) => new(
    x.Id.Value, x.Code.Value, x.Name.Value, x.Description, x.IsTerminal, x.AllowsAssignment, x.Color, x.DisplayOrder, x.IsActive);

  public static CurrencyLookupDto ToDto(this CurrencyLookup x) => new(
    x.Id.Value, x.Name.Value, x.Symbol, x.MinorUnits, x.IsActive);

  public static LocationDto ToDto(this Location x) => new(
    x.Id.Value, x.ParentLocationId?.Value, x.Code.Value, x.Name.Value, x.LocationType, x.Path,
    x.Address, x.Latitude, x.Longitude, x.IsActive);

  public static LifecycleEventTypeDto ToDto(this LifecycleEventType x) => new(
    x.Id.Value, x.Stage, x.Code.Value, x.Name.Value, x.Description, x.IsActive);

  public static DepreciationMethodDto ToDto(this DepreciationMethod x) => new(
    x.Id.Value, x.Code.Value, x.Name.Value, x.Description, x.IsActive);

  public static DisposalMethodDto ToDto(this DisposalMethod x) => new(
    x.Id.Value, x.Code.Value, x.Name.Value, x.RequiresValue, x.IsActive);
}
