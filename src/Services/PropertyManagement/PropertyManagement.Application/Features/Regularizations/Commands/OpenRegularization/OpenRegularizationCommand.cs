using FluentValidation;

public sealed record RegularizationInput(
  decimal AdditionalArea,
  Guid MeasurementUnitId,
  RegularizationStatus Status = RegularizationStatus.Applied,
  DateOnly? ApplicationDate = null,
  DateOnly? RegularizationDate = null,
  string? OrderReferenceNo = null,
  string? ApprovedBy = null,
  DateOnly? EffectiveFrom = null,
  string? Remarks = null);

public sealed record OpenRegularizationCommandResult(Guid Id);

public sealed record OpenRegularizationCommand(Guid PropertyId, RegularizationInput Regularization) : ICommand<Result<OpenRegularizationCommandResult>>;

public class OpenRegularizationCommandValidator : AbstractValidator<OpenRegularizationCommand>
{
  public OpenRegularizationCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Regularization).NotNull();
    RuleFor(x => x.Regularization.AdditionalArea).GreaterThan(0);
    RuleFor(x => x.Regularization.MeasurementUnitId).NotEmpty();
    RuleFor(x => x.Regularization.Status).IsInEnum();
    RuleFor(x => x.Regularization.OrderReferenceNo).MaximumLength(100);
    RuleFor(x => x.Regularization.ApprovedBy).MaximumLength(150);
  }
}
