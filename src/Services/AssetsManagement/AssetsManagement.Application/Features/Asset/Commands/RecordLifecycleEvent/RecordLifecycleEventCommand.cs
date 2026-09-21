using System.Text.Json;
using FluentValidation;

public sealed record RecordLifecycleEventCommandResult(Guid Id);

/// Free-form lifecycle entry (maintenance, inspection, revaluation note ...). Status changes go through ChangeAssetStatus.
public sealed record RecordLifecycleEventCommand(
  Guid AssetId,
  Guid EventTypeId,
  DateTime? EventDate = null,
  Guid? PerformedBy = null,
  string? Notes = null,
  JsonElement? Details = null) : ICommand<Result<RecordLifecycleEventCommandResult>>;

public class RecordLifecycleEventCommandValidator : AbstractValidator<RecordLifecycleEventCommand>
{
  public RecordLifecycleEventCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.EventTypeId).NotEmpty();
  }
}
