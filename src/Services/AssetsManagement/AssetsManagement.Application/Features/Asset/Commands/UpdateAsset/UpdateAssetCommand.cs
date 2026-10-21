using System.Text.Json;
using FluentValidation;

/// Core fields. Class / type / category can change together (re-classification) - values of fields the new form
/// does not ask for stay on the record. ExtraAttributes is a patch: present keys are set, JSON null removes. The tag is
/// fixed once the asset is registered; location, custody and status change through their own acts.
public sealed record UpdateAssetInput(
  string Name,
  string? Description,
  OwnershipType Ownership,
  Guid AssetClassId,
  Guid AssetTypeId,
  Guid CategoryId,
  string? Barcode = null,
  Dictionary<string, JsonElement>? ExtraAttributes = null);

public sealed record UpdateAssetCommandResult(bool IsSuccess);

public sealed record UpdateAssetCommand(Guid Id, UpdateAssetInput Asset) : ICommand<Result<UpdateAssetCommandResult>>;

public class UpdateAssetInputValidator : AbstractValidator<UpdateAssetInput>
{
  public UpdateAssetInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().WithMessage("Give the asset a name.").MaximumLength(200);
    RuleFor(x => x.Description).MaximumLength(4000);
    RuleFor(x => x.Ownership).IsInEnum();
    RuleFor(x => x.AssetClassId).NotEmpty().WithMessage("Choose the asset class.");
    RuleFor(x => x.AssetTypeId).NotEmpty().WithMessage("Choose the asset type.");
    RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Choose the category.");
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
