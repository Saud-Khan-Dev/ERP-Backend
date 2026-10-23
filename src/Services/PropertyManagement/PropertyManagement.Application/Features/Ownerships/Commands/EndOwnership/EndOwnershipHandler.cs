public class EndOwnershipHandler(IApplicationDbContext context, IAuditTrail audit)
  : ICommandHandler<EndOwnershipCommand, Result<EndOwnershipCommandResult>>
{
  public async Task<Result<EndOwnershipCommandResult>> Handle(EndOwnershipCommand command, CancellationToken cancellationToken)
  {
    var ownership = await context.LoadOwnershipAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(ownership.PropertyId.Value, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, new[] { ownership.OwnerId }, cancellationToken);

    ownership.EndHolding(command.EffectiveTo, command.EndReferenceNo, command.Remarks, record);
    audit.Record(PropertyAuditActions.Ended, ownership, string.Join(" · ", new[] { $"order {command.EndReferenceNo}", command.Remarks }.Where(x => !string.IsNullOrWhiteSpace(x))));

    await context.SaveChangesAsync(cancellationToken);
    return Result<EndOwnershipCommandResult>.Success(new EndOwnershipCommandResult(true));
  }
}
