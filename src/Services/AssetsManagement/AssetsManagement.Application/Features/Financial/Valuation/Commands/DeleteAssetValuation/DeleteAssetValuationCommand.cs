using FluentValidation;

public sealed record DeleteAssetValuationCommandResult(bool IsSuccess);

public sealed record DeleteAssetValuationCommand(Guid AssetId, Guid ValuationId) : ICommand<Result<DeleteAssetValuationCommandResult>>;

public class DeleteAssetValuationCommandValidator : AbstractValidator<DeleteAssetValuationCommand>
{
  public DeleteAssetValuationCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.ValuationId).NotEmpty();
  }
}
