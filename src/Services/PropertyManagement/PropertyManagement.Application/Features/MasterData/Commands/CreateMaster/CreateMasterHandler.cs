public class CreateMasterHandler(IApplicationDbContext context)
  : ICommandHandler<CreateMasterCommand, Result<CreateMasterCommandResult>>
{
  public async Task<Result<CreateMasterCommandResult>> Handle(CreateMasterCommand command, CancellationToken cancellationToken)
  {
    var descriptor = MasterRegistry.Get(command.Type);
    var input = command.Item;
    var code = MasterCode.Of(input.Code);

    if (await descriptor.CodeExistsAsync(context, code, cancellationToken))
      return Result<CreateMasterCommandResult>.Failure($"{descriptor.Label} with code {code.Value} already exists.");

    var baseConflict = await MasterRules.FindBaseUnitConflictAsync(context, descriptor, null, input.IsBase, cancellationToken);
    if (baseConflict is not null)
      return Result<CreateMasterCommandResult>.Failure(baseConflict);

    var master = descriptor.Create(
      MasterId.New(), code, Name.Of(input.Name, MasterData.NameMaxLength), input.Description, input.SortOrder, input.Extras(descriptor));

    await descriptor.AddAsync(context, master, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateMasterCommandResult>.Success(new CreateMasterCommandResult(master.Id.Value));
  }
}
