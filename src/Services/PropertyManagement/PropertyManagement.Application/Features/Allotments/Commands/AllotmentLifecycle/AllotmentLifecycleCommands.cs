using FluentValidation;

public sealed record AllotmentLifecycleResult(Guid Id, MasterRef? Status, bool IsActive);

/// Act s.6(4)(c): cancel an allotment. An appeal against the cancellation is filed as a property appeal.
public sealed record CancelAllotmentCommand(Guid Id, DateOnly CancellationDate, string Reason, string? OrderRef = null) : ICommand<Result<AllotmentLifecycleResult>>;

/// Reverses a cancellation (e.g. after an appeal is allowed).
public sealed record RestoreAllotmentCommand(Guid Id, DateOnly RestorationDate, string? OrderRef = null) : ICommand<Result<AllotmentLifecycleResult>>;

/// Any other admin-defined status (Surrendered, Expired ...).
public sealed record ChangeAllotmentStatusCommand(Guid Id, Guid AllotmentStatusId) : ICommand<Result<AllotmentLifecycleResult>>;

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
