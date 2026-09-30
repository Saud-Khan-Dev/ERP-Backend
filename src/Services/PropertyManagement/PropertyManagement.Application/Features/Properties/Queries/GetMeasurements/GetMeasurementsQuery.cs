public sealed record GetMeasurementsQueryResult(IReadOnlyList<MeasurementDto> Measurements);

/// Every survey, newest first; the one with isCurrent = true is in force.
public sealed record GetMeasurementsQuery(Guid PropertyId) : IQuery<Result<GetMeasurementsQueryResult>>;
