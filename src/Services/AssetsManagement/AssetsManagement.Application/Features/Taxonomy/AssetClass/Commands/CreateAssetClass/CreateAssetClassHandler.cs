using Microsoft.EntityFrameworkCore;

public class CreateAssetClassHandler(IApplicationDbContext context)
  : ICommandHandler<CreateAssetClassCommand, Result<CreateAssetClassCommandResult>>
{
  public async Task<Result<CreateAssetClassCommandResult>> Handle(CreateAssetClassCommand command, CancellationToken cancellationToken)
  {
    var input = command.AssetClass;
    var code = LookupCode.Of(input.Code);

    if (await context.AssetClasses.AnyAsync(c => c.Code == code, cancellationToken))
      return Result<CreateAssetClassCommandResult>.Failure($"An asset class with code {code.Value} already exists.");

    var assetClass = AssetClass.Create(
      id: AssetClassId.Of(Guid.NewGuid()),
      code: code,
      name: Name.Of(input.Name, 100),
      description: input.Description,
      displayOrder: input.DisplayOrder);

    if (!input.IsActive)
      assetClass.Deactivate();

    await context.AssetClasses.AddAsync(assetClass, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAssetClassCommandResult>.Success(new CreateAssetClassCommandResult(assetClass.Id.Value));
  }
}
