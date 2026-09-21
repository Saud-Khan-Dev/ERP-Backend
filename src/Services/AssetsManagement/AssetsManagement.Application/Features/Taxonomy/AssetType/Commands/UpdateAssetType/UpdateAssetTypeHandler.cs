using Microsoft.EntityFrameworkCore;

public class UpdateAssetTypeHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateAssetTypeCommand, Result<UpdateAssetTypeCommandResult>>
{
  public async Task<Result<UpdateAssetTypeCommandResult>> Handle(UpdateAssetTypeCommand command, CancellationToken cancellationToken)
  {
    var id = AssetTypeId.Of(command.Id);
    var assetType = await context.AssetTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
      ?? throw new AssetTypeNotFoundException($"Asset type {command.Id} was not found.");

    var input = command.AssetType;

    // The class is part of the composite FK from asset; moving a type between classes would orphan assets.
    if (assetType.AssetClassId.Value != input.AssetClassId)
      return Result<UpdateAssetTypeCommandResult>.Failure("An asset type cannot be moved to another asset class.");

    var code = LookupCode.Of(input.Code);

    if (await context.AssetTypes.AnyAsync(t => t.Id != id && t.AssetClassId == assetType.AssetClassId && t.Code == code, cancellationToken))
      return Result<UpdateAssetTypeCommandResult>.Failure($"An asset type with code {code.Value} already exists in this class.");

    assetType.Update(
      code,
      Name.Of(input.Name, 100),
      input.Description,
      input.IsDepreciable,
      input.RequiresLocation,
      input.RequiresCustodian,
      input.DisplayOrder,
      input.IsActive);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAssetTypeCommandResult>.Success(new UpdateAssetTypeCommandResult(true));
  }
}
