using Microsoft.EntityFrameworkCore;

public class ChangeAssetStatusHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<ChangeAssetStatusCommand, Result<ChangeAssetStatusCommandResult>>
{
  public async Task<Result<ChangeAssetStatusCommandResult>> Handle(ChangeAssetStatusCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: true, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var fromStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);

    var toStatusId = AssetStatusId.Of(command.ToStatusId);
    var toStatus = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == toStatusId, cancellationToken)
      ?? throw new AssetStatusNotFoundException("The status was not found. Choose it again.");

    var eventDate = EventDates.Check(command.EventDate, clock, asset);
    asset.ChangeStatus(fromStatus, toStatus);

    var lifecycleEvent = await SystemEvents.AddAsync(context, assetId, EventPurpose.StatusChange, eventDate, currentUser.Actor(), command.Notes,
      fromStatusId: fromStatus.Id, toStatusId: toStatus.Id, cancellationToken: cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ChangeAssetStatusCommandResult>.Success(new ChangeAssetStatusCommandResult(lifecycleEvent.Id.Value));
  }
}

/// When something happened to an asset: given by the clerk (the minute they typed) or now. Never in the future, never
/// before the asset was registered unless it was registered with an older purchase date (a back-dated record).
public static class EventDates
{
  public static DateTime Check(DateTime? given, IClock clock, Asset asset, DateOnly? purchaseDate = null)
  {
    var now = clock.UtcNow;
    if (given is null)
      return now;

    var at = DateTime.SpecifyKind(given.Value, given.Value.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : given.Value.Kind).ToUniversalTime();
    if (at > now.AddMinutes(5))
      throw new DomainException("It cannot be dated in the future.");

    if (at.Year < 1900)
      throw new DomainException("The date is not a real date. Check the year.");

    // a record registered today for an asset bought years ago may log older events, but not before the purchase
    if (purchaseDate is { } bought && clock.DayOf(at) < bought)
      throw new DomainException($"It cannot be dated before the asset was acquired ({bought:dd'/'MM'/'yyyy}).");

    return at;
  }
}
