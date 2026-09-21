using Microsoft.EntityFrameworkCore;

public class UpdateAssetClassHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateAssetClassCommand, Result<UpdateAssetClassCommandResult>>
{
  public async Task<Result<UpdateAssetClassCommandResult>> Handle(UpdateAssetClassCommand command, CancellationToken cancellationToken)
  {
    var id = AssetClassId.Of(command.Id);
    var assetClass = await context.AssetClasses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
      ?? throw new AssetClassNotFoundException($"Asset class {command.Id} was not found.");

    var input = command.AssetClass;
    var code = LookupCode.Of(input.Code);

    if (await context.AssetClasses.AnyAsync(c => c.Id != id && c.Code == code, cancellationToken))
      return Result<UpdateAssetClassCommandResult>.Failure($"An asset class with code {code.Value} already exists.");

    assetClass.Update(code, Name.Of(input.Name, 100), input.Description, input.DisplayOrder, input.IsActive);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAssetClassCommandResult>.Success(new UpdateAssetClassCommandResult(true));
  }
}
