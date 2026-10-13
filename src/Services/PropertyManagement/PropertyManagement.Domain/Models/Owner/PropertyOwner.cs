/// The single people-and-organizations table: one row per person, company, trust or government body,
/// whether they act as owner, allottee, lessee, tenant, bidder or contractor.
///
/// Contacts and addresses belong to the aggregate so "at most one primary of each" (schema guide,
/// rule 3) is enforced in one place.
///
/// What an owner must carry depends on the owner type (by system code): an INDIVIDUAL needs a CNIC; a COMPANY,
/// TRUST, GOVERNMENT body or JOINT_ENTITY is identified by its NTN and has neither a CNIC nor a father / husband
/// name. Owner types an administrator adds have no such rule.
public class PropertyOwner : Aggregate<OwnerId>
{
  public const int NameMaxLength = 200;
  public const int FatherHusbandNameMaxLength = 200;
  public const int NtnMaxLength = global::Ntn.MaxLength;
  public const int RemarksMaxLength = 4000;

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
  public string? Remarks { get; private set; }
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
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(ownerCode);

    var owner = new PropertyOwner { Id = id, OwnerCode = ownerCode, IsActive = true };
    owner.Apply(ownerType, ownerName, fatherHusbandName, cnic, ntn, email, remarks);
    return owner;
  }

  public void Update(
      OwnerType ownerType,
      Name ownerName,
      string? fatherHusbandName,
      Cnic? cnic,
      Ntn? ntn,
      EmailAddress? email,
      string? remarks)
  {
    EnsureActive();
    Apply(ownerType, ownerName, fatherHusbandName, cnic, ntn, email, remarks);
  }

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
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(ownerType);
    ArgumentNullException.ThrowIfNull(ownerName);

    if (ownerType.Id != OwnerTypeId)
      ownerType.EnsureActive();

    if (ownerName.Value.Length > NameMaxLength)
      throw new DomainException($"Owner name cannot exceed {NameMaxLength} characters.");

    var fatherHusband = Guard.Text(fatherHusbandName, FatherHusbandNameMaxLength, "Father / husband name");

    if (ownerType.Is(SystemMasterCodes.OwnerTypeIndividual) && cnic is null)
      throw new DomainException("An individual needs a CNIC (13 digits, e.g. 12345-1234567-1).");

    if (Organizations.TryGetValue(ownerType.Code.Value, out var organization))
    {
      if (cnic is not null)
        throw new DomainException($"{organization} has no CNIC - use its NTN.");

      if (fatherHusband is not null)
        throw new DomainException($"{organization} has no father / husband name - leave it empty.");
    }

    OwnerTypeId = ownerType.Id;
    OwnerName = ownerName;
    FatherHusbandName = fatherHusband;
    Cnic = cnic;
    Ntn = ntn?.Value;
    Email = email;
    Remarks = Guard.Text(remarks, RemarksMaxLength, "Remarks");
  }
}
