public sealed record GetMeasurementsQueryResult(IReadOnlyList<MeasurementDto> Measurements);

/// Every survey: the one in force (isCurrent = true) first, then the history by measured date, newest first.
public sealed record GetMeasurementsQuery(Guid PropertyId) : IQuery<Result<GetMeasurementsQueryResult>>;
