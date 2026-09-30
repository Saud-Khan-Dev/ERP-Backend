using Microsoft.EntityFrameworkCore;
using FluentValidation;

/// One master value as entered on the settings screen. The extra fields only apply to the masters that
/// carry them (measurement_unit, document_type, transfer_type) and are ignored elsewhere.
public sealed record MasterInput(
  string Code,
  string Name,
  string? Description = null,
  int SortOrder = 0,
  decimal? FactorToBase = null,
  bool? IsBase = null,
  string? StorageFolder = null,
  bool? RequiresRelationship = null)
{
  public MasterExtras Extras(MasterDescriptor descriptor) => new(
    descriptor.Extras.HasFlag(MasterExtraColumns.FactorToBase) ? FactorToBase : null,
    descriptor.Extras.HasFlag(MasterExtraColumns.IsBase) ? IsBase : null,
    descriptor.Extras.HasFlag(MasterExtraColumns.StorageFolder) ? StorageFolder : null,
    descriptor.Extras.HasFlag(MasterExtraColumns.RequiresRelationship) ? RequiresRelationship : null);
}

public class MasterInputValidator : AbstractValidator<MasterInput>
{
  public MasterInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(MasterCode.MaxLength);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(MasterData.NameMaxLength);
    RuleFor(x => x.Description).MaximumLength(2000);
    RuleFor(x => x.FactorToBase).GreaterThan(0).When(x => x.FactorToBase.HasValue);
    RuleFor(x => x.StorageFolder).MaximumLength(DocumentType.StorageFolderMaxLength);
  }
}

/// Rules that span rows of one master table.
public static class MasterRules
{
  /// Codes the application relies on (SystemMasterCodes) must stay active.
  public static void EnsureCanDeactivate(MasterDescriptor descriptor, MasterData master)
  {
    if (SystemMasterCodes.IsRequired(descriptor.ClrType, master.Code.Value))
      throw new DomainException($"{descriptor.Label} {master.Code.Value} is used by the system and cannot be deactivated.");
  }

  /// Only one measurement unit may be the base (square feet).
  public static async Task<string?> FindBaseUnitConflictAsync(IApplicationDbContext context, MasterDescriptor descriptor, MasterId? self, bool? isBase, CancellationToken cancellationToken)
  {
    if (descriptor.ClrType != typeof(MeasurementUnit) || isBase != true)
      return null;

    var other = await context.Set<MeasurementUnit>()
        .Where(u => u.IsBase && (self == null || u.Id != self))
        .Select(u => u.Code)
        .FirstOrDefaultAsync(cancellationToken);

    return other is null ? null : $"{other.Value} is already the base unit; only one unit can be the base.";
  }
}
