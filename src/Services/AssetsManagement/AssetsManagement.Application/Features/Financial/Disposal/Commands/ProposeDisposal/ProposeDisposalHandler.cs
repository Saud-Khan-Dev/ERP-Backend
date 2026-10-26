using Microsoft.EntityFrameworkCore;

public class ProposeDisposalHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<ProposeDisposalCommand, Result<ProposeDisposalCommandResult>>
{
  public async Task<Result<ProposeDisposalCommandResult>> Handle(ProposeDisposalCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    if (await DisposalChecks.WaitingAsync(context, assetId, cancellationToken) is { } waiting)
      return Result<ProposeDisposalCommandResult>.Failure(
        $"A disposal of this asset is already proposed ({DisposalChecks.ByWhom(waiting)}). It must be approved, rejected or withdrawn first.");

    if (await DisposalChecks.OnLoanAsync(context, assetId, cancellationToken))
      return Result<ProposeDisposalCommandResult>.Failure("This asset is out on loan. Record its return before proposing its disposal.");

    var (method, toStatus, currency) = await DisposalChecks.ChoicesAsync(context, command.DisposalMethodId, command.ToStatusId, command.CurrencyCode,
      command.DisposalValue, cancellationToken);

    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);
    if (await DisposalChecks.PostedAfterAsync(context, assetId, command.DisposalDate, cancellationToken) is { } posted)
      return Result<ProposeDisposalCommandResult>.Failure($"Depreciation is posted up to {posted:dd'/'MM'/'yyyy}, after the disposal date. Reverse those months first, or choose a later disposal date.");

    var disposal = AssetDisposal.Propose(
      AssetDisposalId.Of(Guid.NewGuid()), assetId, method, toStatus, command.DisposalDate, command.DisposalValue, currency,
      acquisition?.CurrencyCode, acquisition?.AcquisitionDate, command.BuyerInfo, command.Reason, currentUser.Actor(), clock.UtcNow, clock.Today);

    // the trail's "Added · Disposal" line (with every fact proposed) is written by the save
    await context.AssetDisposals.AddAsync(disposal, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ProposeDisposalCommandResult>.Success(new ProposeDisposalCommandResult(disposal.Id.Value));
  }
}

/// The checks a proposal and its approval share.
public static class DisposalChecks
{
  /// The proposal waiting on the asset, if there is one.
  public static Task<AssetDisposal?> WaitingAsync(IApplicationDbContext context, AssetId assetId, CancellationToken cancellationToken, bool track = false) =>
    (track ? context.AssetDisposals : context.AssetDisposals.AsNoTracking())
      .FirstOrDefaultAsync(d => d.AssetId == assetId && d.State == DisposalState.Proposed, cancellationToken);

  /// "by Ayesha Noor on 21 Oct 2026"
  public static string ByWhom(AssetDisposal disposal)
  {
    var parts = new[]
    {
      disposal.ProposedByName is { } name ? $"by {name}" : null,
      disposal.ProposedAt is { } at ? $"on {AssetAudit.Day(DateOnly.FromDateTime(at))}" : null
    }.Where(p => p is not null).ToList();
    return parts.Count == 0 ? "earlier" : string.Join(" ", parts);
  }

  public static Task<bool> OnLoanAsync(IApplicationDbContext context, AssetId assetId, CancellationToken cancellationToken) =>
    context.AssetAssignments.AnyAsync(a => a.AssetId == assetId && a.ExpectedReturnDate != null && a.ActualReturnDate == null, cancellationToken);

  /// The method, the final status and the currency of the proceeds - each found, or a 404 with a sentence.
  public static async Task<(DisposalMethod Method, AssetStatus ToStatus, Currency? Currency)> ChoicesAsync(IApplicationDbContext context,
      Guid methodId, Guid toStatusId, string? currencyCode, decimal? proceeds, CancellationToken cancellationToken)
  {
    var methodKey = DisposalMethodId.Of(methodId);
    var method = await context.DisposalMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodKey, cancellationToken)
      ?? throw new DisposalMethodNotFoundException("The disposal method was not found. Choose it again.");

    var statusKey = AssetStatusId.Of(toStatusId);
    var toStatus = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == statusKey, cancellationToken)
      ?? throw new AssetStatusNotFoundException("The final status was not found. Choose it again.");

    Currency? currency = null;
    if (currencyCode is not null && proceeds is not null)
    {
      currency = Currency.Of(currencyCode);
      if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
        throw new CurrencyNotFoundException($"The currency {currency.Value} was not found or is switched off.");
    }
    return (method, toStatus, currency);
  }

  /// The last day of posted depreciation when it is after the disposal date (months of an asset GDA no longer had).
  public static async Task<DateOnly?> PostedAfterAsync(IApplicationDbContext context, AssetId assetId, DateOnly disposalDate, CancellationToken cancellationToken)
  {
    var schedules = await context.AssetDepreciationSchedules.AsNoTracking().Include(s => s.Entries).Where(s => s.AssetId == assetId).ToListAsync(cancellationToken);
    return AssetBookValue.PostedThrough(schedules) is { } posted && posted > disposalDate ? posted : null;
  }
}
