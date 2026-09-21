using FluentValidation;

public sealed record GenerateDepreciationEntriesCommandResult(IReadOnlyList<AssetDepreciationEntryDto> Entries);

/// Generates the missing monthly entries up to `Until` (defaults to today). STRAIGHT_LINE / DECLINING_BALANCE only.
public sealed record GenerateDepreciationEntriesCommand(Guid ScheduleId, DateOnly? Until = null) : ICommand<Result<GenerateDepreciationEntriesCommandResult>>;

public class GenerateDepreciationEntriesCommandValidator : AbstractValidator<GenerateDepreciationEntriesCommand>
{
  public GenerateDepreciationEntriesCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
  }
}
