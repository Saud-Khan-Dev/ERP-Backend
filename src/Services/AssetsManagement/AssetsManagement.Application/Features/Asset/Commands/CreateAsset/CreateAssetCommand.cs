using System.Text.Json;
using FluentValidation;

public sealed record CreateAssetInput(
  string AssetCode,
  string Name,
  string? Description,
  OwnershipType Ownership,
  Guid AssetClassId,
  Guid AssetTypeId,
  Guid CategoryId,
  Guid StatusId,
  Guid? ParentAssetId = null,
  Guid? DepartmentId = null,
  Guid? CustodianId = null,
  Guid? CurrentLocationId = null,
  string? SerialNumber = null,
  string? Barcode = null,
  string? RfidTag = null,
  Dictionary<string, JsonElement>? ExtraAttributes = null);

public sealed record CreateAssetCommandResult(Guid Id);

public sealed record CreateAssetCommand(CreateAssetInput Asset, Guid? PerformedBy = null) : ICommand<Result<CreateAssetCommandResult>>;

public class CreateAssetInputValidator : AbstractValidator<CreateAssetInput>
{
  public CreateAssetInputValidator()
  {
    RuleFor(x => x.AssetCode).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    RuleFor(x => x.Ownership).IsInEnum();
    RuleFor(x => x.AssetClassId).NotEmpty();
    RuleFor(x => x.AssetTypeId).NotEmpty();
    RuleFor(x => x.CategoryId).NotEmpty();
    RuleFor(x => x.StatusId).NotEmpty();
    RuleFor(x => x.SerialNumber).MaximumLength(150);
    RuleFor(x => x.Barcode).MaximumLength(100);
    RuleFor(x => x.RfidTag).MaximumLength(100);
  }
}

public class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
{
  public CreateAssetCommandValidator()
  {
    RuleFor(x => x.Asset).NotNull().SetValidator(new CreateAssetInputValidator());
  }
}
