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

    var scopeConflict = await MasterRules.FindScopeChangeConflictAsync(context, master, input.AppliesTo, cancellationToken);
    if (scopeConflict is not null)
      return Result<UpdateMasterCommandResult>.Failure(scopeConflict);

    // every stored area keeps its square feet computed with the factor of the day: a unit in use keeps its factor
    if (master is MeasurementUnit unit && input.FactorToBase is { } factor && decimal.Round(factor, 8) != unit.FactorToBase)
    {
      var used = (await context.ReferenceCountsAsync(typeof(MeasurementUnit), new[] { unit.Id.Value }, cancellationToken)).GetValueOrDefault(unit.Id.Value);
      if (used > 0)
        return Result<UpdateMasterCommandResult>.Failure(
          $"{unit.Name.Value} is used by {used} area(s) already, stored in square feet with its present factor ({unit.FactorToBase:0.########}). Its factor can no longer change; add a new unit instead.");
    }

    var extras = new MasterInput(master.Code.Value, input.Name, input.Description, input.SortOrder,
      input.FactorToBase, input.IsBase, input.StorageFolder, input.RequiresRelationship, input.AppliesTo).Extras(descriptor);

    master.Update(Name.Of(input.Name, MasterData.NameMaxLength), input.Description, input.SortOrder, extras);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateMasterCommandResult>.Success(new UpdateMasterCommandResult(true));
  }
}
