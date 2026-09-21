using FluentValidation;

public sealed record UpdateAssetTypeCommandResult(bool IsSuccess);

public sealed record UpdateAssetTypeCommand(Guid Id, AssetTypeInput AssetType) : ICommand<Result<UpdateAssetTypeCommandResult>>;

public class UpdateAssetTypeCommandValidator : AbstractValidator<UpdateAssetTypeCommand>
{
  public UpdateAssetTypeCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.AssetType).NotNull().SetValidator(new AssetTypeInputValidator());
  }
}
