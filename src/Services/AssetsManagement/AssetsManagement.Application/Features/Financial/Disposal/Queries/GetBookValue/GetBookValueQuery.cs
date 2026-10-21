/// The asset's book value on a date (default today), as the disposal would record it: the cost, what is written off
/// (posted months ended by then), and how many months up to that date are not posted yet (a disposal posts them first).
public sealed record GetBookValueQueryResult(
  DateOnly AsOf,
  decimal? Cost,
  string? CurrencyCode,
  decimal WrittenOff,
  decimal? BookValue,
  DateOnly? PostedThrough,
  int MonthsToPost,
  decimal AmountToPost);

public sealed record GetBookValueQuery(Guid AssetId, DateOnly? AsOf) : IQuery<Result<GetBookValueQueryResult>>;
