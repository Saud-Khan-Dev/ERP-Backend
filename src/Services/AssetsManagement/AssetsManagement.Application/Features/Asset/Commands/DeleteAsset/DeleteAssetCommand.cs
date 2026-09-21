using FluentValidation;

public sealed record DeleteAssetCommandResult(bool IsSuccess);

/// Soft delete: the row stays for depreciation / disposal history and is hidden by the global query filter.
public sealed record DeleteAssetCommand(Guid Id, string? DeletedBy = null) : ICommand<Result<DeleteAssetCommandResult>>;

public class DeleteAssetCommandValidator : AbstractValidator<DeleteAssetCommand>
{
  public DeleteAssetCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
