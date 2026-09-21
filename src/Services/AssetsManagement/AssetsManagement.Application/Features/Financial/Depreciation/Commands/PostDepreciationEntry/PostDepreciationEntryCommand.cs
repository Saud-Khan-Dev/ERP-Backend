using FluentValidation;

public sealed record PostDepreciationEntryCommandResult(bool IsSuccess);

public sealed record PostDepreciationEntryCommand(Guid ScheduleId, Guid EntryId, Guid? PostedBy = null) : ICommand<Result<PostDepreciationEntryCommandResult>>;

public class PostDepreciationEntryCommandValidator : AbstractValidator<PostDepreciationEntryCommand>
{
  public PostDepreciationEntryCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.EntryId).NotEmpty();
  }
}
