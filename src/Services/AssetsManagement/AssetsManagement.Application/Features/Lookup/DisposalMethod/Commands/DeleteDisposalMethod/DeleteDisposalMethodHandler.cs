using Microsoft.EntityFrameworkCore;

public class DeleteDisposalMethodHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteDisposalMethodCommand, Result<DeleteDisposalMethodCommandResult>>
{
  public async Task<Result<DeleteDisposalMethodCommandResult>> Handle(DeleteDisposalMethodCommand command, CancellationToken cancellationToken)
  {
    var id = DisposalMethodId.Of(command.Id);
    var method = await context.DisposalMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
      ?? throw new DisposalMethodNotFoundException($"Disposal method {command.Id} was not found.");

    if (await context.AssetDisposals.AnyAsync(d => d.DisposalMethodId == id, cancellationToken))
      return Result<DeleteDisposalMethodCommandResult>.Failure("This method is used by disposal records. Deactivate it instead.");

    context.DisposalMethods.Remove(method);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteDisposalMethodCommandResult>.Success(new DeleteDisposalMethodCommandResult(true));
  }
}
