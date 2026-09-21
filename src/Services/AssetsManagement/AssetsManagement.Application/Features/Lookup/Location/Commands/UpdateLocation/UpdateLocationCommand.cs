using FluentValidation;

public sealed record UpdateLocationCommandResult(bool IsSuccess);

/// Also moves the location when ParentLocationId changes; descendant paths are rebuilt.
public sealed record UpdateLocationCommand(Guid Id, LocationInput Location) : ICommand<Result<UpdateLocationCommandResult>>;

public class UpdateLocationCommandValidator : AbstractValidator<UpdateLocationCommand>
{
  public UpdateLocationCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Location).NotNull().SetValidator(new LocationInputValidator());
  }
}
