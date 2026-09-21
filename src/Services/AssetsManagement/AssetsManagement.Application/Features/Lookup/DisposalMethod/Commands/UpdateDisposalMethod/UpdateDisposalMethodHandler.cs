using Microsoft.EntityFrameworkCore;

public class UpdateDisposalMethodHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateDisposalMethodCommand, Result<UpdateDisposalMethodCommandResult>>
{
  public async Task<Result<UpdateDisposalMethodCommandResult>> Handle(UpdateDisposalMethodCommand command, CancellationToken cancellationToken)
  {
    var id = DisposalMethodId.Of(command.Id);
    var method = await context.DisposalMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
      ?? throw new DisposalMethodNotFoundException($"Disposal method {command.Id} was not found.");

    var input = command.Method;
    var code = LookupCode.Of(input.Code);

    if (await context.DisposalMethods.AnyAsync(m => m.Id != id && m.Code == code, cancellationToken))
      return Result<UpdateDisposalMethodCommandResult>.Failure($"A disposal method with code {code.Value} already exists.");

    method.Update(code, Name.Of(input.Name, 100), input.RequiresValue, input.IsActive);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateDisposalMethodCommandResult>.Success(new UpdateDisposalMethodCommandResult(true));
  }
}
