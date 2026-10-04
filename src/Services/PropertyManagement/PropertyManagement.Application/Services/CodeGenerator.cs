using Microsoft.EntityFrameworkCore;

/// Issues business codes (PROP-00001, OWN-00001, TRF-00001) from the admin-editable code sequences
/// (schema guide, rule 8 — users never type them).
///
/// Nothing is saved here: the counter moves on the tracked sequence and is committed with the new
/// record. Two concurrent issues collide on the sequence's row version, so the loser gets a 409 rather
/// than a duplicate code; the unique index on each code column is the last line of defence.
public class CodeGenerator(IApplicationDbContext context)
{
  private const int MaxIssueAttempts = 100;

  public async Task<CodeSequence> GetSequenceAsync(string key, CancellationToken cancellationToken)
  {
    var sequenceKey = MasterCode.Of(key);

    var sequence = await context.CodeSequences.FirstOrDefaultAsync(s => s.Key == sequenceKey, cancellationToken);
    if (sequence is not null)
      return sequence;

    var defaults = CodeSequenceKeys.Defaults.FirstOrDefault(d => d.Key == sequenceKey.Value)
      ?? throw new CodeSequenceNotFoundException($"There is no code sequence '{sequenceKey.Value}'.");

    sequence = CodeSequence.Create(CodeSequenceId.New(), sequenceKey, defaults.Prefix, defaults.Separator, defaults.MinimumDigits, 1);
    await context.CodeSequences.AddAsync(sequence, cancellationToken);
    return sequence;
  }

  public async Task<BusinessCode> NextAsync(string key, CancellationToken cancellationToken)
  {
    var sequence = await GetSequenceAsync(key, cancellationToken);

    // skip anything already taken, e.g. after an admin moved the counter back
    for (var attempt = 0; attempt < MaxIssueAttempts; attempt++)
    {
      var code = sequence.Issue();

      if (!await IsTakenAsync(key, code, cancellationToken))
        return code;
    }

    throw new DomainException($"Could not find a free {key} code after {MaxIssueAttempts} attempts. Raise the sequence's next number.");
  }

  /// The code NextAsync would issue now, without moving the counter or saving anything (a preview, not a
  /// reservation). It skips codes already taken exactly as NextAsync does.
  public async Task<BusinessCode> PreviewAsync(string key, CancellationToken cancellationToken)
  {
    var sequenceKey = MasterCode.Of(key);

    var sequence = await context.CodeSequences.AsNoTracking().FirstOrDefaultAsync(s => s.Key == sequenceKey, cancellationToken);
    if (sequence is null)
    {
      var defaults = CodeSequenceKeys.Defaults.FirstOrDefault(d => d.Key == sequenceKey.Value)
        ?? throw new CodeSequenceNotFoundException($"There is no code sequence '{sequenceKey.Value}'.");
      sequence = CodeSequence.Create(CodeSequenceId.New(), sequenceKey, defaults.Prefix, defaults.Separator, defaults.MinimumDigits, 1);
    }

    for (long number = sequence.NextNumber, attempt = 0; attempt < MaxIssueAttempts && number <= CodeSequence.MaxNumber; number++, attempt++)
    {
      var code = sequence.Format(number);

      if (!await IsTakenAsync(key, code, cancellationToken))
        return code;
    }

    throw new DomainException($"Could not find a free {key} code after {MaxIssueAttempts} attempts. Raise the sequence's next number.");
  }

  /// Every code already issued for this key, so a sequence edit can be checked against them.
  public async Task<IReadOnlyList<BusinessCode>> UsedCodesAsync(string key, CancellationToken cancellationToken) => key switch
  {
    CodeSequenceKeys.Property => await context.Properties.AsNoTracking().Select(p => p.PropertyCode).ToListAsync(cancellationToken),
    CodeSequenceKeys.Owner => await context.Owners.AsNoTracking().Select(o => o.OwnerCode).ToListAsync(cancellationToken),
    CodeSequenceKeys.Transfer => await context.Transfers.AsNoTracking().Select(t => t.TransferNo).ToListAsync(cancellationToken),
    CodeSequenceKeys.Allotment => await context.Allotments.AsNoTracking().Select(x => x.AllotmentNo).ToListAsync(cancellationToken),
    CodeSequenceKeys.Lease => await context.Leases.AsNoTracking().Select(x => x.LeaseNo).ToListAsync(cancellationToken),
    CodeSequenceKeys.Rental => await context.Rentals.AsNoTracking().Select(x => x.RentalNo).ToListAsync(cancellationToken),
    CodeSequenceKeys.Auction => await context.Auctions.AsNoTracking().Select(x => x.AuctionNo).ToListAsync(cancellationToken),
    CodeSequenceKeys.Contract => await context.Outsourcings.AsNoTracking().Select(x => x.ContractNo).ToListAsync(cancellationToken),
    CodeSequenceKeys.Encroachment => await context.Encroachments.AsNoTracking().Select(x => x.EncroachmentNo).ToListAsync(cancellationToken),
    CodeSequenceKeys.Appeal => await context.Appeals.AsNoTracking().Select(x => x.AppealNo).ToListAsync(cancellationToken),
    CodeSequenceKeys.BuildingPlan => (await context.BuildingPlans.AsNoTracking().Select(x => x.PlanNo).ToListAsync(cancellationToken)).Distinct().ToList(),
    _ => Array.Empty<BusinessCode>()
  };

  private Task<bool> IsTakenAsync(string key, BusinessCode code, CancellationToken cancellationToken) => key switch
  {
    CodeSequenceKeys.Property => context.Properties.AnyAsync(p => p.PropertyCode == code, cancellationToken),
    CodeSequenceKeys.Owner => context.Owners.AnyAsync(o => o.OwnerCode == code, cancellationToken),
    CodeSequenceKeys.Transfer => context.Transfers.AnyAsync(t => t.TransferNo == code, cancellationToken),
    CodeSequenceKeys.Allotment => context.Allotments.AnyAsync(x => x.AllotmentNo == code, cancellationToken),
    CodeSequenceKeys.Lease => context.Leases.AnyAsync(x => x.LeaseNo == code, cancellationToken),
    CodeSequenceKeys.Rental => context.Rentals.AnyAsync(x => x.RentalNo == code, cancellationToken),
    CodeSequenceKeys.Auction => context.Auctions.AnyAsync(x => x.AuctionNo == code, cancellationToken),
    CodeSequenceKeys.Contract => context.Outsourcings.AnyAsync(x => x.ContractNo == code, cancellationToken),
    CodeSequenceKeys.Encroachment => context.Encroachments.AnyAsync(x => x.EncroachmentNo == code, cancellationToken),
    CodeSequenceKeys.Appeal => context.Appeals.AnyAsync(x => x.AppealNo == code, cancellationToken),
    CodeSequenceKeys.BuildingPlan => context.BuildingPlans.AnyAsync(x => x.PlanNo == code, cancellationToken),
    _ => Task.FromResult(false)
  };
}
