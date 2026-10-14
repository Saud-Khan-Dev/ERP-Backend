using FluentValidation;

/// EndedOwnershipIds: ownership rows the act ended (a cancelled or surrendered allotment that had been
/// confirmed as ownership); empty otherwise.
public sealed record AllotmentLifecycleResult(Guid Id, MasterRef? Status, bool IsActive, IReadOnlyList<Guid> EndedOwnershipIds);

/// Act s.6(4)(c): cancel an allotment in force. A confirmed ownership through it ends on the cancellation date.
/// An appeal against the cancellation is filed as a property appeal.
public sealed record CancelAllotmentCommand(Guid Id, DateOnly CancellationDate, string Reason, string? OrderRef = null) : ICommand<Result<AllotmentLifecycleResult>>;

/// Reverses a cancellation (e.g. after an appeal is allowed). Refused while another allotment is in force.
public sealed record RestoreAllotmentCommand(Guid Id, DateOnly RestorationDate, string? OrderRef = null) : ICommand<Result<AllotmentLifecycleResult>>;

/// An allotment in force becomes SURRENDERED (Date = the surrender date, required) or EXPIRED (after its expiry
/// date; one without an expiry date needs Date, recorded as its expiry date).
public sealed record ChangeAllotmentStatusCommand(Guid Id, Guid AllotmentStatusId, DateOnly? Date = null) : ICommand<Result<AllotmentLifecycleResult>>;

/// Withdraws an allotment entered in error (nothing is hard-deleted).
public sealed record DeactivateAllotmentCommand(Guid Id) : ICommand<Result<AllotmentLifecycleResult>>;

public class CancelAllotmentCommandValidator : AbstractValidator<CancelAllotmentCommand>
{
  public CancelAllotmentCommandValidator()
  {
    RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
    RuleFor(x => x.OrderRef).MaximumLength(100);
  }
}

public class RestoreAllotmentCommandValidator : AbstractValidator<RestoreAllotmentCommand>
{
  public RestoreAllotmentCommandValidator() => RuleFor(x => x.OrderRef).MaximumLength(100);
}

public class ChangeAllotmentStatusCommandValidator : AbstractValidator<ChangeAllotmentStatusCommand>
{
  public ChangeAllotmentStatusCommandValidator() => RuleFor(x => x.AllotmentStatusId).NotEmpty();
}
