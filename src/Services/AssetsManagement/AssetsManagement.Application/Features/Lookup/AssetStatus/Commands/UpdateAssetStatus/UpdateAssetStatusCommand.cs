using FluentValidation;

public sealed record UpdateAssetStatusCommandResult(bool IsSuccess);

public sealed record UpdateAssetStatusCommand(Guid Id, AssetStatusInput Status) : ICommand<Result<UpdateAssetStatusCommandResult>>;

public class UpdateAssetStatusCommandValidator : AbstractValidator<UpdateAssetStatusCommand>
{
  public UpdateAssetStatusCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Status).NotNull().SetValidator(new AssetStatusInputValidator());
  }
}
