using Microsoft.EntityFrameworkCore;

public class DeleteAssetHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<DeleteAssetCommand, Result<DeleteAssetCommandResult>>
{
  public async Task<Result<DeleteAssetCommandResult>> Handle(DeleteAssetCommand command, CancellationToken cancellationToken)
  {
    var id = AssetId.Of(command.Id);
    var asset = await AssetRecordGuard.LoadAsync(context, id, track: true, cancellationToken);

    if (await context.AssetDisposals.AnyAsync(d => d.AssetId == id, cancellationToken))
      return Result<DeleteAssetCommandResult>.Failure("A disposed asset stays in the register as the record of its disposal, so it cannot be deleted.");

    var schedules = await context.AssetDepreciationSchedules.Include(s => s.Entries).Where(s => s.AssetId == id).ToListAsync(cancellationToken);
    var posted = schedules.SelectMany(s => s.Entries).Count(e => e.Posted && !e.IsReversed);
    if (posted > 0)
      return Result<DeleteAssetCommandResult>.Failure($"Depreciation is posted on this asset ({posted} {(posted == 1 ? "month" : "months")}), so it is part of the accounts and cannot be deleted. Dispose of it instead - or, if it was registered by mistake, reverse those months first.");

    if (await context.AssetAssignments.AnyAsync(a => a.AssetId == id && a.ExpectedReturnDate != null && a.ActualReturnDate == null, cancellationToken))
      return Result<DeleteAssetCommandResult>.Failure("This asset is out on loan. Record its return before deleting it.");

    // a running schedule with only draft months goes with it
    foreach (var schedule in schedules.Where(s => s.IsActive))
    {
      schedule.RemoveDraftsAfter(DateOnly.MinValue);
      schedule.Close(clock.Today.AddDays(1));
    }

    var actor = currentUser.Actor();
    asset.SoftDelete(actor.ShortName, command.Reason, clock.UtcNow);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetCommandResult>.Success(new DeleteAssetCommandResult(true));
  }
}
