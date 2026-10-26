/// The single people-and-organizations table: one row per person, company, trust or government body,
/// whether they act as owner, allottee, lessee, tenant, bidder or contractor.
///
/// Contacts and addresses belong to the aggregate so "at most one primary of each" (schema guide,
/// rule 3) is enforced in one place.
///
/// What an owner must carry depends on the owner type (by system code): an INDIVIDUAL needs a CNIC; a COMPANY,
/// TRUST, GOVERNMENT body or JOINT_ENTITY is identified by its NTN and has neither a CNIC nor a father / husband
/// name. Owner types an administrator adds have no such rule.
///
/// Old records: an individual who never had a CNIC (owners of the 1960s-1990s) is entered with the identity they had
/// instead - an old NIC, a passport, or "no ID on record" - and may be marked deceased. Such a person can appear in a
/// property's history, but a share registered today needs a living holder with a CNIC (CanHoldToday); a deceased
/// holder is allowed, since an estate is often still in the dead owner's name until it is transferred.
public class PropertyOwner : Aggregate<OwnerId>
{
  public const int NameMaxLength = 200;
  public const int FatherHusbandNameMaxLength = 200;
  public const int NtnMaxLength = global::Ntn.MaxLength;
  public const int RemarksMaxLength = 4000;
  public const int OldIdNoMaxLength = 30;

  /// An individual's identity from before CNICs, as the old papers give it.
  public sealed record OldIdentity(OldIdType Type, string? Number);

  /// The organization owner types, with how a message names them ("A company has no CNIC ...").
  private static readonly Dictionary<string, string> Organizations = new()
  {
    [SystemMasterCodes.OwnerTypeCompany] = "A company",
    [SystemMasterCodes.OwnerTypeTrust] = "A trust",
    [SystemMasterCodes.OwnerTypeGovernment] = "A government body",
    [SystemMasterCodes.OwnerTypeJointEntity] = "A joint entity",
  };

  private readonly List<OwnerContact> _contacts = new();
  private readonly List<OwnerAddress> _addresses = new();

  /// OWN-00001, issued by the OWNER code sequence.
  public BusinessCode OwnerCode { get; private set; } = default!;
  public MasterId OwnerTypeId { get; private set; } = default!;
  public Name OwnerName { get; private set; } = default!;
  public string? FatherHusbandName { get; private set; }
  /// Individuals.
  public Cnic? Cnic { get; private set; }
  /// Organizations (and individuals who have one): 1234567-8 or 12345-1234567-1 (see Ntn).
  public string? Ntn { get; private set; }
  public EmailAddress? Email { get; private set; }
  /// The owner's current CNIC copy: the latest active, latest-version CNIC_COPY document filed on this owner
  /// (kept by RefreshCnicDocument).
  public DocumentId? CnicDocumentId { get; private set; }
  /// Individuals without a CNIC (old records): the identity they had instead. Null for everyone else.
  public OldIdType? OldIdType { get; private set; }
  /// The old NIC or passport number (none for NotRecorded).
  public string? OldIdNo { get; private set; }
  /// The person has died (individuals only); the date when it is known.
  public bool IsDeceased { get; private set; }
  public DateOnly? DateOfDeath { get; private set; }
  public string? Remarks { get; private set; }
  /// On the register (not whether the person is alive - see IsDeceased).
  public bool IsActive { get; private set; }

  public IReadOnlyList<OwnerContact> Contacts => _contacts.AsReadOnly();
  public IReadOnlyList<OwnerAddress> Addresses => _addresses.AsReadOnly();

  public static PropertyOwner Create(
      OwnerId id,
      BusinessCode ownerCode,
      OwnerType ownerType,
      Name ownerName,
      string? fatherHusbandName,
      Cnic? cnic,
      Ntn? ntn,
      EmailAddress? email,
      string? remarks,
      OldIdentity? oldIdentity = null,
      bool isDeceased = false,
      DateOnly? dateOfDeath = null,
      DateOnly? today = null)
  {
    ArgumentNullException.ThrowIfNull(ownerCode);

    var owner = new PropertyOwner { Id = id, OwnerCode = ownerCode, IsActive = true };
    owner.Apply(ownerType, ownerName, fatherHusbandName, cnic, ntn, email, remarks, oldIdentity, isDeceased, dateOfDeath, today);
    return owner;
  }

  public void Update(
      OwnerType ownerType,
      Name ownerName,
      string? fatherHusbandName,
      Cnic? cnic,
      Ntn? ntn,
      EmailAddress? email,
      string? remarks,
      OldIdentity? oldIdentity = null,
      bool isDeceased = false,
      DateOnly? dateOfDeath = null,
      DateOnly? today = null)
  {
    EnsureActive();
    Apply(ownerType, ownerName, fatherHusbandName, cnic, ntn, email, remarks, oldIdentity, isDeceased, dateOfDeath, today);
  }

  /// May a share be registered in this person's name today (as a first owner, or as the receiver of a transfer)?
  /// An individual needs a CNIC - unless they have died: an estate often stays in the dead owner's name until it is
  /// transferred. People known only from the old records (old NIC, passport, no ID) appear in a property's history.
  public bool CanHoldToday => Cnic is not null || IsDeceased || OldIdType is null;

  public void EnsureCanHoldToday()
  {
    if (!CanHoldToday)
      throw new DomainException(
        $"{OwnerName.Value} ({OwnerCode.Value}) has no CNIC on record - only {OldIdLabel(OldIdType)}. A share registered "
        + "today needs a CNIC: add it on the owner's record first. People from the old records belong in the property's history.");
  }

  /// "an old NIC", "a passport", "no ID".
  private static string OldIdLabel(OldIdType? type) => type switch
  {
    global::OldIdType.OldNic => "an old NIC",
    global::OldIdType.Passport => "a passport",
    _ => "no ID",
  };

  /// Points cnic_document_id at the owner's current CNIC copy: of the CNIC_COPY documents filed on this owner,
  /// the active ones that no later version supersedes, the most recently uploaded. None left = no pointer.
  /// Run after every upload, new version, (de)activation or re-type of one of the owner's documents.
  public void RefreshCnicDocument(IEnumerable<PropertyDocument> ownerDocuments, DocumentType cnicCopy)
  {
    ArgumentNullException.ThrowIfNull(ownerDocuments);
    ArgumentNullException.ThrowIfNull(cnicCopy);
    cnicCopy.EnsureIs(SystemMasterCodes.DocumentCnicCopy);

    var documents = ownerDocuments
        .Where(d => d.EntityType == DocumentEntityType.Owner && d.EntityId == Id.Value)
        .DistinctBy(d => d.Id)
        .ToList();

    var superseded = documents.Where(d => d.SupersedesDocumentId is not null).Select(d => d.SupersedesDocumentId!).ToHashSet();

    CnicDocumentId = documents
        .Where(d => d.IsActive && d.DocumentTypeId == cnicCopy.Id && !superseded.Contains(d.Id))
        .OrderByDescending(d => d.UploadedAt)
        .ThenByDescending(d => d.VersionNo)
        .Select(d => d.Id)
        .FirstOrDefault();
  }

  /// What still ties an owner to the register, each as a short label ("PROP-00007 (40%)", "TRF-00041", "LSE-00005").
  /// Shares: active or disputed shares on active properties. Pending transfers: initiated or approved, not yet
  /// registered. Agreements: allotments, leases, rentals and contracts in force (or a lease still in draft).
  public sealed record Commitments(IReadOnlyList<string> Shares, IReadOnlyList<string> PendingTransfers, IReadOnlyList<string> Agreements)
  {
    public static Commitments None { get; } = new([], [], []);

    public bool Any => Shares.Count > 0 || PendingTransfers.Count > 0 || Agreements.Count > 0;
  }

  /// An owner leaves the registry only once nothing ties them to a property any more; the message lists what does.
  public void Deactivate(Commitments commitments)
  {
    ArgumentNullException.ThrowIfNull(commitments);

    if (commitments.Any)
    {
      var reasons = new List<string>();
      if (commitments.Shares.Count > 0)
        reasons.Add($"holds a share in {string.Join(", ", commitments.Shares)}");
      if (commitments.PendingTransfers.Count > 0)
        reasons.Add($"is a party to pending transfer {string.Join(", ", commitments.PendingTransfers)}");
      if (commitments.Agreements.Count > 0)
        reasons.Add($"holds {string.Join(", ", commitments.Agreements)}");

      throw new DomainException(
        $"{OwnerCode.Value} ({OwnerName.Value}) cannot be deactivated while the owner {string.Join("; ", reasons)}. "
        + "End, transfer or cancel these first.");
    }

    IsActive = false;
  }

  public void Activate() => IsActive = true;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Owner {OwnerCode.Value} is inactive.");
  }

  // =====================================================
  // CONTACTS
  // =====================================================

  /// The first active contact becomes primary automatically.
  public OwnerContact AddContact(ContactType contactType, string contactNumber, bool isPrimary, string? remarks)
  {
    ArgumentNullException.ThrowIfNull(contactType);
    EnsureActive();
    contactType.EnsureActive();

    var makePrimary = isPrimary || !_contacts.Any(c => c.IsActive);
    if (makePrimary)
      _contacts.ForEach(c => c.SetPrimary(false));

    var contact = OwnerContact.Create(OwnerContactId.New(), Id, contactType.Id, contactNumber, makePrimary, remarks);
    _contacts.Add(contact);
    return contact;
  }

  public void UpdateContact(OwnerContactId contactId, ContactType contactType, string contactNumber, bool isPrimary, string? remarks)
  {
    ArgumentNullException.ThrowIfNull(contactType);
    EnsureActive();

    var contact = FindContact(contactId);
    if (contact.ContactTypeId != contactType.Id)
      contactType.EnsureActive();

    if (isPrimary)
      _contacts.Where(c => c.Id != contactId).ToList().ForEach(c => c.SetPrimary(false));

    contact.Update(contactType.Id, contactNumber, isPrimary, remarks);
    KeepOnePrimaryContact();
  }

  /// Deactivating the primary contact promotes the next active one, so an owner keeps a primary.
  public void DeactivateContact(OwnerContactId contactId)
  {
    var contact = FindContact(contactId);
    contact.Deactivate();
    KeepOnePrimaryContact();
  }

  // =====================================================
  // ADDRESSES
  // =====================================================

  public OwnerAddress AddAddress(
      AddressType addressType,
      string fullAddress,
      string? cityTown,
      string? district,
      string? province,
      string? country,
      string? postalCode,
      bool isPrimary)
  {
    EnsureActive();

    var makePrimary = isPrimary || !_addresses.Any(a => a.IsActive);
    if (makePrimary)
      _addresses.ForEach(a => a.SetPrimary(false));

    var address = OwnerAddress.Create(OwnerAddressId.New(), Id, addressType, fullAddress, cityTown, district, province, country, postalCode, makePrimary);
    _addresses.Add(address);
    return address;
  }

  public void UpdateAddress(
      OwnerAddressId addressId,
      AddressType addressType,
      string fullAddress,
      string? cityTown,
      string? district,
      string? province,
      string? country,
      string? postalCode,
      bool isPrimary)
  {
    EnsureActive();

    var address = FindAddress(addressId);
    if (isPrimary)
      _addresses.Where(a => a.Id != addressId).ToList().ForEach(a => a.SetPrimary(false));

    address.Update(addressType, fullAddress, cityTown, district, province, country, postalCode, isPrimary);
    KeepOnePrimaryAddress();
  }

  public void DeactivateAddress(OwnerAddressId addressId)
  {
    var address = FindAddress(addressId);
    address.Deactivate();
    KeepOnePrimaryAddress();
  }

  /// While an owner has active contacts, exactly one of them is primary: un-ticking or deactivating
  /// the primary hands the flag to the next active contact.
  private void KeepOnePrimaryContact()
  {
    if (!_contacts.Any(c => c.IsPrimary))
      _contacts.FirstOrDefault(c => c.IsActive)?.SetPrimary(true);
  }

  private void KeepOnePrimaryAddress()
  {
    if (!_addresses.Any(a => a.IsPrimary))
      _addresses.FirstOrDefault(a => a.IsActive)?.SetPrimary(true);
  }

  private OwnerContact FindContact(OwnerContactId contactId) =>
      _contacts.FirstOrDefault(c => c.Id == contactId)
      ?? throw new DomainException($"Contact {contactId.Value} does not belong to owner {OwnerCode.Value}.");

  private OwnerAddress FindAddress(OwnerAddressId addressId) =>
      _addresses.FirstOrDefault(a => a.Id == addressId)
      ?? throw new DomainException($"Address {addressId.Value} does not belong to owner {OwnerCode.Value}.");

  private void Apply(
      OwnerType ownerType,
      Name ownerName,
      string? fatherHusbandName,
      Cnic? cnic,
      Ntn? ntn,
      EmailAddress? email,
      string? remarks,
      OldIdentity? oldIdentity,
      bool isDeceased,
      DateOnly? dateOfDeath,
      DateOnly? today)
  {
    ArgumentNullException.ThrowIfNull(ownerType);
    ArgumentNullException.ThrowIfNull(ownerName);

    if (ownerType.Id != OwnerTypeId)
      ownerType.EnsureActive();

    if (ownerName.Value.Length > NameMaxLength)
      throw new DomainException($"Owner name cannot exceed {NameMaxLength} characters.");

    var fatherHusband = Guard.Text(fatherHusbandName, FatherHusbandNameMaxLength, "Father / husband name");
    var individual = ownerType.Is(SystemMasterCodes.OwnerTypeIndividual);

    // an old identity stands in for the CNIC of someone from the old records; with a CNIC it is not needed
    var oldId = cnic is null ? oldIdentity : null;
    var oldIdNo = oldId is null ? null : Guard.Text(oldId.Number, OldIdNoMaxLength, "Old ID number");
    if (oldId is not null)
    {
      if (!individual)
        throw new DomainException("Only a person is entered with an old NIC, a passport or \"no ID on record\"; an organization is identified by its NTN.");
      if (oldId.Type == global::OldIdType.NotRecorded && oldIdNo is not null)
        throw new DomainException("\"No ID on record\" takes no number - leave the number empty, or choose old NIC or passport.");
      if (oldId.Type != global::OldIdType.NotRecorded && oldIdNo is null)
        throw new DomainException($"Give the {(oldId.Type == global::OldIdType.OldNic ? "old NIC" : "passport")} number as it is written on the old papers.");
    }

    if (individual && cnic is null && oldId is null)
      throw new DomainException(
        "An individual needs a CNIC (13 digits, e.g. 12345-1234567-1). For someone in the old records who never had one, "
        + "choose the old NIC or passport they had, or \"No ID on record\".");

    if (Organizations.TryGetValue(ownerType.Code.Value, out var organization))
    {
      if (cnic is not null)
        throw new DomainException($"{organization} has no CNIC - use its NTN.");

      if (fatherHusband is not null)
        throw new DomainException($"{organization} has no father / husband name - leave it empty.");
    }

    if (!isDeceased && dateOfDeath is not null)
      throw new DomainException("A date of death is given only for someone marked as deceased.");
    if (isDeceased && !individual)
      throw new DomainException("Only a person can be marked as deceased.");
    if (dateOfDeath is { } died && today is { } day && died > day)
      throw new DomainException($"The date of death cannot be in the future ({died:yyyy-MM-dd}); today is {day:yyyy-MM-dd}.");

    OwnerTypeId = ownerType.Id;
    OwnerName = ownerName;
    FatherHusbandName = fatherHusband;
    Cnic = cnic;
    Ntn = ntn?.Value;
    Email = email;
    Remarks = Guard.Text(remarks, RemarksMaxLength, "Remarks");
    OldIdType = oldId?.Type;
    OldIdNo = oldIdNo;
    IsDeceased = isDeceased;
    DateOfDeath = dateOfDeath;
  }
}
