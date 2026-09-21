using Microsoft.EntityFrameworkCore;

public class DeleteDepreciationMethodHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteDepreciationMethodCommand, Result<DeleteDepreciationMethodCommandResult>>
{
  public async Task<Result<DeleteDepreciationMethodCommandResult>> Handle(DeleteDepreciationMethodCommand command, CancellationToken cancellationToken)
  {
    var id = DepreciationMethodId.Of(command.Id);
    var method = await context.DepreciationMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
      ?? throw new DepreciationMethodNotFoundException($"Depreciation method {command.Id} was not found.");

    if (await context.AssetDepreciationSchedules.AnyAsync(s => s.MethodId == id, cancellationToken))
      return Result<DeleteDepreciationMethodCommandResult>.Failure("This method is used by depreciation schedules. Deactivate it instead.");

    context.DepreciationMethods.Remove(method);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteDepreciationMethodCommandResult>.Success(new DeleteDepreciationMethodCommandResult(true));
  }
}
