using FluentValidation;

public sealed record DepreciationRunSkip(Guid AssetId, string AssetCode, string Reason);

public sealed record RunDepreciationCommandResult(int Schedules, int EntriesGenerated, int EntriesPosted, decimal AmountGenerated, IReadOnlyList<DepreciationRunSkip> Skipped);

/// Month-end depreciation for EVERY running schedule (of assets not deleted and not closed): the missing months up to
/// `Until` (default today) are written and, when Post is set, every draft month that has ENDED by then is posted
/// (needs ASSET_FINANCE.POST; a date after today is refused). A schedule that cannot run (units of production, no
/// purchase) is listed as skipped and never stops the others. The run over every asset needs ASSET_FINANCE.RUN;
/// AssetId limits it to one asset, which ASSET_FINANCE.CREATE (writing draft months) also allows. The months are not
/// written to the audit trail one by one: the run writes one "Ran" line with what it did.
public sealed record RunDepreciationCommand(DateOnly? Until, bool Post, Guid? AssetId = null) : ICommand<Result<RunDepreciationCommandResult>>;

public class RunDepreciationCommandValidator : AbstractValidator<RunDepreciationCommand>
{
  public RunDepreciationCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEqual(Guid.Empty).When(x => x.AssetId.HasValue);
  }
}
