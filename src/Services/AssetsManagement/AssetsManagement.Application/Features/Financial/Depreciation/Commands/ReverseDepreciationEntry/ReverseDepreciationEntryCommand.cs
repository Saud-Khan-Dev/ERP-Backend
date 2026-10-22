using FluentValidation;

public sealed record ReverseDepreciationEntryCommandResult(bool IsSuccess);

/// Undoes a posted month (ASSET_FINANCE.REVERSE), saying why; the month and its reversal stay on record. Refused to the
/// person who posted it while the rule "Depreciation is reversed by someone else" is on.
public sealed record ReverseDepreciationEntryCommand(Guid ScheduleId, Guid EntryId, string Reason) : ICommand<Result<ReverseDepreciationEntryCommandResult>>;

public class ReverseDepreciationEntryCommandValidator : AbstractValidator<ReverseDepreciationEntryCommand>
{
  public ReverseDepreciationEntryCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.EntryId).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the month is reversed.")
      .MinimumLength(5).WithMessage("Say why the month is reversed - a few words at least.").MaximumLength(500);
  }
}
