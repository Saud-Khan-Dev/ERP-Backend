public class RegisterEncumbranceHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<RegisterEncumbranceCommand, Result<RegisterEncumbranceCommandResult>>
{
  public async Task<Result<RegisterEncumbranceCommandResult>> Handle(RegisterEncumbranceCommand command, CancellationToken cancellationToken)
  {
    var input = command.Encumbrance;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var type = await masters.GetAsync<EncumbranceType>(input.EncumbranceTypeId, cancellationToken);
    var (ownership, holder) = await EncumbranceLinks.LoadAsync(context, input, cancellationToken);

    var encumbrance = PropertyEncumbrance.Register(
      EncumbranceId.New(), property, type, ownership, input.HolderName, holder,
      input.ReferenceNo, input.Amount, input.StartDate, input.EndDate, input.Remarks);

    await context.Encumbrances.AddAsync(encumbrance, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RegisterEncumbranceCommandResult>.Success(new RegisterEncumbranceCommandResult(encumbrance.Id.Value));
  }
}
