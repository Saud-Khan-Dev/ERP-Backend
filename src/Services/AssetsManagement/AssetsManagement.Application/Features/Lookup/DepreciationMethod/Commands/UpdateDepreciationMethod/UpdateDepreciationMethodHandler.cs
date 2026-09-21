using Microsoft.EntityFrameworkCore;

public class UpdateDepreciationMethodHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateDepreciationMethodCommand, Result<UpdateDepreciationMethodCommandResult>>
{
  public async Task<Result<UpdateDepreciationMethodCommandResult>> Handle(UpdateDepreciationMethodCommand command, CancellationToken cancellationToken)
  {
    var id = DepreciationMethodId.Of(command.Id);
    var method = await context.DepreciationMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
      ?? throw new DepreciationMethodNotFoundException($"Depreciation method {command.Id} was not found.");

    var input = command.Method;
    var code = LookupCode.Of(input.Code);

    if (await context.DepreciationMethods.AnyAsync(m => m.Id != id && m.Code == code, cancellationToken))
      return Result<UpdateDepreciationMethodCommandResult>.Failure($"A depreciation method with code {code.Value} already exists.");

    method.Update(code, Name.Of(input.Name, 100), input.Description, input.IsActive);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateDepreciationMethodCommandResult>.Success(new UpdateDepreciationMethodCommandResult(true));
  }
}
