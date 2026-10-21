using Microsoft.EntityFrameworkCore;

public class DeleteDepreciationMethodHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteDepreciationMethodCommand, Result<DeleteDepreciationMethodCommandResult>>
{
  public async Task<Result<DeleteDepreciationMethodCommandResult>> Handle(DeleteDepreciationMethodCommand command, CancellationToken cancellationToken)
  {
    var id = DepreciationMethodId.Of(command.Id);
    var method = await context.DepreciationMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
      ?? throw new DepreciationMethodNotFoundException($"Depreciation method {command.Id} was not found.");

    method.EnsureDeletable();

    if (await context.AssetDepreciationSchedules.AnyAsync(s => s.MethodId == id, cancellationToken))
      return Result<DeleteDepreciationMethodCommandResult>.Failure($"'{method.Name.Value}' is used by depreciation schedules, so it cannot be deleted. Switch it off instead.");

    context.DepreciationMethods.Remove(method);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteDepreciationMethodCommandResult>.Success(new DeleteDepreciationMethodCommandResult(true));
  }
}
