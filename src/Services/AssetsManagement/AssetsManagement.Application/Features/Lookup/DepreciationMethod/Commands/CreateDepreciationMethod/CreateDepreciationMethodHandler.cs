using Microsoft.EntityFrameworkCore;

public class CreateDepreciationMethodHandler(IApplicationDbContext context)
  : ICommandHandler<CreateDepreciationMethodCommand, Result<CreateDepreciationMethodCommandResult>>
{
  public async Task<Result<CreateDepreciationMethodCommandResult>> Handle(CreateDepreciationMethodCommand command, CancellationToken cancellationToken)
  {
    var input = command.Method;
    var code = LookupCode.Of(input.Code);

    if (await context.DepreciationMethods.AnyAsync(m => m.Code == code, cancellationToken))
      return Result<CreateDepreciationMethodCommandResult>.Failure($"A depreciation method with code {code.Value} already exists.");

    var method = DepreciationMethod.Create(DepreciationMethodId.Of(Guid.NewGuid()), code, Name.Of(input.Name, 100), input.Description);

    if (!input.IsActive)
      method.Update(code, Name.Of(input.Name, 100), input.Description, false);

    await context.DepreciationMethods.AddAsync(method, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateDepreciationMethodCommandResult>.Success(new CreateDepreciationMethodCommandResult(method.Id.Value));
  }
}
