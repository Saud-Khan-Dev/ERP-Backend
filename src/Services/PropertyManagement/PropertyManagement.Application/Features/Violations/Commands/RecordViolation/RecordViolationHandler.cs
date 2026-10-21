using Microsoft.EntityFrameworkCore;

/// Records the violation and applies rule 5 in the same transaction: if it is the third within the
/// notice period, the linked lease / rental is cancelled - a cancellation, so the person recording it must hold the
/// approval right (403 otherwise, and nothing is saved).
public class RecordViolationHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<RecordViolationCommand, Result<RecordViolationCommandResult>>
{
  public async Task<Result<RecordViolationCommandResult>> Handle(RecordViolationCommand command, CancellationToken cancellationToken)
  {
    var input = command.Violation;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var agreementType = await masters.GetAsync<AgreementType>(input.AgreementTypeId, cancellationToken);
    var violator = await context.LoadOwnerAsync(input.ViolatorOwnerId, cancellationToken);

    var lease = input.LeaseId is { } leaseId ? await context.LoadLeaseAsync(leaseId, cancellationToken) : null;
    var rental = input.RentalId is { } rentalId ? await context.LoadRentalAsync(rentalId, cancellationToken) : null;
    var transfer = input.TransferId is { } transferId ? await context.LoadTransferAsync(transferId, cancellationToken) : null;
    var allotment = input.AllotmentId is { } allotmentId ? await context.LoadAllotmentAsync(allotmentId, cancellationToken) : null;

    // the agreement's other violations; the domain counts only those dated on or before this one
    var earlier = context.Violations.AsNoTracking().Where(v => v.PropertyId == property.Id);
    if (lease is not null) earlier = earlier.Where(v => v.LeaseId == lease.Id);
    if (rental is not null) earlier = earlier.Where(v => v.RentalId == rental.Id);
    if (transfer is not null) earlier = earlier.Where(v => v.TransferId == transfer.Id);
    if (allotment is not null) earlier = earlier.Where(v => v.AllotmentId == allotment.Id);
    var earlierViolations = await earlier.ToListAsync(cancellationToken);

    var leaseStatus = lease is null ? null : await masters.GetAsync<LeaseStatus>(lease.LeaseStatusId.Value, cancellationToken);
    var rentalStatus = rental is null ? null : await masters.GetAsync<RentalStatus>(rental.RentalStatusId.Value, cancellationToken);
    var inForce = (lease is not null && lease.IsInForce(leaseStatus!)) || (rental is not null && rental.IsInForce(rentalStatus!));

    var violation = AgreementViolation.Record(
      ViolationId.New(), property, agreementType,
      new AgreementViolation.Agreement(lease, rental, transfer, allotment),
      violator, input.ViolationDate, input.ViolationDescription,
      new AgreementViolation.Notice(input.NoticeNo, input.NoticeDate, input.NoticeDeadline),
      earlierViolations, inForce, input.Remarks, PropertyWriteGuard.Today(), out var warnings);

    if (violation.LedToCancellation)
    {
      if (!currentUser.HasPermission(PermissionCatalog.Property.Approve))
        throw new PermissionRequiredException(
          $"This is violation no. {violation.OccurrenceNo} within the notice period, so recording it cancels {(lease is not null ? lease.LeaseNo.Value : rental!.RentalNo.Value)} (Act s.28-A). "
          + "A cancellation is an approver's act: ask an officer who can approve to record it.");

      var reason = $"Cancelled under Act s.28-A: violation no. {violation.OccurrenceNo} within the notice period.";

      if (lease is not null)
        lease.Cancel(leaseStatus!, await masters.GetByCodeAsync<LeaseStatus>(SystemMasterCodes.Cancelled, cancellationToken), input.ViolationDate, reason);

      if (rental is not null)
        rental.Cancel(rentalStatus!, await masters.GetByCodeAsync<RentalStatus>(SystemMasterCodes.Cancelled, cancellationToken), input.ViolationDate, reason);
    }

    await context.Violations.AddAsync(violation, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordViolationCommandResult>.Success(
      new RecordViolationCommandResult(violation.Id.Value, violation.OccurrenceNo, violation.LedToCancellation, warnings));
  }
}
