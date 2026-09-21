using FluentValidation;

public sealed record ChangeAssetStatusCommandResult(Guid LifecycleEventId);

/// Moves the asset to another status and records the transition as a lifecycle event.
public sealed record ChangeAssetStatusCommand(
  Guid AssetId,
  Guid ToStatusId,
  Guid EventTypeId,
  string? Notes = null,
  Guid? PerformedBy = null,
  DateTime? EventDate = null) : ICommand<Result<ChangeAssetStatusCommandResult>>;

public class ChangeAssetStatusCommandValidator : AbstractValidator<ChangeAssetStatusCommand>
{
  public ChangeAssetStatusCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.ToStatusId).NotEmpty();
    RuleFor(x => x.EventTypeId).NotEmpty();
  }
}
