using FluentValidation;

/// Warnings: other agreements running on the property at the same time (activation, renewal).
public sealed record RentalLifecycleResult(Guid Id, MasterRef? Status, IReadOnlyList<string>? Warnings = null);
public sealed record RenewRentalCommandResult(Guid Id, string RentalNo, Guid RenewedFromRentalId, IReadOnlyList<string> Warnings);

/// A draft rental comes into force.
public sealed record ActivateRentalCommand(Guid Id) : ICommand<Result<RentalLifecycleResult>>;

/// An active rental is marked EXPIRED once its end date has passed.
public sealed record ExpireRentalCommand(Guid Id) : ICommand<Result<RentalLifecycleResult>>;

/// Ends the rental early by an order (TERMINATED): an approver's act, dated not in the future and not after the agreed end.
public sealed record TerminateRentalCommand(Guid Id, DateOnly TerminationDate, string Reason) : ICommand<Result<RentalLifecycleResult>>;

/// A draft that will not go ahead: WITHDRAWN with the reason (it never came into force).
public sealed record WithdrawRentalDraftCommand(Guid Id, string Reason) : ICommand<Result<RentalLifecycleResult>>;

public class WithdrawRentalDraftCommandValidator : AbstractValidator<WithdrawRentalDraftCommand>
{
  public WithdrawRentalDraftCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Give the reason the draft is withdrawn.").MaximumLength(4000);
}

/// A renewal (or revision of the terms) is a new rental row pointing back at this one, which ends the day before it
/// starts. TenantOwnerId defaults to the current tenant.
public sealed record RenewRentalCommand(Guid Id, RentalTermsInput Terms, Guid? TenantOwnerId = null) : ICommand<Result<RenewRentalCommandResult>>;

public class TerminateRentalCommandValidator : AbstractValidator<TerminateRentalCommand>
{
  public TerminateRentalCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
}

public class RenewRentalCommandValidator : AbstractValidator<RenewRentalCommand>
{
  public RenewRentalCommandValidator() => RuleFor(x => x.Terms).NotNull().SetValidator(new RentalTermsInputValidator());
}
