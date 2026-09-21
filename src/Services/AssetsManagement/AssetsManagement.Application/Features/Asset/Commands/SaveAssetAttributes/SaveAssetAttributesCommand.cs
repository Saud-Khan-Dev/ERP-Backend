using System.Text.Json;
using FluentValidation;

public sealed record SaveAssetAttributesCommandResult(IReadOnlyDictionary<string, JsonElement> ExtraAttributes, DateTime? AttributesValidatedAt);

/// Patches the dynamic attribute bag: keys present are set, JSON null removes a key, absent keys are kept.
public sealed record SaveAssetAttributesCommand(
  Guid AssetId,
  Dictionary<string, JsonElement> Attributes,
  Guid? ChangedBy = null,
  string? ChangeReason = null) : ICommand<Result<SaveAssetAttributesCommandResult>>;

public class SaveAssetAttributesCommandValidator : AbstractValidator<SaveAssetAttributesCommand>
{
  public SaveAssetAttributesCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Attributes).NotNull().WithMessage("Attributes must be a JSON object keyed by attribute code.");
  }
}
