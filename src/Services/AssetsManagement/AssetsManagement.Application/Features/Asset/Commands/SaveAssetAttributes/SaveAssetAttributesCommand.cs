using System.Text.Json;
using FluentValidation;

public sealed record SaveAssetAttributesCommandResult(IReadOnlyDictionary<string, JsonElement> ExtraAttributes, DateTime? AttributesValidatedAt);

/// Patches the dynamic attribute bag: keys present are set, JSON null removes a key, absent keys are kept. Values of
/// fields no longer on the asset's form are never removed this way.
public sealed record SaveAssetAttributesCommand(
  Guid AssetId,
  Dictionary<string, JsonElement> Attributes,
  string? ChangeReason = null) : ICommand<Result<SaveAssetAttributesCommandResult>>;

public class SaveAssetAttributesCommandValidator : AbstractValidator<SaveAssetAttributesCommand>
{
  public SaveAssetAttributesCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Attributes).NotNull().WithMessage("Send the details to save.");
    RuleFor(x => x.ChangeReason).MaximumLength(500);
  }
}
