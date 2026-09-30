public class UpdateMasterHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateMasterCommand, Result<UpdateMasterCommandResult>>
{
  public async Task<Result<UpdateMasterCommandResult>> Handle(UpdateMasterCommand command, CancellationToken cancellationToken)
  {
    var descriptor = MasterRegistry.Get(command.Type);
    var id = MasterId.Of(command.Id);
    var input = command.Item;

    var master = await descriptor.FindAsync(context, id, cancellationToken)
      ?? throw new MasterDataNotFoundException($"{descriptor.Label} {command.Id} was not found.");

    var baseConflict = await MasterRules.FindBaseUnitConflictAsync(context, descriptor, id, input.IsBase, cancellationToken);
    if (baseConflict is not null)
      return Result<UpdateMasterCommandResult>.Failure(baseConflict);

    var extras = new MasterInput(master.Code.Value, input.Name, input.Description, input.SortOrder,
      input.FactorToBase, input.IsBase, input.StorageFolder, input.RequiresRelationship).Extras(descriptor);

    master.Update(Name.Of(input.Name, MasterData.NameMaxLength), input.Description, input.SortOrder, extras);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateMasterCommandResult>.Success(new UpdateMasterCommandResult(true));
  }
}
