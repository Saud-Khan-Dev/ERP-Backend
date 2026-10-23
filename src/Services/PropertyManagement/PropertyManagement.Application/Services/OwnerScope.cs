using Microsoft.EntityFrameworkCore;

/// Which owners a person with a town scope may see and act on. An owner is visible when they hold or held a share on a
/// property in the person's towns, are party to an allotment, lease, rental or contract there, or hold no share anywhere
/// (a new owner stays creatable and usable). Outside that the owner screens answer 404. A party's name on a record in
/// scope is always shown (the records carry it); this narrows only the owner register and an owner's own record.
public static class OwnerScope
{
  /// The owners the caller may see (all of them without a town scope). The share and agreement tables are narrowed to
  /// the caller's towns by the model's query filters; OwnershipLinks is not, so "holds nothing anywhere" is exact.
  public static IQueryable<PropertyOwner> VisibleOwners(this IApplicationDbContext context, IQueryable<PropertyOwner> owners)
  {
    if (context.TownScope is null)
      return owners;

    var links = context.OwnershipLinks;
    return owners.Where(o =>
      !links.Any(l => l.OwnerId == o.Id)
      || context.Ownerships.Any(x => x.OwnerId == o.Id)
      || context.Allotments.Any(x => x.AllotteeOwnerId == o.Id)
      || context.Leases.Any(x => x.LesseeOwnerId == o.Id)
      || context.Rentals.Any(x => x.TenantOwnerId == o.Id)
      || context.Outsourcings.Any(x => x.OutsourcedPartyOwnerId == o.Id));
  }

  /// 404 for an owner outside the caller's towns (or not on file at all).
  public static async Task EnsureOwnerVisibleAsync(this IApplicationDbContext context, OwnerId ownerId, CancellationToken cancellationToken)
  {
    if (!await context.VisibleOwners(context.Owners.AsNoTracking()).AnyAsync(o => o.Id == ownerId, cancellationToken))
      throw new OwnerNotFoundException($"Owner {ownerId.Value} was not found.");
  }

  /// A property may be registered in (or moved to) a town only by someone who works there (403 with the reason).
  public static void EnsureTownInScope(this IApplicationDbContext context, MasterId townId, string townName)
  {
    if (context.TownScope is { } towns && !towns.Contains(townId.Value))
      throw new PermissionRequiredException($"You work on other towns, not {townName}: a property there is registered or changed by someone who works on {townName} (Administration › Users › where they work).");
  }
}
