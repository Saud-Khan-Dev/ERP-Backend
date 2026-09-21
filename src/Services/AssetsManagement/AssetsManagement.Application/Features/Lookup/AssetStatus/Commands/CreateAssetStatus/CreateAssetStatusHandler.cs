using Microsoft.EntityFrameworkCore;

public class CreateAssetStatusHandler(IApplicationDbContext context)
  : ICommandHandler<CreateAssetStatusCommand, Result<CreateAssetStatusCommandResult>>
{
  public async Task<Result<CreateAssetStatusCommandResult>> Handle(CreateAssetStatusCommand command, CancellationToken cancellationToken)
  {
    var input = command.Status;
    var code = LookupCode.Of(input.Code);

    if (await context.AssetStatuses.AnyAsync(s => s.Code == code, cancellationToken))
      return Result<CreateAssetStatusCommandResult>.Failure($"An asset status with code {code.Value} already exists.");

    var status = AssetStatus.Create(
      AssetStatusId.Of(Guid.NewGuid()), code, Name.Of(input.Name, 100), input.Description,
      input.IsTerminal, input.AllowsAssignment, input.Color, input.DisplayOrder);

    if (!input.IsActive)
      status.Update(code, Name.Of(input.Name, 100), input.Description, input.IsTerminal, input.AllowsAssignment, input.Color, input.DisplayOrder, false);

    await context.AssetStatuses.AddAsync(status, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetStatusCommandResult>.Success(new CreateAssetStatusCommandResult(status.Id.Value));
  }
}
