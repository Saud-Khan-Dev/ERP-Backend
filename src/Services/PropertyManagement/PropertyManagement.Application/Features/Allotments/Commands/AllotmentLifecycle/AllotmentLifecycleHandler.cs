public class AllotmentLifecycleHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<CancelAllotmentCommand, Result<AllotmentLifecycleResult>>,
    ICommandHandler<RestoreAllotmentCommand, Result<AllotmentLifecycleResult>>,
    ICommandHandler<ChangeAllotmentStatusCommand, Result<AllotmentLifecycleResult>>,
    ICommandHandler<DeactivateAllotmentCommand, Result<AllotmentLifecycleResult>>
{
  public async Task<Result<AllotmentLifecycleResult>> Handle(CancelAllotmentCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    allotment.Cancel(await Status(SystemMasterCodes.Cancelled, cancellationToken), command.CancellationDate, command.Reason, command.OrderRef);
    return await SaveAsync(allotment, cancellationToken);
  }

  public async Task<Result<AllotmentLifecycleResult>> Handle(RestoreAllotmentCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    allotment.Restore(await Status(SystemMasterCodes.Cancelled, cancellationToken), await Status(SystemMasterCodes.Restored, cancellationToken),
      command.RestorationDate, command.OrderRef);
    return await SaveAsync(allotment, cancellationToken);
  }

  public async Task<Result<AllotmentLifecycleResult>> Handle(ChangeAllotmentStatusCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    allotment.ChangeStatus(await masters.GetAsync<AllotmentStatus>(command.AllotmentStatusId, cancellationToken));
    return await SaveAsync(allotment, cancellationToken);
  }

  public async Task<Result<AllotmentLifecycleResult>> Handle(DeactivateAllotmentCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    allotment.Deactivate();
    return await SaveAsync(allotment, cancellationToken);
  }

  private Task<AllotmentStatus> Status(string code, CancellationToken cancellationToken) =>
      masters.GetByCodeAsync<AllotmentStatus>(code, cancellationToken);

  private async Task<Result<AllotmentLifecycleResult>> SaveAsync(PropertyAllotment allotment, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<AllotmentStatus>(allotment.AllotmentStatusId).LoadAsync(cancellationToken);
    return Result<AllotmentLifecycleResult>.Success(new AllotmentLifecycleResult(allotment.Id.Value, refs[allotment.AllotmentStatusId], allotment.IsActive));
  }
}
