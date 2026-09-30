public class UpdateRentalTermsHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateRentalTermsCommand, Result<UpdateRentalTermsCommandResult>>
{
  public async Task<Result<UpdateRentalTermsCommandResult>> Handle(UpdateRentalTermsCommand command, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(command.Id, cancellationToken);
    var current = await masters.GetAsync<RentalStatus>(rental.RentalStatusId.Value, cancellationToken);

    rental.UpdateTerms(current, await command.Terms.ToTermsAsync(masters, cancellationToken));

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateRentalTermsCommandResult>.Success(new UpdateRentalTermsCommandResult(true));
  }
}
