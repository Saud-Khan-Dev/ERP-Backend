using Microsoft.EntityFrameworkCore;

public class FileAppealHandler(IApplicationDbContext context, CodeGenerator codes)
  : ICommandHandler<FileAppealCommand, Result<FileAppealCommandResult>>
{
  public async Task<Result<FileAppealCommandResult>> Handle(FileAppealCommand command, CancellationToken cancellationToken)
  {
    var input = command.Appeal;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);

    // the order being appealed must exist on this property, like a document's entity (rule 10)
    if (input.OrderSourceTable is { } source && input.OrderSourceId is { } sourceId)
      await EnsureSourceExistsAsync(property.Id, source, sourceId, cancellationToken);

    var appeal = PropertyAppeal.File(
      AppealId.New(), property, await codes.NextAsync(CodeSequenceKeys.Appeal, cancellationToken),
      await input.ToFilingAsync(context, cancellationToken), out var warnings);

    if (input.OrderSourceTable == AppealOrderSource.AgreementViolation && input.OrderSourceId is { } violationId)
      (await context.LoadViolationAsync(violationId, cancellationToken)).MarkAppealed();

    await context.Appeals.AddAsync(appeal, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<FileAppealCommandResult>.Success(
      new FileAppealCommandResult(appeal.Id.Value, appeal.AppealNo.Value, appeal.DecisionDueDate, warnings));
  }

  private async Task EnsureSourceExistsAsync(PropertyId propertyId, AppealOrderSource source, Guid id, CancellationToken cancellationToken)
  {
    var exists = source switch
    {
      AppealOrderSource.PropertyAllotment => await context.Allotments.AnyAsync(x => x.Id == AllotmentId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      AppealOrderSource.AgreementViolation => await context.Violations.AnyAsync(x => x.Id == ViolationId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      AppealOrderSource.PropertyEncroachment => await context.Encroachments.AnyAsync(x => x.Id == EncroachmentId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      AppealOrderSource.PropertyLease => await context.Leases.AnyAsync(x => x.Id == LeaseId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      AppealOrderSource.PropertyRental => await context.Rentals.AnyAsync(x => x.Id == RentalId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      AppealOrderSource.BuildingPlan => await context.BuildingPlans.AnyAsync(x => x.Id == BuildingPlanId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      AppealOrderSource.PropertyTransfer => await context.Transfers.AnyAsync(x => x.Id == TransferId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      _ => true
    };

    if (!exists)
      throw new DomainException($"The appealed order's record ({source} {id}) was not found on this property.");
  }
}
