using System.Text.RegularExpressions;

public class OwnerContact : Entity<OwnerContactId>
{
  private static readonly Regex NumberPattern = new(@"^\+?[0-9][0-9\s\-()]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  public OwnerId OwnerId { get; private set; } = default!;
  public MasterId ContactTypeId { get; private set; } = default!;
  public string ContactNumber { get; private set; } = default!;
  public bool IsPrimary { get; private set; }
  public bool IsActive { get; private set; }
  public string? Remarks { get; private set; }

  internal static OwnerContact Create(OwnerContactId id, OwnerId ownerId, MasterId contactTypeId, string contactNumber, bool isPrimary, string? remarks)
  {
    var contact = new OwnerContact { Id = id, OwnerId = ownerId, IsActive = true };
    contact.Update(contactTypeId, contactNumber, isPrimary, remarks);
    return contact;
  }

  internal void Update(MasterId contactTypeId, string contactNumber, bool isPrimary, string? remarks)
  {
    if (!IsActive)
      throw new DomainException("This contact is inactive.");

    var number = Guard.RequiredText(contactNumber, 30, "Contact number");
    if (!NumberPattern.IsMatch(number))
      throw new DomainException("Contact number may only contain digits, spaces, '-', '(' ')' and a leading '+'.");

    ContactTypeId = contactTypeId;
    ContactNumber = number;
    IsPrimary = isPrimary;
    Remarks = Guard.Text(remarks, 200, "Remarks");
  }

  internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary && IsActive;

  internal void Deactivate()
  {
    IsActive = false;
    IsPrimary = false;
  }
}
