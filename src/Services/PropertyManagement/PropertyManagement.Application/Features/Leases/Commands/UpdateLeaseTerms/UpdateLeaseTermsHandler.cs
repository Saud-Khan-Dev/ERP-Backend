public class UpdateLeaseTermsHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateLeaseTermsCommand, Result<UpdateLeaseTermsCommandResult>>
{
  public async Task<Result<UpdateLeaseTermsCommandResult>> Handle(UpdateLeaseTermsCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    var current = await masters.GetAsync<LeaseStatus>(lease.LeaseStatusId.Value, cancellationToken);

    lease.UpdateTerms(current, await command.Terms.ToTermsAsync(masters, cancellationToken));

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateLeaseTermsCommandResult>.Success(new UpdateLeaseTermsCommandResult(true));
  }
}
