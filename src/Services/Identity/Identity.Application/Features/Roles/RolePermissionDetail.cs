using Microsoft.EntityFrameworkCore;

/// The detail line for a change to what a role carries: the codes of the permissions added and
/// removed, grouped by module, a whole module written "MODULE:*" (ActivityDetail.Permissions).
public static class RolePermissionDetail
{
  public static async Task<string?> DescribeAsync(IApplicationDbContext context,
      IEnumerable<PermissionId> added, IEnumerable<PermissionId> removed, CancellationToken cancellationToken)
  {
    var catalogue = await context.Permissions.AsNoTracking()
      .Select(p => new { p.Id, p.Code, p.IsActive })
      .ToListAsync(cancellationToken);

    var codes = catalogue.ToDictionary(p => p.Id, p => p.Code.Value);
    var moduleSizes = catalogue.Where(p => p.IsActive)
      .GroupBy(p => p.Code.Value.Split('.')[0])
      .ToDictionary(g => g.Key, g => g.Count());

    IEnumerable<string> Codes(IEnumerable<PermissionId> ids) => ids.Where(codes.ContainsKey).Select(id => codes[id]);
    return ActivityDetail.Permissions(Codes(added), Codes(removed), moduleSizes);
  }
}
