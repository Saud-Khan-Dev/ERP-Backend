using FluentValidation;

public sealed record TransferAssetCommandResult(Guid AssignmentId);

/// Transfers or lends an asset: writes an asset_assignment row and updates the asset's current department / custodian
/// / location. Omitted targets keep the asset's current value; ClearCustodian / ClearDepartment take them off (back to
/// a store). A new custodian brings their unit as the department unless one is given. A loan has an expected return.
public sealed record TransferAssetCommand(
  Guid AssetId,
  Guid? ToDepartmentId,
  Guid? ToCustodianId,
  Guid? ToLocationId,
  DateTime? AssignmentDate = null,
  DateOnly? ExpectedReturnDate = null,
  string? Reason = null,
  bool ClearCustodian = false,
  bool ClearDepartment = false) : ICommand<Result<TransferAssetCommandResult>>;

public class TransferAssetCommandValidator : AbstractValidator<TransferAssetCommand>
{
  public TransferAssetCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Reason).MaximumLength(1000);
    RuleFor(x => x)
      .Must(x => x.ToDepartmentId.HasValue || x.ToCustodianId.HasValue || x.ToLocationId.HasValue || x.ClearCustodian || x.ClearDepartment)
      .WithMessage("Say where the asset goes: a location, a person or a department.");
    RuleFor(x => x).Must(x => !(x.ClearCustodian && x.ToCustodianId.HasValue))
      .WithMessage("Choose a new custodian, or 'nobody' - not both.");
    RuleFor(x => x).Must(x => !(x.ClearDepartment && x.ToDepartmentId.HasValue))
      .WithMessage("Choose a new department, or 'none' - not both.");
  }
}
