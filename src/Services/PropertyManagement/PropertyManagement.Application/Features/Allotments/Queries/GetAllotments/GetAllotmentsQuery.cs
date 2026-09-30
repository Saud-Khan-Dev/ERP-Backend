public sealed record GetAllotmentsQueryResult(IReadOnlyList<AllotmentDto> Allotments);
public sealed record GetAllotmentQueryResult(AllotmentDto Allotment);

public sealed record GetPropertyAllotmentsQuery(Guid PropertyId, bool IncludeInactive = false) : IQuery<Result<GetAllotmentsQueryResult>>;

public sealed record GetAllotmentQuery(Guid Id) : IQuery<Result<GetAllotmentQueryResult>>;
