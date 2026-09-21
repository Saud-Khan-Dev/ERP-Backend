using FluentValidation;

public sealed record ReverseDepreciationEntryCommandResult(bool IsSuccess);

public sealed record ReverseDepreciationEntryCommand(Guid ScheduleId, Guid EntryId, Guid? ReversedBy = null) : ICommand<Result<ReverseDepreciationEntryCommandResult>>;

public class ReverseDepreciationEntryCommandValidator : AbstractValidator<ReverseDepreciationEntryCommand>
{
  public ReverseDepreciationEntryCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.EntryId).NotEmpty();
  }
}
