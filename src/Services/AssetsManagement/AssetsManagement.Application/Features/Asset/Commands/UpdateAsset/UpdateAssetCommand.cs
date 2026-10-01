using System.Text.Json;
using FluentValidation;

/// Core fields. Class / type / category can change together (re-classification) — the attribute bag is
/// re-validated against the new schema. ExtraAttributes is a patch: present keys are set, JSON null removes.
public sealed record UpdateAssetInput(
  string AssetCode,
  string Name,
  string? Description,
  OwnershipType Ownership,
  Guid AssetClassId,
  Guid AssetTypeId,
  Guid CategoryId,
  string? Barcode = null,
  bool IsActive = true,
  Dictionary<string, JsonElement>? ExtraAttributes = null);

public sealed record UpdateAssetCommandResult(bool IsSuccess);

public sealed record UpdateAssetCommand(Guid Id, UpdateAssetInput Asset, Guid? PerformedBy = null) : ICommand<Result<UpdateAssetCommandResult>>;

public class UpdateAssetInputValidator : AbstractValidator<UpdateAssetInput>
{
  public UpdateAssetInputValidator()
  {
    RuleFor(x => x.AssetCode).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    RuleFor(x => x.Ownership).IsInEnum();
    RuleFor(x => x.AssetClassId).NotEmpty();
    RuleFor(x => x.AssetTypeId).NotEmpty();
    RuleFor(x => x.CategoryId).NotEmpty();
    RuleFor(x => x.Barcode).MaximumLength(100);
  }
}

public class UpdateAssetCommandValidator : AbstractValidator<UpdateAssetCommand>
{
  public UpdateAssetCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Asset).NotNull().SetValidator(new UpdateAssetInputValidator());
  }
}
