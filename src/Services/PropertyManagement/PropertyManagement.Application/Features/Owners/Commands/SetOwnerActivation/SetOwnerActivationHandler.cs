using Microsoft.EntityFrameworkCore;

public class SetOwnerActivationHandler(IApplicationDbContext context)
  : ICommandHandler<SetOwnerActivationCommand, Result<SetOwnerActivationCommandResult>>
{
  public async Task<Result<SetOwnerActivationCommandResult>> Handle(SetOwnerActivationCommand command, CancellationToken cancellationToken)
  {
    var owner = await context.LoadOwnerAsync(command.Id, cancellationToken);

    if (command.IsActive)
    {
      owner.Activate();
    }
    else
    {
      // a current owner cannot disappear from the registry while holding a share
      if (await context.Ownerships.AnyAsync(o => o.OwnerId == owner.Id && o.OwnershipStatus == OwnershipStatus.Active, cancellationToken))
        return Result<SetOwnerActivationCommandResult>.Failure(
          $"{owner.OwnerName.Value} still holds a current ownership share. End or transfer it first.");

      owner.Deactivate();
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetOwnerActivationCommandResult>.Success(new SetOwnerActivationCommandResult(owner.IsActive));
  }
}
