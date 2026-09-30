using FluentValidation;

public sealed record LeaseLifecycleResult(Guid Id, MasterRef? Status);
public sealed record RenewLeaseCommandResult(Guid Id, string LeaseNo, Guid RenewedFromLeaseId);

public sealed record ActivateLeaseCommand(Guid Id) : ICommand<Result<LeaseLifecycleResult>>;

public sealed record ExpireLeaseCommand(Guid Id) : ICommand<Result<LeaseLifecycleResult>>;

public sealed record TerminateLeaseCommand(Guid Id, DateOnly TerminationDate, string Reason) : ICommand<Result<LeaseLifecycleResult>>;

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
