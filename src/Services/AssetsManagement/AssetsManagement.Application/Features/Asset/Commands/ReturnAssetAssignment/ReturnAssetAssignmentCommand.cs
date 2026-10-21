using FluentValidation;

public sealed record ReturnAssetAssignmentCommandResult(bool IsSuccess);

/// Closes a loan and moves the asset back to where (and to whom) it was issued from. Always possible, whatever status
/// the asset is in now. Writes "Returned" on the timeline.
public sealed record ReturnAssetAssignmentCommand(Guid AssetId, Guid AssignmentId, DateOnly? ActualReturnDate = null, string? Notes = null)
  : ICommand<Result<ReturnAssetAssignmentCommandResult>>;

public class ReturnAssetAssignmentCommandValidator : AbstractValidator<ReturnAssetAssignmentCommand>
{
  public ReturnAssetAssignmentCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.AssignmentId).NotEmpty();
    RuleFor(x => x.Notes).MaximumLength(1000);
  }
}
