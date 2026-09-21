using Microsoft.EntityFrameworkCore;

public class DeleteAssetStatusHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetStatusCommand, Result<DeleteAssetStatusCommandResult>>
{
  public async Task<Result<DeleteAssetStatusCommandResult>> Handle(DeleteAssetStatusCommand command, CancellationToken cancellationToken)
  {
    var id = AssetStatusId.Of(command.Id);
    var status = await context.AssetStatuses.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
      ?? throw new AssetStatusNotFoundException($"Asset status {command.Id} was not found.");

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.StatusId == id, cancellationToken)
        || await context.AssetLifecycleEvents.AnyAsync(e => e.FromStatusId == id || e.ToStatusId == id, cancellationToken))
      return Result<DeleteAssetStatusCommandResult>.Failure("This status is referenced by assets or lifecycle events. Deactivate it instead.");

    context.AssetStatuses.Remove(status);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetStatusCommandResult>.Success(new DeleteAssetStatusCommandResult(true));
  }
}
