using FluentValidation;

public sealed record LocationInput(
  Guid? ParentLocationId,
  string Code,
  string Name,
  string? LocationType,
  string? Address,
  decimal? Latitude,
  decimal? Longitude,
  bool IsActive = true);

public sealed record CreateLocationCommandResult(Guid Id);

public sealed record CreateLocationCommand(LocationInput Location) : ICommand<Result<CreateLocationCommandResult>>;

public class LocationInputValidator : AbstractValidator<LocationInput>
{
  public LocationInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    RuleFor(x => x.LocationType).MaximumLength(50);
    RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
    RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
  }
}

public class CreateLocationCommandValidator : AbstractValidator<CreateLocationCommand>
{
  public CreateLocationCommandValidator()
  {
    RuleFor(x => x.Location).NotNull().SetValidator(new LocationInputValidator());
  }
}
