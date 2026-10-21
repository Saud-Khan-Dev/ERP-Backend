public class AllotmentLifecycleHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<CancelAllotmentCommand, Result<AllotmentLifecycleResult>>,
    ICommandHandler<RestoreAllotmentCommand, Result<AllotmentLifecycleResult>>,
    ICommandHandler<ChangeAllotmentStatusCommand, Result<AllotmentLifecycleResult>>,
    ICommandHandler<DeactivateAllotmentCommand, Result<AllotmentLifecycleResult>>
{
  public async Task<Result<AllotmentLifecycleResult>> Handle(CancelAllotmentCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    var record = await RecordAsync(allotment, cancellationToken);

    var ended = allotment.Cancel(
      await CurrentStatusAsync(allotment, cancellationToken), await Status(SystemMasterCodes.Cancelled, cancellationToken),
      command.CancellationDate, command.Reason, command.OrderRef, record);

    return await SaveAsync(allotment, ended, cancellationToken);
  }

  public async Task<Result<AllotmentLifecycleResult>> Handle(RestoreAllotmentCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(allotment.PropertyId.Value, cancellationToken);
    context.Touch(property);

    allotment.Restore(
      property, await CurrentStatusAsync(allotment, cancellationToken), await Status(SystemMasterCodes.Restored, cancellationToken),
      command.RestorationDate, command.OrderRef,
      await context.AllotmentInForceAsync(property.Id, allotment.Id, cancellationToken),
      EntityLoaders.Today);

    return await SaveAsync(allotment, Array.Empty<PropertyOwnership>(), cancellationToken);
  }

  public async Task<Result<AllotmentLifecycleResult>> Handle(ChangeAllotmentStatusCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    var status = await masters.GetAsync<AllotmentStatus>(command.AllotmentStatusId, cancellationToken);

    // a surrender ends the allottee's hold on the plot (and an ownership confirmed through it): a decision, not a record
    if (status.Is(PropertyAllotment.SurrenderedCode) && !currentUser.HasPermission(PermissionCatalog.Property.Approve))
      throw new PermissionRequiredException("Accepting the surrender of an allotment is an approver's act; ask an officer who can approve.");

    var record = await RecordAsync(allotment, cancellationToken);

    var ended = allotment.ChangeStatus(
      await CurrentStatusAsync(allotment, cancellationToken),
      status,
      command.Date, record);

    return await SaveAsync(allotment, ended, cancellationToken);
  }

  public async Task<Result<AllotmentLifecycleResult>> Handle(DeactivateAllotmentCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    var record = await RecordAsync(allotment, cancellationToken);

    allotment.Deactivate(record.Property, record.Ownerships);
    return await SaveAsync(allotment, Array.Empty<PropertyOwnership>(), cancellationToken);
  }

  private async Task<OwnershipRecord> RecordAsync(PropertyAllotment allotment, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(allotment.PropertyId.Value, cancellationToken);
    return await context.LoadOwnershipRecordAsync(property, new[] { allotment.AllotteeOwnerId }, cancellationToken);
  }

  private Task<AllotmentStatus> CurrentStatusAsync(PropertyAllotment allotment, CancellationToken cancellationToken) =>
      masters.GetAsync<AllotmentStatus>(allotment.AllotmentStatusId.Value, cancellationToken);

  private Task<AllotmentStatus> Status(string code, CancellationToken cancellationToken) =>
      masters.GetByCodeAsync<AllotmentStatus>(code, cancellationToken);

  private async Task<Result<AllotmentLifecycleResult>> SaveAsync(
      PropertyAllotment allotment, IReadOnlyList<PropertyOwnership> ended, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<AllotmentStatus>(allotment.AllotmentStatusId).LoadAsync(cancellationToken);
    return Result<AllotmentLifecycleResult>.Success(new AllotmentLifecycleResult(
      allotment.Id.Value, refs[allotment.AllotmentStatusId], allotment.IsActive, ended.Select(o => o.Id.Value).ToList()));
  }
}
