using System.Text.RegularExpressions;

/// The single people-and-organizations table: one row per person, company, trust or government body,
/// whether they act as owner, allottee, lessee, tenant, bidder or contractor.
///
/// Contacts and addresses belong to the aggregate so "at most one primary of each" (schema guide,
/// rule 3) is enforced in one place.
public class PropertyOwner : Aggregate<OwnerId>
{
  public const int NameMaxLength = 200;
  public const int NtnMaxLength = 20;

  private static readonly Regex NtnPattern = new(@"^[A-Z0-9][A-Z0-9\-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private readonly List<OwnerContact> _contacts = new();
  private readonly List<OwnerAddress> _addresses = new();

  /// OWN-00001, issued by the OWNER code sequence.
  public BusinessCode OwnerCode { get; private set; } = default!;
  public MasterId OwnerTypeId { get; private set; } = default!;
  public Name OwnerName { get; private set; } = default!;
  public string? FatherHusbandName { get; private set; }
  /// Individuals.
  public Cnic? Cnic { get; private set; }
  /// Companies / trusts.
  public string? Ntn { get; private set; }
  /// SECP / trust registration where applicable.
  public string? RegistrationNo { get; private set; }
  public EmailAddress? Email { get; private set; }
  /// The scanned CNIC copy, a property_document row.
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
      string? ntn,
      string? registrationNo,
      EmailAddress? email,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(ownerCode);

    var owner = new PropertyOwner { Id = id, OwnerCode = ownerCode, IsActive = true };
    owner.Apply(ownerType, ownerName, fatherHusbandName, cnic, ntn, registrationNo, email, remarks);
    return owner;
  }

  public void Update(
      OwnerType ownerType,
      Name ownerName,
      string? fatherHusbandName,
      Cnic? cnic,
      string? ntn,
      string? registrationNo,
      EmailAddress? email,
      string? remarks)
  {
    EnsureActive();
    Apply(ownerType, ownerName, fatherHusbandName, cnic, ntn, registrationNo, email, remarks);
  }

  public void AttachCnicDocument(DocumentId documentId)
  {
    ArgumentNullException.ThrowIfNull(documentId);
    EnsureActive();
    CnicDocumentId = documentId;
  }

  public void Deactivate() => IsActive = false;
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
      string? ntn,
      string? registrationNo,
      EmailAddress? email,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(ownerType);
    ArgumentNullException.ThrowIfNull(ownerName);

    if (ownerType.Id != OwnerTypeId)
      ownerType.EnsureActive();

    if (ownerName.Value.Length > NameMaxLength)
      throw new DomainException($"Owner name cannot exceed {NameMaxLength} characters.");

    ntn = Guard.Text(ntn, NtnMaxLength, "NTN")?.ToUpperInvariant();
    if (ntn is not null && !NtnPattern.IsMatch(ntn))
      throw new DomainException("NTN may only contain letters, digits and '-'.");

    OwnerTypeId = ownerType.Id;
    OwnerName = ownerName;
    FatherHusbandName = Guard.Text(fatherHusbandName, 200, "Father / husband name");
    Cnic = cnic;
    Ntn = ntn;
    RegistrationNo = Guard.Text(registrationNo, 50, "Registration no.");
    Email = email;
    Remarks = Guard.Text(remarks, 4000, "Remarks");
  }
}
