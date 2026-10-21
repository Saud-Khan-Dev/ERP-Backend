using FluentValidation;

/// Warnings: other agreements running on the property at the same time (activation).
public sealed record OutsourcingActionResult(Guid Id, MasterRef? Status, IReadOnlyList<string>? Warnings = null);
public sealed record RenewOutsourcingCommandResult(Guid Id, string ContractNo, Guid RenewedFromContractId, IReadOnlyList<string> Warnings);

/// A draft takes any change; once in force only the reference no., the remarks (and a money direction never recorded).
public sealed record UpdateOutsourcingCommand(Guid Id, OutsourcingTermsInput Terms) : ICommand<Result<OutsourcingActionResult>>;

/// Only EXPIRED (the other statuses come from the acts): kept for older clients, the same as ExpireOutsourcingCommand.
public sealed record ChangeContractStatusCommand(Guid Id, Guid ContractStatusId) : ICommand<Result<OutsourcingActionResult>>;

public sealed record ActivateOutsourcingCommand(Guid Id) : ICommand<Result<OutsourcingActionResult>>;

public sealed record ExpireOutsourcingCommand(Guid Id) : ICommand<Result<OutsourcingActionResult>>;

/// Ends the contract early by an order (TERMINATED): an approver's act, dated not in the future and not after the agreed end.
public sealed record TerminateOutsourcingCommand(Guid Id, DateOnly TerminationDate, string Reason) : ICommand<Result<OutsourcingActionResult>>;

/// A draft that will not go ahead: WITHDRAWN with the reason (it never came into force).
public sealed record WithdrawOutsourcingDraftCommand(Guid Id, string Reason) : ICommand<Result<OutsourcingActionResult>>;

public class WithdrawOutsourcingDraftCommandValidator : AbstractValidator<WithdrawOutsourcingDraftCommand>
{
  public WithdrawOutsourcingDraftCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Give the reason the draft is withdrawn.").MaximumLength(4000);
}

/// New terms (or the same contractor again): a new contract row pointing back at this one, which becomes RENEWED and ends
/// the day before the renewal starts. OutsourcedPartyOwnerId defaults to the current contractor.
public sealed record RenewOutsourcingCommand(Guid Id, OutsourcingTermsInput Terms, Guid? OutsourcedPartyOwnerId = null) : ICommand<Result<RenewOutsourcingCommandResult>>;

public class UpdateOutsourcingCommandValidator : AbstractValidator<UpdateOutsourcingCommand>
{
  public UpdateOutsourcingCommandValidator() => RuleFor(x => x.Terms).NotNull().SetValidator(new OutsourcingTermsInputValidator());
}

public class TerminateOutsourcingCommandValidator : AbstractValidator<TerminateOutsourcingCommand>
{
  public TerminateOutsourcingCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
}

public class RenewOutsourcingCommandValidator : AbstractValidator<RenewOutsourcingCommand>
{
  public RenewOutsourcingCommandValidator() => RuleFor(x => x.Terms).NotNull().SetValidator(new OutsourcingTermsInputValidator());
}
