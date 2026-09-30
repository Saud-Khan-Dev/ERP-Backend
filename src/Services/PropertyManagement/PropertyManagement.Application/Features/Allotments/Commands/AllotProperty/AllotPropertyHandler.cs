public class AllotPropertyHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<AllotPropertyCommand, Result<AllotPropertyCommandResult>>
{
  public async Task<Result<AllotPropertyCommandResult>> Handle(AllotPropertyCommand command, CancellationToken cancellationToken)
  {
    var input = command.Allotment;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var allottee = await context.LoadOwnerAsync(command.AllotteeOwnerId, cancellationToken);

    var allotment = PropertyAllotment.Allot(
      AllotmentId.New(), property, allottee,
      await codes.NextAsync(CodeSequenceKeys.Allotment, cancellationToken),
      await masters.GetAsync<AllotmentType>(input.AllotmentTypeId, cancellationToken),
      await masters.GetByCodeAsync<AllotmentStatus>(SystemMasterCodes.Active, cancellationToken),
      input.AllotmentDate, input.EffectiveDate, input.ExpiryDate, input.AllotmentLetterRef, input.Conditions, input.Remarks);

    await context.Allotments.AddAsync(allotment, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<AllotPropertyCommandResult>.Success(new AllotPropertyCommandResult(allotment.Id.Value, allotment.AllotmentNo.Value));
  }
}
