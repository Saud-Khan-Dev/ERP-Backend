using System.Text.Json;
using FluentValidation;

public sealed record CreateAssetInput(
  string? AssetCode,
  string Name,
  string? Description,
  OwnershipType Ownership,
  Guid AssetClassId,
  Guid AssetTypeId,
  Guid CategoryId,
  Guid StatusId,
  Guid? DepartmentId = null,
  Guid? CustodianId = null,
  Guid? CurrentLocationId = null,
  string? Barcode = null,
  Dictionary<string, JsonElement>? ExtraAttributes = null);

/// How the asset is depreciated from day one. The base is the acquisition cost, so it needs an acquisition.
public sealed record DepreciationPlanInput(
  Guid MethodId,
  int UsefulLifeMonths,
  decimal SalvageValue,
  DateOnly? StartDate = null,
  decimal? DecliningRate = null);

public sealed record CreateAssetCommandResult(Guid Id, string AssetCode);

/// Registers an asset in ONE transaction: the record with its details, and - when given - its acquisition and
/// depreciation plan. Either all of it is saved or none of it. An empty AssetCode is issued by the service from a
/// database sequence (AST-000001, AST-000002 …): two clerks saving at the same moment get different tags, and a tag
/// is never handed out twice (deleted assets keep theirs). The custodian and the department are checked in HRM.
public sealed record CreateAssetCommand(
  CreateAssetInput Asset,
  AssetAcquisitionInput? Acquisition = null,
  DepreciationPlanInput? Depreciation = null) : ICommand<Result<CreateAssetCommandResult>>;

public class CreateAssetInputValidator : AbstractValidator<CreateAssetInput>
{
  public CreateAssetInputValidator()
  {
    RuleFor(x => x.AssetCode).MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().WithMessage("Give the asset a name.").MaximumLength(200);
    RuleFor(x => x.Description).MaximumLength(4000);
    RuleFor(x => x.Ownership).IsInEnum();
    RuleFor(x => x.AssetClassId).NotEmpty().WithMessage("Choose the asset class.");
    RuleFor(x => x.AssetTypeId).NotEmpty().WithMessage("Choose the asset type.");
    RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Choose the category.");
    RuleFor(x => x.StatusId).NotEmpty().WithMessage("Choose the status it starts in.");
    RuleFor(x => x.Barcode).MaximumLength(100);
  }
}

public class DepreciationPlanInputValidator : AbstractValidator<DepreciationPlanInput>
{
  public DepreciationPlanInputValidator()
  {
    RuleFor(x => x.MethodId).NotEmpty().WithMessage("Choose how it depreciates.");
    RuleFor(x => x.UsefulLifeMonths).InclusiveBetween(1, 1200).WithMessage("The useful life must be between 1 month and 100 years.");
    RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0).WithMessage("The value at the end cannot be negative.");
    RuleFor(x => x.DecliningRate).ExclusiveBetween(0, 1).When(x => x.DecliningRate.HasValue)
      .WithMessage("The yearly rate must be between 0 % and 100 %.");
  }
}

public class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
{
  public CreateAssetCommandValidator()
  {
    RuleFor(x => x.Asset).NotNull().SetValidator(new CreateAssetInputValidator());
    RuleFor(x => x.Acquisition!).SetValidator(new AssetAcquisitionInputValidator()).When(x => x.Acquisition is not null);
    RuleFor(x => x.Depreciation!).SetValidator(new DepreciationPlanInputValidator()).When(x => x.Depreciation is not null);
    RuleFor(x => x.Acquisition).NotNull()
      .When(x => x.Depreciation is not null)
      .WithMessage("Depreciation is worked out from the purchase cost: enter the purchase details too.");
  }
}
