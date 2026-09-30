using FluentValidation;

public sealed record RentalLifecycleResult(Guid Id, MasterRef? Status);
public sealed record RenewRentalCommandResult(Guid Id, string RentalNo, Guid RenewedFromRentalId);

public sealed record TerminateRentalCommand(Guid Id, DateOnly TerminationDate, string Reason) : ICommand<Result<RentalLifecycleResult>>;

/// A renewal is a new rental row pointing back at this one, which ends. TenantOwnerId defaults to the current tenant.
public sealed record RenewRentalCommand(Guid Id, RentalTermsInput Terms, Guid? TenantOwnerId = null) : ICommand<Result<RenewRentalCommandResult>>;

public class TerminateRentalCommandValidator : AbstractValidator<TerminateRentalCommand>
{
  public TerminateRentalCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
}

public class RenewRentalCommandValidator : AbstractValidator<RenewRentalCommand>
{
  public RenewRentalCommandValidator() => RuleFor(x => x.Terms).NotNull().SetValidator(new RentalTermsInputValidator());
}
