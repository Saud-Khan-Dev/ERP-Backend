/// What a taxpayer may see: the tax files on which their Property owner record is a payer today (owner, lessee, tenant or
/// allottee), and everything under those files. The owner comes from the token (`own`), never from a request.
public static class TaxpayerScope
{
  public static IQueryable<TaxFileId> MyFileIds(this IApplicationDbContext context, Guid ownerId, DateOnly today) =>
      context.TaxFilePayers.Where(p => p.OwnerId == ownerId && p.EffectiveFrom <= today && (p.EffectiveTo == null || p.EffectiveTo >= today))
        .Select(p => p.TaxFileId).Distinct();

  /// What a taxpayer is shown of a demand: issued ones, never a draft or a cancelled one.
  public static bool ShownToTaxpayer(this Demand demand) => demand.Status is not (DemandStatus.Draft or DemandStatus.Cancelled);
}
