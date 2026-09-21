using Microsoft.EntityFrameworkCore;

public class CreateAssetCategoryHandler(IApplicationDbContext context)
  : ICommandHandler<CreateAssetCategoryCommand, Result<CreateAssetCategoryCommandResult>>
{
  public async Task<Result<CreateAssetCategoryCommandResult>> Handle(CreateAssetCategoryCommand command, CancellationToken cancellationToken)
  {
    var input = command.Category;
    var classId = AssetClassId.Of(input.AssetClassId);

    if (!await context.AssetClasses.AnyAsync(c => c.Id == classId, cancellationToken))
      throw new AssetClassNotFoundException($"Asset class {input.AssetClassId} was not found.");

    AssetTypeId? typeId = null;
    if (input.AssetTypeId.HasValue)
    {
      typeId = AssetTypeId.Of(input.AssetTypeId.Value);
      var assetType = await context.AssetTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
        ?? throw new AssetTypeNotFoundException($"Asset type {input.AssetTypeId} was not found.");

      if (assetType.AssetClassId != classId)
        return Result<CreateAssetCategoryCommandResult>.Failure("The asset type does not belong to the given asset class.");
    }

    AssetCategory? parent = null;
    if (input.ParentCategoryId.HasValue)
    {
      var parentId = AssetCategoryId.Of(input.ParentCategoryId.Value);
      parent = await context.AssetCategories.FirstOrDefaultAsync(c => c.Id == parentId, cancellationToken)
        ?? throw new AssetCategoryNotFoundException($"Parent category {input.ParentCategoryId} was not found.");

      // a leaf that already holds assets cannot become a branch (assets live on leaves only)
      if (parent.IsLeaf && await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.CategoryId == parentId, cancellationToken))
        return Result<CreateAssetCategoryCommandResult>.Failure("The parent category already has assets attached; move them before adding sub-categories.");
    }

    var code = LookupCode.Of(input.Code);

    if (await context.AssetCategories.AnyAsync(c => c.AssetClassId == classId && c.Code == code, cancellationToken))
      return Result<CreateAssetCategoryCommandResult>.Failure($"A category with code {code.Value} already exists in this class.");

    var category = AssetCategory.Create(
      id: AssetCategoryId.Of(Guid.NewGuid()),
      assetClassId: classId,
      assetTypeId: typeId,
      parent: parent,
      code: code,
      name: Name.Of(input.Name, 150),
      description: input.Description,
      displayOrder: input.DisplayOrder);

    if (!input.IsActive)
      category.Update(typeId, code, Name.Of(input.Name, 150), input.Description, input.DisplayOrder, false);

    await context.AssetCategories.AddAsync(category, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetCategoryCommandResult>.Success(new CreateAssetCategoryCommandResult(category.Id.Value));
  }
}
