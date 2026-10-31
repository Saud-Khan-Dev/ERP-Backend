/// One queue of work that waits for someone, counted for the caller - what the bell and the module overviews list.
/// Every service answers `GET /attention` (any signed-in person) with `{ "attention": AttentionDto }`, holding only the
/// queues the caller may act on; a zero count means "checked, nothing waits", a missing key "not yours".
///
/// Key: stable, dotted, lower case ("leave.pending") - the frontend gives it its words and its link.
/// Count: exact (a COUNT, never an estimate).
/// Oldest: when the longest-waiting record started waiting (applied, submitted, fell due).
/// NextDue: for work coming up (a hearing this week), the nearest date.
/// ForMe: work assigned to the caller personally (their task).
public sealed record AttentionItemDto(string Key, int Count, DateTimeOffset? Oldest = null, DateOnly? NextDue = null, bool ForMe = false);

public sealed record AttentionDto(DateTimeOffset AsOf, IReadOnlyList<AttentionItemDto> Items);

public sealed record AttentionResponse(AttentionDto Attention);
