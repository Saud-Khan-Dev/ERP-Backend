public sealed record GetEncroachmentsQueryResult(IReadOnlyList<EncroachmentDto> Encroachments);
public sealed record GetEncroachmentQueryResult(EncroachmentDto Encroachment);

public sealed record GetPropertyEncroachmentsQuery(Guid PropertyId, bool UnresolvedOnly = false) : IQuery<Result<GetEncroachmentsQueryResult>>;

public sealed record GetEncroachmentQuery(Guid Id) : IQuery<Result<GetEncroachmentQueryResult>>;
