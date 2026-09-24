public sealed record GetMeQueryResult(CurrentUserDto User);

/// The caller's own identity and effective permissions. This is what lets a UI hide actions the
/// user cannot perform — while the backend still enforces them independently.
public sealed record GetMeQuery() : IQuery<Result<GetMeQueryResult>>;
