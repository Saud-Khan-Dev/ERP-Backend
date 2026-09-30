using FluentValidation;

public sealed record ChangePropertyStatusCommandResult(Guid StatusHistoryId);

public sealed record ChangePropertyStatusCommand(
  Guid PropertyId,
  Guid PropertyStatusId,
  DateOnly? EffectiveFrom = null,
  string? Reason = null,
  string? ReferenceNo = null) : ICommand<Result<ChangePropertyStatusCommandResult>>;

public class ChangePropertyStatusCommandValidator : AbstractValidator<ChangePropertyStatusCommand>
{
  public ChangePropertyStatusCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.PropertyStatusId).NotEmpty();
    RuleFor(x => x.Reason).MaximumLength(300);
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
  }
}
