using FluentValidation;

/// Warnings: other agreements running on the property at the same time (activation).
public sealed record LeaseLifecycleResult(Guid Id, MasterRef? Status, IReadOnlyList<string>? Warnings = null);
public sealed record RenewLeaseCommandResult(Guid Id, string LeaseNo, Guid RenewedFromLeaseId, IReadOnlyList<string> Warnings);

public sealed record ActivateLeaseCommand(Guid Id) : ICommand<Result<LeaseLifecycleResult>>;

public sealed record ExpireLeaseCommand(Guid Id) : ICommand<Result<LeaseLifecycleResult>>;

public sealed record TerminateLeaseCommand(Guid Id, DateOnly TerminationDate, string Reason) : ICommand<Result<LeaseLifecycleResult>>;

/// A draft that will not go ahead: WITHDRAWN with the reason (it never came into force).
public sealed record WithdrawLeaseDraftCommand(Guid Id, string Reason) : ICommand<Result<LeaseLifecycleResult>>;

public class WithdrawLeaseDraftCommandValidator : AbstractValidator<WithdrawLeaseDraftCommand>
{
  public WithdrawLeaseDraftCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Give the reason the draft is withdrawn.").MaximumLength(4000);
}

/// A renewal is a new lease row (LSE-...) pointing back at this one, which becomes RENEWED.
/// LesseeOwnerId defaults to the current lessee.
public sealed record RenewLeaseCommand(Guid Id, LeaseTermsInput Terms, Guid? LesseeOwnerId = null) : ICommand<Result<RenewLeaseCommandResult>>;

public class TerminateLeaseCommandValidator : AbstractValidator<TerminateLeaseCommand>
{
  public TerminateLeaseCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
}

public class RenewLeaseCommandValidator : AbstractValidator<RenewLeaseCommand>
{
  public RenewLeaseCommandValidator() => RuleFor(x => x.Terms).NotNull().SetValidator(new LeaseTermsInputValidator());
}
