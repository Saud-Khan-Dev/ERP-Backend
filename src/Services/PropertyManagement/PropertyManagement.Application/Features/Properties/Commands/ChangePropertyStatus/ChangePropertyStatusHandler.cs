using Microsoft.EntityFrameworkCore;

/// Closes the current status period and opens a new one (rule 2: history, never overwrite).
public class ChangePropertyStatusHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<ChangePropertyStatusCommand, Result<ChangePropertyStatusCommandResult>>
{
  public async Task<Result<ChangePropertyStatusCommandResult>> Handle(ChangePropertyStatusCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var status = await masters.GetAsync<PropertyStatus>(command.PropertyStatusId, cancellationToken);

    var current = await context.PropertyStatusHistories
        .Where(h => h.PropertyId == property.Id && h.EffectiveTo == null)
        .OrderByDescending(h => h.EffectiveFrom)
        .FirstOrDefaultAsync(cancellationToken);

    var next = property.ChangeStatus(
      status, current, command.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow), command.Reason, command.ReferenceNo);

    await context.PropertyStatusHistories.AddAsync(next, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ChangePropertyStatusCommandResult>.Success(new ChangePropertyStatusCommandResult(next.Id.Value));
  }
}
