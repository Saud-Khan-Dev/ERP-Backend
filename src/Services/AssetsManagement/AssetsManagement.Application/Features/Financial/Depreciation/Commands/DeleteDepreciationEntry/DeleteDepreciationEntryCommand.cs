using FluentValidation;

public sealed record DeleteDepreciationEntryCommandResult(bool IsSuccess);

/// Only unposted entries can be deleted; posted ones must be reversed.
public sealed record DeleteDepreciationEntryCommand(Guid ScheduleId, Guid EntryId) : ICommand<Result<DeleteDepreciationEntryCommandResult>>;

public class DeleteDepreciationEntryCommandValidator : AbstractValidator<DeleteDepreciationEntryCommand>
{
  public DeleteDepreciationEntryCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.EntryId).NotEmpty();
  }
}
