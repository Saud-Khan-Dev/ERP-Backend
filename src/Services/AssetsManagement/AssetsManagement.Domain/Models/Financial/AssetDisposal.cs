/// How an asset leaves GDA, in two steps: a clerk proposes it (the method, the date, the proceeds, the final status)
/// and another person approves it - only then is depreciation posted up to the date, the book value on that day kept,
/// the gain or loss worked out ((proceeds, or nothing) minus that book value) and the asset closed. A proposal can be
/// rejected by an approver or withdrawn by its proposer. An approved disposal recorded by mistake is cancelled, and the
/// row stays as the record of it. At most one live disposal (proposed or approved) per asset: a partial unique index.
public class AssetDisposal : Aggregate<AssetDisposalId>
{
  public AssetId AssetId { get; private set; } = default!;
  public DisposalState State { get; private set; }
  public DateOnly DisposalDate { get; private set; }
  public DisposalMethodId DisposalMethodId { get; private set; } = default!;
  /// The final status the asset takes when the disposal is approved (null on disposals recorded before 2026-10-21).
  public AssetStatusId? ToStatusId { get; private set; }
  public decimal? DisposalValue { get; private set; }
  public Currency? CurrencyCode { get; private set; }
  /// Snapshot on approval, to compute gain/loss.
  public decimal? NetBookValueAtDisposal { get; private set; }
  public decimal? GainLoss { get; private set; }
  public string? BuyerInfo { get; private set; }
  public string? Reason { get; private set; }

  /// Who proposed it (ASSET_FINANCE.CREATE) and when.
  public Guid? ProposedBy { get; private set; }
  public string? ProposedByName { get; private set; }
  public DateTime? ProposedAt { get; private set; }

  /// Who approved it (ASSET_FINANCE.APPROVE) and when - another person than the proposer while the rule
  /// "Disposals pass through two people" is on.
  public Guid? ApprovedBy { get; private set; }
  public string? ApprovedByName { get; private set; }
  public DateTime? ApprovedAt { get; private set; }

  /// Who rejected or withdrew the proposal, when and why.
  public Guid? DecidedBy { get; private set; }
  public string? DecidedByName { get; private set; }
  public DateTime? DecidedAt { get; private set; }
  public string? DecisionNote { get; private set; }

  /// Who cancelled an approved disposal (recorded by mistake), when and why.
  public Guid? CancelledBy { get; private set; }
  public string? CancelledByName { get; private set; }
  public DateTime? CancelledAt { get; private set; }
  public string? CancellationReason { get; private set; }

  public bool IsLive => State is DisposalState.Proposed or DisposalState.Approved;

  /// Step one: the proposal. Everything a person can check before the approval is checked now; the book value and the
  /// gain or loss are worked out when it is approved.
  public static AssetDisposal Propose(
      AssetDisposalId id,
      AssetId assetId,
      DisposalMethod method,
      AssetStatus toStatus,
      DateOnly disposalDate,
      decimal? disposalValue,
      Currency? currencyCode,
      Currency? purchaseCurrency,
      DateOnly? purchaseDate,
      string? buyerInfo,
      string? reason,
      ActorRef proposedBy,
      DateTime now,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(method);
    ArgumentNullException.ThrowIfNull(toStatus);
    ArgumentNullException.ThrowIfNull(proposedBy);

    CheckFinalStatus(toStatus);
    Check(method, disposalDate, disposalValue, currencyCode, purchaseCurrency, purchaseDate, today);

    return new AssetDisposal
    {
      Id = id,
      AssetId = assetId,
      State = DisposalState.Proposed,
      DisposalMethodId = method.Id,
      ToStatusId = toStatus.Id,
      DisposalDate = disposalDate,
      DisposalValue = disposalValue,
      CurrencyCode = disposalValue is null ? null : currencyCode,
      BuyerInfo = string.IsNullOrWhiteSpace(buyerInfo) ? null : buyerInfo.Trim(),
      Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
      ProposedBy = proposedBy.Id,
      ProposedByName = proposedBy.ShortName,
      ProposedAt = now
    };
  }

  /// Step two: the approval. The method and the final status are checked again (either may have been switched off in
  /// the meantime); the book value on the disposal date is kept and the gain or loss worked out against it.
  public void Approve(DisposalMethod method, AssetStatus toStatus, decimal? netBookValueAtDisposal, Currency? purchaseCurrency,
      DateOnly? purchaseDate, ActorRef approvedBy, DateTime now, DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(method);
    ArgumentNullException.ThrowIfNull(toStatus);
    ArgumentNullException.ThrowIfNull(approvedBy);
    EnsureProposed("approved");

    if (method.Id != DisposalMethodId)
      throw new DomainException("The disposal method is not the one proposed.");

    CheckFinalStatus(toStatus);
    Check(method, DisposalDate, DisposalValue, CurrencyCode, purchaseCurrency, purchaseDate, today);

    State = DisposalState.Approved;
    ToStatusId = toStatus.Id;
    NetBookValueAtDisposal = netBookValueAtDisposal;
    // a write-off with no proceeds loses the whole book value
    GainLoss = netBookValueAtDisposal.HasValue ? (DisposalValue ?? 0m) - netBookValueAtDisposal.Value : null;
    ApprovedBy = approvedBy.Id;
    ApprovedByName = approvedBy.ShortName;
    ApprovedAt = now;
  }

  /// An approver turns the proposal down, saying why. The asset stays as it is.
  public void Reject(ActorRef rejectedBy, string reason, DateTime now)
  {
    ArgumentNullException.ThrowIfNull(rejectedBy);
    EnsureProposed("rejected");
    if (string.IsNullOrWhiteSpace(reason))
      throw new DomainException("Say why the proposal is rejected.");

    State = DisposalState.Rejected;
    Decide(rejectedBy, reason, now);
  }

  /// The person who proposed it takes the proposal back.
  public void Withdraw(ActorRef withdrawnBy, string? note, DateTime now)
  {
    ArgumentNullException.ThrowIfNull(withdrawnBy);
    EnsureProposed("withdrawn");
    if (ProposedBy is { } proposer && withdrawnBy.Id != proposer)
      throw new DomainException($"Only {ProposedByName ?? "the person who proposed it"} can withdraw this proposal. An approver can reject it.");

    State = DisposalState.Withdrawn;
    Decide(withdrawnBy, note, now);
  }

  /// An approved disposal recorded by mistake (the wrong asset, the wrong date) is cancelled, with the reason. The row
  /// stays - with its proceeds, book value, gain or loss and who approved it.
  public void Cancel(ActorRef cancelledBy, string reason, DateTime now)
  {
    ArgumentNullException.ThrowIfNull(cancelledBy);
    if (State != DisposalState.Approved)
      throw new DomainException("Only an approved disposal can be cancelled. A proposal is rejected or withdrawn instead.");
    if (string.IsNullOrWhiteSpace(reason))
      throw new DomainException("Say why the disposal is cancelled.");

    State = DisposalState.Cancelled;
    CancelledBy = cancelledBy.Id;
    CancelledByName = cancelledBy.ShortName;
    CancelledAt = now;
    CancellationReason = reason.Trim();
  }

  private void Decide(ActorRef actor, string? note, DateTime now)
  {
    DecidedBy = actor.Id;
    DecidedByName = actor.ShortName;
    DecidedAt = now;
    DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
  }

  private void EnsureProposed(string act)
  {
    if (State != DisposalState.Proposed)
      throw new DomainException($"This disposal is {State.ToString().ToLowerInvariant()}, so it cannot be {act}.");
  }

  private static void CheckFinalStatus(AssetStatus toStatus)
  {
    if (!toStatus.IsTerminal)
      throw new DomainException($"'{toStatus.Name.Value}' is not a final status. A disposal must end in a final status (e.g. Disposed).");

    if (!toStatus.IsActive)
      throw new DomainException($"The status '{toStatus.Name.Value}' is switched off. Choose another final status.");
  }

  private static void Check(DisposalMethod method, DateOnly disposalDate, decimal? disposalValue, Currency? currencyCode,
      Currency? purchaseCurrency, DateOnly? purchaseDate, DateOnly today)
  {
    if (!method.IsActive)
      throw new DomainException($"The disposal method '{method.Name.Value}' is switched off.");

    if (disposalDate < AssetAcquisition.EarliestDate)
      throw new DomainException("The disposal date is not a real date. Check the year.");

    if (disposalDate > today)
      throw new DomainException("The disposal date cannot be in the future.");

    if (purchaseDate.HasValue && disposalDate < purchaseDate.Value)
      throw new DomainException($"The disposal date cannot be before the purchase date ({purchaseDate.Value:dd'/'MM'/'yyyy}).");

    if (method.RequiresValue && disposalValue is null)
      throw new DomainException($"'{method.Name.Value}' records what was received: enter the proceeds.");

    if (!method.AllowsValue && disposalValue is not null)
      throw new DomainException($"'{method.Name.Value}' records no proceeds. Leave the proceeds empty, or choose another method.");

    if (disposalValue is < 0)
      throw new DomainException("The proceeds cannot be negative.");

    if (disposalValue is not null && currencyCode is null)
      throw new DomainException("Choose the currency of the proceeds.");

    if (disposalValue is not null && purchaseCurrency is not null && currencyCode != purchaseCurrency)
      throw new DomainException($"Record the proceeds in {purchaseCurrency.Value}, the currency the asset was bought in - the gain or loss is worked out against its book value.");
  }
}
