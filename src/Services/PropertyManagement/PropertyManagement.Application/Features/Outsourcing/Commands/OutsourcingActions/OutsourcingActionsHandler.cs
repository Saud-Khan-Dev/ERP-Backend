public class OutsourcingActionsHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<UpdateOutsourcingCommand, Result<OutsourcingActionResult>>,
    ICommandHandler<ChangeContractStatusCommand, Result<OutsourcingActionResult>>,
    ICommandHandler<ActivateOutsourcingCommand, Result<OutsourcingActionResult>>,
    ICommandHandler<ExpireOutsourcingCommand, Result<OutsourcingActionResult>>,
    ICommandHandler<TerminateOutsourcingCommand, Result<OutsourcingActionResult>>,
    ICommandHandler<WithdrawOutsourcingDraftCommand, Result<OutsourcingActionResult>>,
    ICommandHandler<RenewOutsourcingCommand, Result<RenewOutsourcingCommandResult>>
{
  public async Task<Result<OutsourcingActionResult>> Handle(UpdateOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(contract.PropertyId, cancellationToken);
    contract.Update(await Current(contract, cancellationToken), await command.Terms.ToTermsAsync(masters, cancellationToken));
    return await SaveAsync(contract, cancellationToken);
  }

  public async Task<Result<OutsourcingActionResult>> Handle(ChangeContractStatusCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(contract.PropertyId, cancellationToken);
    contract.ChangeStatus(await Current(contract, cancellationToken), await masters.GetAsync<ContractStatus>(command.ContractStatusId, cancellationToken),
      PropertyWriteGuard.Today());
    return await SaveAsync(contract, cancellationToken);
  }

  public async Task<Result<OutsourcingActionResult>> Handle(ActivateOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    var property = await context.EnsurePropertyActiveAsync(contract.PropertyId, cancellationToken);
    contract.Activate(await Current(contract, cancellationToken), await Status(SystemMasterCodes.Active, cancellationToken));

    context.Touch(property);
    var contractor = await context.LoadOwnerAsync(contract.OutsourcedPartyOwnerId.Value, cancellationToken);
    var warnings = AgreementOverlap.Check(contract.Span(contractor.OwnerName.Value), await context.RunningOnAsync(property.Id, cancellationToken));
    return await SaveAsync(contract, cancellationToken, warnings);
  }

  public async Task<Result<OutsourcingActionResult>> Handle(ExpireOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(contract.PropertyId, cancellationToken);
    contract.Expire(await Current(contract, cancellationToken), await Status(SystemMasterCodes.Expired, cancellationToken), PropertyWriteGuard.Today());
    return await SaveAsync(contract, cancellationToken);
  }

  public async Task<Result<OutsourcingActionResult>> Handle(TerminateOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(contract.PropertyId, cancellationToken);
    contract.Terminate(await Current(contract, cancellationToken), await Status(SystemMasterCodes.Terminated, cancellationToken),
      command.TerminationDate, command.Reason, PropertyWriteGuard.Today());
    return await SaveAsync(contract, cancellationToken);
  }

  public async Task<Result<OutsourcingActionResult>> Handle(WithdrawOutsourcingDraftCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(contract.PropertyId, cancellationToken);
    contract.WithdrawDraft(await Current(contract, cancellationToken), await Status(SystemMasterCodes.Withdrawn, cancellationToken), command.Reason);
    return await SaveAsync(contract, cancellationToken);
  }

  public async Task<Result<RenewOutsourcingCommandResult>> Handle(RenewOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var contract = await context.LoadOutsourcingAsync(command.Id, cancellationToken);
    var property = await context.EnsurePropertyActiveAsync(contract.PropertyId, cancellationToken);
    var contractor = await context.LoadOwnerAsync(command.OutsourcedPartyOwnerId ?? contract.OutsourcedPartyOwnerId.Value, cancellationToken);

    var renewal = contract.Renew(
      await Current(contract, cancellationToken),
      await Status(SystemMasterCodes.Renewed, cancellationToken),
      await Status(SystemMasterCodes.Active, cancellationToken),
      OutsourcingId.New(),
      await codes.NextAsync(CodeSequenceKeys.Contract, cancellationToken),
      contractor,
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    context.Touch(property);
    var warnings = AgreementOverlap.Check(renewal.Span(contractor.OwnerName.Value), await context.RunningOnAsync(property.Id, cancellationToken), except: contract.ContractNo.Value);

    await context.Outsourcings.AddAsync(renewal, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RenewOutsourcingCommandResult>.Success(new RenewOutsourcingCommandResult(renewal.Id.Value, renewal.ContractNo.Value, contract.Id.Value, warnings));
  }

  private Task<ContractStatus> Current(PropertyOutsourcing contract, CancellationToken cancellationToken) =>
      masters.GetAsync<ContractStatus>(contract.ContractStatusId.Value, cancellationToken);

  private Task<ContractStatus> Status(string code, CancellationToken cancellationToken) =>
      masters.GetByCodeAsync<ContractStatus>(code, cancellationToken);

  private async Task<Result<OutsourcingActionResult>> SaveAsync(PropertyOutsourcing contract, CancellationToken cancellationToken, IReadOnlyList<string>? warnings = null)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<ContractStatus>(contract.ContractStatusId).LoadAsync(cancellationToken);
    return Result<OutsourcingActionResult>.Success(new OutsourcingActionResult(contract.Id.Value, refs[contract.ContractStatusId], warnings ?? Array.Empty<string>()));
  }
}
