using FluentValidation;

public sealed record DeleteAssetCommandResult(bool IsSuccess);

/// Takes an asset registered by mistake out of the register (soft delete: the row stays, hidden everywhere). Not for
/// an asset that left GDA - that is a disposal, and a disposed asset stays in the register as its record. Refused
/// while depreciation is posted on it (reverse the months first) or it is out on loan. A reason is required.
public sealed record DeleteAssetCommand(Guid Id, string Reason) : ICommand<Result<DeleteAssetCommandResult>>;

public class DeleteAssetCommandValidator : AbstractValidator<DeleteAssetCommand>
{
  public DeleteAssetCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the asset is deleted.").MinimumLength(5).WithMessage("Say why the asset is deleted - a few words at least.").MaximumLength(500);
  }
}
