using FluentValidation;

public sealed record ReverseDepreciationEntryCommandResult(bool IsSuccess);

/// Undoes a posted month; the month and its reversal stay on record.
public sealed record ReverseDepreciationEntryCommand(Guid ScheduleId, Guid EntryId) : ICommand<Result<ReverseDepreciationEntryCommandResult>>;

public class ReverseDepreciationEntryCommandValidator : AbstractValidator<ReverseDepreciationEntryCommand>
{
  public ReverseDepreciationEntryCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.EntryId).NotEmpty();
  }
}
