using FluentValidation;

/// IsCurrent: false when the survey is older than the measurement in force (kept as history; Warnings says so).
public sealed record RecordMeasurementCommandResult(Guid Id, bool IsCurrent, IReadOnlyList<string> Warnings);

/// A new survey. When it is the latest dated one, the previous current measurement is superseded (never
/// edited); an older survey is stored as history.
public sealed record RecordMeasurementCommand(Guid PropertyId, MeasurementInput Measurement) : ICommand<Result<RecordMeasurementCommandResult>>;

public class RecordMeasurementCommandValidator : AbstractValidator<RecordMeasurementCommand>
{
  public RecordMeasurementCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Measurement).NotNull().SetValidator(new MeasurementInputValidator());
  }
}
