using System.Text.Json;
using FluentValidation;

public sealed record RecordLifecycleEventCommandResult(Guid Id);

/// An event a clerk records by hand (an inspection, a repair, an insurance renewal) with optional details. The events
/// the acts write themselves (a move, a status change, a disposal) cannot be recorded this way.
public sealed record RecordLifecycleEventCommand(
  Guid AssetId,
  Guid EventTypeId,
  DateTime? EventDate = null,
  string? Notes = null,
  JsonElement? Details = null) : ICommand<Result<RecordLifecycleEventCommandResult>>;

public class RecordLifecycleEventCommandValidator : AbstractValidator<RecordLifecycleEventCommand>
{
  public RecordLifecycleEventCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.EventTypeId).NotEmpty().WithMessage("Choose what happened.");
    RuleFor(x => x.Notes).MaximumLength(2000);
    RuleFor(x => x.Details).Must(d => d is null || d.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Null)
      .WithMessage("The extra details must be a list of names and values.");
  }
}
