using Microsoft.EntityFrameworkCore;

/// A disposed asset (or one in a final status) has a closed record: what was on file when it left is what stays.
public static class AssetRecordGuard
{
  /// The sentence to refuse a change with, or null while the record is open.
  public static async Task<string?> ClosedReasonAsync(IApplicationDbContext context, Asset asset, CancellationToken cancellationToken)
  {
    if (await context.AssetDisposals.AsNoTracking().AnyAsync(d => d.AssetId == asset.Id, cancellationToken))
      return "This asset has been disposed. Its record is closed, so its files can no longer be added or removed.";

    var status = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == asset.StatusId, cancellationToken);
    return status is { IsTerminal: true }
      ? $"This asset is in the final status '{status.Name.Value}'. Its record is closed, so its files can no longer be added or removed."
      : null;
  }
}
