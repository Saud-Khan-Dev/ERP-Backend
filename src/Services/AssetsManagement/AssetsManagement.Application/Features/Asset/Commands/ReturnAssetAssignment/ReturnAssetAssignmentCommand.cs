using FluentValidation;

public sealed record ReturnAssetAssignmentCommandResult(bool IsSuccess);

/// Closes a temporary assignment (loan) and moves the asset back to the "from" side of that assignment.
public sealed record ReturnAssetAssignmentCommand(Guid AssetId, Guid AssignmentId, DateOnly? ActualReturnDate = null)
  : ICommand<Result<ReturnAssetAssignmentCommandResult>>;

public class ReturnAssetAssignmentCommandValidator : AbstractValidator<ReturnAssetAssignmentCommand>
{
  public ReturnAssetAssignmentCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.AssignmentId).NotEmpty();
  }
}
