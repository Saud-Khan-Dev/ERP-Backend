using FluentValidation;

public sealed record UpdateAssetClassCommandResult(bool IsSuccess);

public sealed record UpdateAssetClassCommand(Guid Id, AssetClassInput AssetClass) : ICommand<Result<UpdateAssetClassCommandResult>>;

public class UpdateAssetClassCommandValidator : AbstractValidator<UpdateAssetClassCommand>
{
  public UpdateAssetClassCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.AssetClass).NotNull().SetValidator(new AssetClassInputValidator());
  }
}
