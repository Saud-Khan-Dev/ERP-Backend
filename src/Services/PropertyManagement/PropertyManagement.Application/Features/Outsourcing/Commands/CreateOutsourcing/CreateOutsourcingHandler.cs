public class CreateOutsourcingHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<CreateOutsourcingCommand, Result<CreateOutsourcingCommandResult>>
{
  public async Task<Result<CreateOutsourcingCommandResult>> Handle(CreateOutsourcingCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var contractor = await context.LoadOwnerAsync(command.OutsourcedPartyOwnerId, cancellationToken);

    var contract = PropertyOutsourcing.Create(
      OutsourcingId.New(), property, contractor, await codes.NextAsync(CodeSequenceKeys.Contract, cancellationToken),
      await masters.GetByCodeAsync<ContractStatus>(command.AsDraft ? SystemMasterCodes.Draft : SystemMasterCodes.Active, cancellationToken),
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    // one contract of a type at a time on a property; a draft is checked when it is activated
    IReadOnlyList<string> warnings = Array.Empty<string>();
    if (!command.AsDraft)
    {
      context.Touch(property);
      warnings = AgreementOverlap.Check(contract.Span(contractor.OwnerName.Value), await context.RunningOnAsync(property.Id, cancellationToken));
    }

    await context.Outsourcings.AddAsync(contract, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateOutsourcingCommandResult>.Success(new CreateOutsourcingCommandResult(contract.Id.Value, contract.ContractNo.Value, warnings));
  }
}
