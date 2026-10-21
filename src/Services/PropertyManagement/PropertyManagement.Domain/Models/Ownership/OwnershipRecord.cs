/// The ownership record of one property as it stands, which every ownership rule reads: all its ownership
/// rows (history included; the handler passes them tracked so rows can be closed), the owners the act
/// involves, the other transfers still waiting (INITIATED, or APPROVED before approval became completion),
/// the charges that hold shares in place, the allotment in force (if any) and today's date.
///
/// A charge holds a share in place while it is ACTIVE, and also once it is ENFORCED (the holder foreclosed):
/// the share then moves only to the charge holder, and that transfer satisfies the charge.
public sealed class OwnershipRecord
{
  private readonly Dictionary<OwnerId, PropertyOwner> _owners;

  public OwnershipRecord(
      Property property,
      IEnumerable<PropertyOwnership> ownerships,
      IEnumerable<PropertyOwner> owners,
      IEnumerable<PropertyTransfer> pendingTransfers,
      IEnumerable<PropertyEncumbrance> encumbrances,
      DateOnly today,
      PropertyAllotment.InForce? allotmentInForce = null)
  {
    ArgumentNullException.ThrowIfNull(property);

    Property = property;
    Ownerships = ownerships.Where(o => o.PropertyId == property.Id).ToList();
    _owners = owners.GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());
    PendingTransfers = pendingTransfers.Where(t => t.PropertyId == property.Id && t.IsPending).ToList();
    Charges = encumbrances.Where(e => e.PropertyId == property.Id && e.HoldsShares).ToList();
    Today = today;
    AllotmentInForce = allotmentInForce is not null && allotmentInForce.Allotment.PropertyId == property.Id ? allotmentInForce : null;
  }

  public Property Property { get; }
  public IReadOnlyList<PropertyOwnership> Ownerships { get; }
  public IReadOnlyList<PropertyTransfer> PendingTransfers { get; }
  /// The charges that hold shares in place: ACTIVE ones and ENFORCED ones (see the class comment).
  public IReadOnlyList<PropertyEncumbrance> Charges { get; }
  public DateOnly Today { get; }
  /// The allotment in force on the plot (ACTIVE or RESTORED), with its allottee's name; null when there is none.
  public PropertyAllotment.InForce? AllotmentInForce { get; }

  /// The ACTIVE charges only (an enforced charge is listed in Charges too).
  public IReadOnlyList<PropertyEncumbrance> ActiveEncumbrances => Charges.Where(e => e.Status == EncumbranceStatus.Active).ToList();

  /// The shares held now (ACTIVE + DISPUTED).
  public IReadOnlyList<PropertyOwnership> Current => Ownerships.Where(o => o.IsCurrent).ToList();

  /// Anyone ever held a share of this property (rows of any status). A property with no holder history yet takes its
  /// first owners as "entered from the register"; after that, owners change only by approval.
  public bool HasHolderHistory => Ownerships.Count > 0;

  public PropertyOwner? Owner(OwnerId id) => _owners.GetValueOrDefault(id);

  public string NameOf(OwnerId id) => _owners.GetValueOrDefault(id)?.OwnerName.Value ?? "The owner";

  public IEnumerable<PropertyOwnership> RowsOf(OwnerId id) => Ownerships.Where(o => o.OwnerId == id);

  public IEnumerable<PropertyOwnership> CurrentRowsOf(OwnerId id) => Ownerships.Where(o => o.OwnerId == id && o.IsCurrent);

  /// A charge (active or enforced) registered against this one share.
  public PropertyEncumbrance? ChargeOn(PropertyOwnership row) => Charges.FirstOrDefault(e => e.OwnershipId == row.Id);

  /// A charge (active or enforced) on the whole property (no ownership named).
  public PropertyEncumbrance? PropertyCharge => Charges.FirstOrDefault(e => e.OwnershipId is null);

  /// Waiting transfers (other than `except`) in which this owner gives a share.
  public IEnumerable<PropertyTransfer> PendingTransfersGivenBy(OwnerId id, TransferId? except = null) =>
      PendingTransfers.Where(t => t.Id != except && t.Parties.Any(p => p.OwnerId == id && p.PartyRole == TransferPartyRole.Transferor));
}
