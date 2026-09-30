public sealed record GetCodeSequencesQueryResult(IReadOnlyList<CodeSequenceDto> Sequences);

public sealed record GetCodeSequencesQuery : IQuery<Result<GetCodeSequencesQueryResult>>;
