using FluentValidation;

public sealed record OutsourcingActionResult(Guid Id, MasterRef? Status);

public sealed record UpdateOutsourcingCommand(Guid Id, OutsourcingTermsInput Terms) : ICommand<Result<OutsourcingActionResult>>;

/// Expired or any other admin-defined status.
public sealed record ChangeContractStatusCommand(Guid Id, Guid ContractStatusId) : ICommand<Result<OutsourcingActionResult>>;

public sealed record TerminateOutsourcingCommand(Guid Id, DateOnly TerminationDate, string Reason) : ICommand<Result<OutsourcingActionResult>>;

public class UpdateOutsourcingCommandValidator : AbstractValidator<UpdateOutsourcingCommand>
{
  public UpdateOutsourcingCommandValidator() => RuleFor(x => x.Terms).NotNull().SetValidator(new OutsourcingTermsInputValidator());
}

public class TerminateOutsourcingCommandValidator : AbstractValidator<TerminateOutsourcingCommand>
{
  public TerminateOutsourcingCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
}
