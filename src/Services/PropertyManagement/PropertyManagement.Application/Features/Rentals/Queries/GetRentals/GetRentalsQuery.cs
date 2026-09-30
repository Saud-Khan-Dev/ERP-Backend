public sealed record GetRentalsQueryResult(IReadOnlyList<RentalDto> Rentals);
public sealed record GetRentalQueryResult(RentalDto Rental);

public sealed record GetPropertyRentalsQuery(Guid PropertyId) : IQuery<Result<GetRentalsQueryResult>>;

public sealed record GetRentalQuery(Guid Id) : IQuery<Result<GetRentalQueryResult>>;
