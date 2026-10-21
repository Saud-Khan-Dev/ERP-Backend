using Microsoft.EntityFrameworkCore;

/// Which agreements are in force, by status code. Shared by the agreements register (inForce, endsWithinDays,
/// overdue), the property register ("Allotted to ...") and owner deactivation, so they always agree.
///
///   allotment   — ACTIVE or RESTORED (and the row itself active), from the day it takes effect (its effective date,
///                 else the allotment date); CANCELLED, SURRENDERED, EXPIRED are not
///   lease       — ACTIVE; a DRAFT is not in force yet, EXPIRED, RENEWED, TERMINATED, CANCELLED no longer
///   rental      — ACTIVE; a DRAFT is not in force yet, ENDED, EXPIRED, TERMINATED, CANCELLED no longer
///   outsourcing — ACTIVE; a DRAFT is not in force yet, EXPIRED, RENEWED, TERMINATED no longer
///   auction     — never in force (a sale being run, not an agreement): AuctionStatuses are the RUNNING ones - PLANNED,
///                 ANNOUNCED or CONDUCTED; UNSUCCESSFUL, CANCELLED and AWARDED have closed it
public static class InForce
{
  public static readonly IReadOnlyList<string> AllotmentStatuses = [SystemMasterCodes.Active, SystemMasterCodes.Restored];
  public static readonly IReadOnlyList<string> LeaseStatuses = [SystemMasterCodes.Active];
  public static readonly IReadOnlyList<string> RentalStatuses = [SystemMasterCodes.Active];
  public static readonly IReadOnlyList<string> ContractStatuses = [SystemMasterCodes.Active];
  public static readonly IReadOnlyList<string> AuctionStatuses = [SystemMasterCodes.Planned, SystemMasterCodes.Announced, SystemMasterCodes.Conducted];

  /// The ids of one status table's rows with one of the codes (status tables are tiny, so they are read whole).
  public static async Task<List<MasterId>> StatusIdsAsync<T>(IApplicationDbContext context, IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
      where T : MasterData =>
      (await context.Set<T>().AsNoTracking().ToListAsync(cancellationToken))
        .Where(s => codes.Contains(s.Code.Value))
        .Select(s => s.Id)
        .ToList();
}
