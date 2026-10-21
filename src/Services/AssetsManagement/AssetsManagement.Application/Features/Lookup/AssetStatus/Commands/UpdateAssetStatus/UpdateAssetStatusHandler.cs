using Microsoft.EntityFrameworkCore;

public class UpdateAssetStatusHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateAssetStatusCommand, Result<UpdateAssetStatusCommandResult>>
{
  public async Task<Result<UpdateAssetStatusCommandResult>> Handle(UpdateAssetStatusCommand command, CancellationToken cancellationToken)
  {
    var id = AssetStatusId.Of(command.Id);
    var status = await context.AssetStatuses.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
      ?? throw new AssetStatusNotFoundException($"Asset status {command.Id} was not found.");

    var input = command.Status;
    var code = LookupCode.Of(input.Code);

    if (await context.AssetStatuses.AnyAsync(s => s.Id != id && s.Code == code, cancellationToken))
      return Result<UpdateAssetStatusCommandResult>.Failure($"An asset status with code {code.Value} already exists.");

    var inUse = await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.StatusId == id, cancellationToken)
                || await context.AssetLifecycleEvents.AnyAsync(e => e.FromStatusId == id || e.ToStatusId == id, cancellationToken);
    status.Update(code, Name.Of(input.Name, 100), input.Description, input.IsTerminal, input.AllowsAssignment, input.Color, input.DisplayOrder, input.IsActive, inUse);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAssetStatusCommandResult>.Success(new UpdateAssetStatusCommandResult(true));
  }
}
