/// The signed-in officer who approves an act: their Identity user id, their HRM employee id when the account is linked
/// to one, and the name written on the record. It is read from the sign-in, never typed, so another officer's name
/// cannot be put on an approval.
public sealed record Approver(Guid? UserId, Guid? EmployeeId, string Name)
{
  public const int NameMaxLength = 150;
}
