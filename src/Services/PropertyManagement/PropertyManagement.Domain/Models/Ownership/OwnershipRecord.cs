/// The ownership record of one property as it stands, which every ownership rule reads: all its ownership
/// rows (history included; the handler passes them tracked so rows can be closed), the owners the act
/// involves, the other transfers still waiting (INITIATED, or APPROVED before approval became completion),
/// the charges in force and today's date.
public sealed class OwnershipRecord
{
  private readonly Dictionary<OwnerId, PropertyOwner> _owners;

  public OwnershipRecord(
      Property property,
      IEnumerable<PropertyOwnership> ownerships,
      IEnumerable<PropertyOwner> owners,
      IEnumerable<PropertyTransfer> pendingTransfers,
      IEnumerable<PropertyEncumbrance> encumbrances,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(property);

    Property = property;
    Ownerships = ownerships.Where(o => o.PropertyId == property.Id).ToList();
    _owners = owners.GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());
    PendingTransfers = pendingTransfers.Where(t => t.PropertyId == property.Id && t.IsPending).ToList();
    ActiveEncumbrances = encumbrances.Where(e => e.PropertyId == property.Id && e.Status == EncumbranceStatus.Active).ToList();
    Today = today;
  }

  public Property Property { get; }
  public IReadOnlyList<PropertyOwnership> Ownerships { get; }
  public IReadOnlyList<PropertyTransfer> PendingTransfers { get; }
  public IReadOnlyList<PropertyEncumbrance> ActiveEncumbrances { get; }
  public DateOnly Today { get; }

  /// The shares held now (ACTIVE + DISPUTED).
  public IReadOnlyList<PropertyOwnership> Current => Ownerships.Where(o => o.IsCurrent).ToList();

  public PropertyOwner? Owner(OwnerId id) => _owners.GetValueOrDefault(id);

  public string NameOf(OwnerId id) => _owners.GetValueOrDefault(id)?.OwnerName.Value ?? "The owner";

  public IEnumerable<PropertyOwnership> RowsOf(OwnerId id) => Ownerships.Where(o => o.OwnerId == id);

  public IEnumerable<PropertyOwnership> CurrentRowsOf(OwnerId id) => Ownerships.Where(o => o.OwnerId == id && o.IsCurrent);

  /// A charge registered against this one share.
  public PropertyEncumbrance? ChargeOn(PropertyOwnership row) => ActiveEncumbrances.FirstOrDefault(e => e.OwnershipId == row.Id);

  /// A charge on the whole property (no ownership named).
  public PropertyEncumbrance? PropertyCharge => ActiveEncumbrances.FirstOrDefault(e => e.OwnershipId is null);

  /// Waiting transfers (other than `except`) in which this owner gives a share.
  public IEnumerable<PropertyTransfer> PendingTransfersGivenBy(OwnerId id, TransferId? except = null) =>
      PendingTransfers.Where(t => t.Id != except && t.Parties.Any(p => p.OwnerId == id && p.PartyRole == TransferPartyRole.Transferor));
}
