public sealed record GetNextPropertyCodeQueryResult(string Code);

/// The code the next registration would get (PROP-00012). A preview only: nothing is reserved.
public sealed record GetNextPropertyCodeQuery : IQuery<Result<GetNextPropertyCodeQueryResult>>;
