/// The facts that identify a person or pay them (HR_SENSITIVE.VIEW): CNIC, date of birth, family, addresses,
/// contacts, bank accounts and pay figures. Without the permission a response carries them as null and names them in
/// its `restricted` list, so a screen shows "Restricted" instead of an empty value. A person always sees their own
/// (self-service passes `own: true`).
public sealed class Sensitive(ICurrentUser currentUser)
{
  public const string Cnic = "cnic";
  public const string DateOfBirth = "dateOfBirth";
  public const string Contacts = "contacts";
  public const string Addresses = "addresses";
  public const string EmergencyContacts = "emergencyContacts";
  public const string FamilyMembers = "familyMembers";
  public const string Mobile = "mobile";
  public const string BasicPay = "basicPay";
  public const string Pay = "pay";
  public const string AccountNumber = "accountNumber";
  public const string Iban = "iban";

  /// The caller may see sensitive facts about other people.
  public bool CanView => currentUser.HasPermission(PermissionCatalog.HrSensitive.View);

  /// The caller may change a CNIC, a date of birth or a salary bank account.
  public bool CanEdit => currentUser.HasPermission(PermissionCatalog.HrSensitive.Edit);

  /// Shown in full: the caller may view them, or it is the caller's own record.
  public bool Shows(bool own = false) => own || CanView;

  /// The fields withheld from this response: empty when shown.
  public IReadOnlyList<string> Withheld(bool own, params string[] fields) => Shows(own) ? [] : fields;

  public static readonly IReadOnlyList<string> None = [];
}
