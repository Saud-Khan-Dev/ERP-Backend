using Microsoft.EntityFrameworkCore;

/// The agreements running on a property, as the overlap rule (AgreementOverlap) reads them: ACTIVE leases, rentals
/// and contracts, and the current allotment (ACTIVE / RESTORED, from the day it takes effect). Drafts are not in force
/// and are left out; a draft is checked when it is activated.
public static class AgreementSpans
{
  public static async Task<List<AgreementSpan>> RunningOnAsync(this IApplicationDbContext context, PropertyId propertyId, CancellationToken cancellationToken)
  {
    var activeLease = await InForce.StatusIdsAsync<LeaseStatus>(context, InForce.LeaseStatuses, cancellationToken);
    var activeRental = await InForce.StatusIdsAsync<RentalStatus>(context, InForce.RentalStatuses, cancellationToken);
    var activeContract = await InForce.StatusIdsAsync<ContractStatus>(context, InForce.ContractStatuses, cancellationToken);
    var currentAllotment = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);

    var leases = await context.Leases.AsNoTracking()
        .Where(l => l.PropertyId == propertyId && activeLease.Contains(l.LeaseStatusId)).ToListAsync(cancellationToken);
    var rentals = await context.Rentals.AsNoTracking()
        .Where(r => r.PropertyId == propertyId && activeRental.Contains(r.RentalStatusId)).ToListAsync(cancellationToken);
    var contracts = await context.Outsourcings.AsNoTracking()
        .Where(o => o.PropertyId == propertyId && activeContract.Contains(o.ContractStatusId)).ToListAsync(cancellationToken);
    var allotments = await context.Allotments.AsNoTracking()
        .Where(a => a.PropertyId == propertyId && a.IsActive && currentAllotment.Contains(a.AllotmentStatusId)).ToListAsync(cancellationToken);

    var partyIds = leases.Select(l => l.LesseeOwnerId)
        .Concat(rentals.Select(r => r.TenantOwnerId))
        .Concat(contracts.Select(c => c.OutsourcedPartyOwnerId))
        .Concat(allotments.Select(a => a.AllotteeOwnerId))
        .Distinct().ToList();
    var names = await context.Owners.AsNoTracking().Where(o => partyIds.Contains(o.Id))
        .ToDictionaryAsync(o => o.Id, o => o.OwnerName.Value, cancellationToken);
    string Name(OwnerId id) => names.GetValueOrDefault(id, "another party");

    return leases.Select(l => l.Span(Name(l.LesseeOwnerId)))
      .Concat(rentals.Select(r => r.Span(Name(r.TenantOwnerId))))
      .Concat(contracts.Select(c => c.Span(Name(c.OutsourcedPartyOwnerId))))
      .Concat(allotments.Select(a => new AgreementSpan(AgreementSpanKind.Allotment, a.AllotmentNo.Value, Name(a.AllotteeOwnerId), a.TakesEffectOn, a.ExpiryDate, a.AllotmentTypeId)))
      .ToList();
  }
}
