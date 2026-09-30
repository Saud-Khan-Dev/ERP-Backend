public class OutsourcingActionsHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateOutsourcingCommand, Result<OutsourcingActionResult>>,
    ICommandHandler<ChangeContractStatusCommand, Result<OutsourcingActionResult>>,
    ICommandHandler<TerminateOutsourcingCommand, Result<OutsourcingActionResult>>
{
  public async Task<Result<OutsourcingActionResult>> Handle(UpdateOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    contract.Update(await Current(contract, cancellationToken), await command.Terms.ToTermsAsync(masters, cancellationToken));
    return await SaveAsync(contract, cancellationToken);
  }

  public async Task<Result<OutsourcingActionResult>> Handle(ChangeContractStatusCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    contract.ChangeStatus(await Current(contract, cancellationToken), await masters.GetAsync<ContractStatus>(command.ContractStatusId, cancellationToken));
    return await SaveAsync(contract, cancellationToken);
  }

  public async Task<Result<OutsourcingActionResult>> Handle(TerminateOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    contract.Terminate(await Current(contract, cancellationToken),
      await masters.GetByCodeAsync<ContractStatus>(SystemMasterCodes.Terminated, cancellationToken), command.TerminationDate, command.Reason);
    return await SaveAsync(contract, cancellationToken);
  }

  private Task<ContractStatus> Current(PropertyOutsourcing contract, CancellationToken cancellationToken) =>
      masters.GetAsync<ContractStatus>(contract.ContractStatusId.Value, cancellationToken);

  private async Task<Result<OutsourcingActionResult>> SaveAsync(PropertyOutsourcing contract, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<ContractStatus>(contract.ContractStatusId).LoadAsync(cancellationToken);
    return Result<OutsourcingActionResult>.Success(new OutsourcingActionResult(contract.Id.Value, refs[contract.ContractStatusId]));
  }
}
