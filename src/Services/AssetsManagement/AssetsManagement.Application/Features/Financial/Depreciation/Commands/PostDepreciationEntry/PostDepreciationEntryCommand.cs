using FluentValidation;

public sealed record PostDepreciationEntryCommandResult(bool IsSuccess);

/// Makes a month final (posted). Only a month that is over can be posted.
public sealed record PostDepreciationEntryCommand(Guid ScheduleId, Guid EntryId) : ICommand<Result<PostDepreciationEntryCommandResult>>;

public class PostDepreciationEntryCommandValidator : AbstractValidator<PostDepreciationEntryCommand>
{
  public PostDepreciationEntryCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.EntryId).NotEmpty();
  }
}
