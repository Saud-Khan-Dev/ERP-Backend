public class CreateOutsourcingHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<CreateOutsourcingCommand, Result<CreateOutsourcingCommandResult>>
{
  public async Task<Result<CreateOutsourcingCommandResult>> Handle(CreateOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var contractor = await context.LoadOwnerAsync(command.OutsourcedPartyOwnerId, cancellationToken);

    var contract = PropertyOutsourcing.Create(
      OutsourcingId.New(), property, contractor, await codes.NextAsync(CodeSequenceKeys.Contract, cancellationToken),
      await masters.GetByCodeAsync<ContractStatus>(SystemMasterCodes.Active, cancellationToken),
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    await context.Outsourcings.AddAsync(contract, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateOutsourcingCommandResult>.Success(new CreateOutsourcingCommandResult(contract.Id.Value, contract.ContractNo.Value));
  }
}
