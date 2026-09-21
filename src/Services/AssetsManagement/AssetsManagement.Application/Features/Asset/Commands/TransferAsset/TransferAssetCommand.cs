using FluentValidation;

public sealed record TransferAssetCommandResult(Guid AssignmentId);

/// Assigns / transfers / loans an asset: writes an asset_assignment row and updates the asset's current
/// department / custodian / location. Omitted targets keep the asset's current value.
public sealed record TransferAssetCommand(
  Guid AssetId,
  Guid? ToDepartmentId,
  Guid? ToCustodianId,
  Guid? ToLocationId,
  DateTime? AssignmentDate = null,
  DateOnly? ExpectedReturnDate = null,
  string? Reason = null,
  Guid? ApprovedBy = null,
  DateTime? ApprovedAt = null,
  Guid? EventTypeId = null,
  Guid? PerformedBy = null) : ICommand<Result<TransferAssetCommandResult>>;

public class TransferAssetCommandValidator : AbstractValidator<TransferAssetCommand>
{
  public TransferAssetCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x)
      .Must(x => x.ToDepartmentId.HasValue || x.ToCustodianId.HasValue || x.ToLocationId.HasValue)
      .WithMessage("Provide at least one of toDepartmentId, toCustodianId or toLocationId.");
  }
}
