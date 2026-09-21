using Microsoft.EntityFrameworkCore;

public class CreateAssetTypeHandler(IApplicationDbContext context)
  : ICommandHandler<CreateAssetTypeCommand, Result<CreateAssetTypeCommandResult>>
{
  public async Task<Result<CreateAssetTypeCommandResult>> Handle(CreateAssetTypeCommand command, CancellationToken cancellationToken)
  {
    var input = command.AssetType;
    var classId = AssetClassId.Of(input.AssetClassId);

    if (!await context.AssetClasses.AnyAsync(c => c.Id == classId, cancellationToken))
      throw new AssetClassNotFoundException($"Asset class {input.AssetClassId} was not found.");

    var code = LookupCode.Of(input.Code);

    if (await context.AssetTypes.AnyAsync(t => t.AssetClassId == classId && t.Code == code, cancellationToken))
      return Result<CreateAssetTypeCommandResult>.Failure($"An asset type with code {code.Value} already exists in this class.");

    var assetType = AssetType.Create(
      id: AssetTypeId.Of(Guid.NewGuid()),
      assetClassId: classId,
      code: code,
      name: Name.Of(input.Name, 100),
      description: input.Description,
      isDepreciable: input.IsDepreciable,
      requiresLocation: input.RequiresLocation,
      requiresCustodian: input.RequiresCustodian,
      displayOrder: input.DisplayOrder);

    if (!input.IsActive)
      assetType.Update(code, Name.Of(input.Name, 100), input.Description, input.IsDepreciable, input.RequiresLocation, input.RequiresCustodian, input.DisplayOrder, false);

    await context.AssetTypes.AddAsync(assetType, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetTypeCommandResult>.Success(new CreateAssetTypeCommandResult(assetType.Id.Value));
  }
}
