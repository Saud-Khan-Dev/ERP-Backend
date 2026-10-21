using Microsoft.EntityFrameworkCore;

/// The assets (not deleted) whose form an assignment at a node can change: everything under the node.
public static class AssignmentReach
{
  public static async Task<List<Asset>> AssetsUnderAsync(IApplicationDbContext context, AttributeAssignment assignment, CancellationToken cancellationToken)
  {
    switch (assignment.Scope)
    {
      case AttributeScope.AssetClass:
        return await context.Assets.Where(a => a.AssetClassId == assignment.AssetClassId).ToListAsync(cancellationToken);
      case AttributeScope.AssetType:
        return await context.Assets.Where(a => a.AssetTypeId == assignment.AssetTypeId).ToListAsync(cancellationToken);
      case AttributeScope.Category:
      {
        var root = await context.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == assignment.CategoryId, cancellationToken);
        if (root is null) return [];
        var subtree = (await context.AssetCategories.AsNoTracking().Where(c => c.AssetTypeId == root.AssetTypeId).ToListAsync(cancellationToken))
          .Where(c => c.Id == root.Id || root.IsAncestorOf(c)).Select(c => c.Id).ToList();
        return await context.Assets.Where(a => subtree.Contains(a.CategoryId)).ToListAsync(cancellationToken);
      }
      default:
        return await context.Assets.Where(a => a.Id == assignment.AssetId).ToListAsync(cancellationToken);
    }
  }
}
