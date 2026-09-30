using Microsoft.EntityFrameworkCore;

/// Loaders for the management and compliance aggregates (tracked, children included).
public static class ComplianceLoaders
{
  public static async Task<PropertyAllotment> LoadAllotmentAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = AllotmentId.Of(id);
    return await context.Allotments.FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new AllotmentNotFoundException($"Allotment {id} was not found.");
  }

  public static async Task<PropertyLease> LoadLeaseAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = LeaseId.Of(id);
    return await context.Leases.FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new LeaseNotFoundException($"Lease {id} was not found.");
  }

  public static async Task<PropertyRental> LoadRentalAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = RentalId.Of(id);
    return await context.Rentals.FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new RentalNotFoundException($"Rental {id} was not found.");
  }

  public static async Task<AgreementViolation> LoadViolationAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = ViolationId.Of(id);
    return await context.Violations.FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new ViolationNotFoundException($"Agreement violation {id} was not found.");
  }

  public static async Task<PropertyAuction> LoadAuctionAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = AuctionId.Of(id);
    return await context.Auctions.Include(x => x.Bids).FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new AuctionNotFoundException($"Auction {id} was not found.");
  }

  public static async Task<PropertyOutsourcing> LoadOutsourcingAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = OutsourcingId.Of(id);
    return await context.Outsourcings.FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new OutsourcingNotFoundException($"Outsourcing contract {id} was not found.");
  }

  public static async Task<PropertyBoundary> LoadBoundaryAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = BoundaryId.Of(id);
    return await context.Boundaries.Include(x => x.Points).FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new BoundaryNotFoundException($"Boundary {id} was not found.");
  }

  public static async Task<PropertyEncroachment> LoadEncroachmentAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = EncroachmentId.Of(id);
    return await context.Encroachments.Include(x => x.Points).FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new EncroachmentNotFoundException($"Encroachment {id} was not found.");
  }

  public static async Task<PropertyLitigation> LoadLitigationAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = LitigationId.Of(id);
    return await context.Litigations.Include(x => x.Parties).Include(x => x.Hearings).FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new LitigationNotFoundException($"Litigation {id} was not found.");
  }

  public static async Task<PropertyAppeal> LoadAppealAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = AppealId.Of(id);
    return await context.Appeals.FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new AppealNotFoundException($"Appeal {id} was not found.");
  }

  public static async Task<BuildingPlan> LoadBuildingPlanAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var key = BuildingPlanId.Of(id);
    return await context.BuildingPlans.FirstOrDefaultAsync(x => x.Id == key, cancellationToken)
      ?? throw new BuildingPlanNotFoundException($"Building plan {id} was not found.");
  }

  /// Checks a property exists (for list queries that do not need the entity itself).
  public static async Task<PropertyId> EnsurePropertyExistsAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(id);

    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {id} was not found.");

    return propertyId;
  }
}
