public class UpdateEncumbranceHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateEncumbranceCommand, Result<UpdateEncumbranceCommandResult>>
{
  public async Task<Result<UpdateEncumbranceCommandResult>> Handle(UpdateEncumbranceCommand command, CancellationToken cancellationToken)
  {
    var input = command.Encumbrance;
    var encumbrance = await context.LoadEncumbranceAsync(command.Id, cancellationToken);
    var type = await masters.GetAsync<EncumbranceType>(input.EncumbranceTypeId, cancellationToken);
    var (ownership, holder) = await EncumbranceLinks.LoadAsync(context, input, cancellationToken);

    encumbrance.Update(type, ownership, input.HolderName, holder, input.ReferenceNo, input.Amount, input.StartDate, input.EndDate, input.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateEncumbranceCommandResult>.Success(new UpdateEncumbranceCommandResult(true));
  }
}
