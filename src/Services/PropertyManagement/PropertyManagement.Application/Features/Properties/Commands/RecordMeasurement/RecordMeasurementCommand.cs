using FluentValidation;

public sealed record RecordMeasurementCommandResult(Guid Id);

/// A new survey. The previous current measurement is superseded, never edited.
public sealed record RecordMeasurementCommand(Guid PropertyId, MeasurementInput Measurement) : ICommand<Result<RecordMeasurementCommandResult>>;

public class RecordMeasurementCommandValidator : AbstractValidator<RecordMeasurementCommand>
{
  public RecordMeasurementCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Measurement).NotNull().SetValidator(new MeasurementInputValidator());
  }
}
