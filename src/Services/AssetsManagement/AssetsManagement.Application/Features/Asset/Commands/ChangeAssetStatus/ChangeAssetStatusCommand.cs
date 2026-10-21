using FluentValidation;

public sealed record ChangeAssetStatusCommandResult(Guid LifecycleEventId);

/// Moves the asset to another (not final) status and writes "Status changed" on its timeline. A final status is
/// reached only by disposing of the asset.
public sealed record ChangeAssetStatusCommand(
  Guid AssetId,
  Guid ToStatusId,
  string? Notes = null,
  DateTime? EventDate = null) : ICommand<Result<ChangeAssetStatusCommandResult>>;

public class ChangeAssetStatusCommandValidator : AbstractValidator<ChangeAssetStatusCommand>
{
  public ChangeAssetStatusCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.ToStatusId).NotEmpty().WithMessage("Choose the new status.");
    RuleFor(x => x.Notes).MaximumLength(1000);
  }
}
