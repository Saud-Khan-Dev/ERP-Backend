using Microsoft.EntityFrameworkCore;

public class CreateDisposalMethodHandler(IApplicationDbContext context)
  : ICommandHandler<CreateDisposalMethodCommand, Result<CreateDisposalMethodCommandResult>>
{
  public async Task<Result<CreateDisposalMethodCommandResult>> Handle(CreateDisposalMethodCommand command, CancellationToken cancellationToken)
  {
    var input = command.Method;
    var code = LookupCode.Of(input.Code);

    if (await context.DisposalMethods.AnyAsync(m => m.Code == code, cancellationToken))
      return Result<CreateDisposalMethodCommandResult>.Failure($"A disposal method with code {code.Value} already exists.");

    var method = DisposalMethod.Create(DisposalMethodId.Of(Guid.NewGuid()), code, Name.Of(input.Name, 100), input.RequiresValue);

    if (!input.IsActive)
      method.Update(code, Name.Of(input.Name, 100), input.RequiresValue, false);

    await context.DisposalMethods.AddAsync(method, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateDisposalMethodCommandResult>.Success(new CreateDisposalMethodCommandResult(method.Id.Value));
  }
}
