/// The fields the change log names (plain words, shown as they are on the employee record).
public static class EmployeeFields
{
  public const string FirstName = "First name";
  public const string MiddleName = "Middle name";
  public const string LastName = "Last name";
  public const string Cnic = "CNIC";
  public const string DateOfBirth = "Date of birth";
  public const string Gender = "Gender";
  public const string Nationality = "Nationality";
  public const string MaritalStatus = "Marital status";
  public const string BloodGroup = "Blood group";
  public const string EmployeeNumber = "Employee number";
  public const string EmploymentType = "Employment type";
  public const string EmploymentMethod = "Employment method";
  public const string Term = "Term";
  public const string ParentDepartment = "Parent department";
  public const string SigninAccount = "Sign-in account";
  public const string Profile = "Profile";
  public const string Pay = "Pay";
  public const string NextIncrement = "Next increment";
  public const string ServiceStatus = "Service status";

  /// A change of these needs a reason (they decide retirement, identity and pay).
  public static readonly string[] NeedReason = [Cnic, DateOfBirth];
}

/// One change to an employee's identity, employment or pay: old value -> new value, why, who and when. Append-only
/// (a database trigger refuses updates and deletes), so the record's history can always be explained.
public class EmployeeChangeLogEntry : Aggregate<EmployeeChangeLogEntryId>
{
  public const int ValueMaxLength = 500;

  public EmployeeId EmployeeId { get; private set; } = default!;
  public string Field { get; private set; } = default!;
  public string? OldValue { get; private set; }
  public string? NewValue { get; private set; }
  public string? Reason { get; private set; }
  public Guid? ChangedBy { get; private set; }
  public string? ChangedByName { get; private set; }
  public DateTime ChangedAt { get; private set; }

  public static EmployeeChangeLogEntry Record(
      EmployeeId employeeId,
      string field,
      string? oldValue,
      string? newValue,
      string? reason,
      Guid? changedBy,
      string? changedByName,
      DateTime changedAt)
  {
    ArgumentNullException.ThrowIfNull(employeeId);

    return new EmployeeChangeLogEntry
    {
      Id = EmployeeChangeLogEntryId.New(),
      EmployeeId = employeeId,
      Field = Guard.RequiredText(field, 60, "Field"),
      OldValue = Cut(oldValue),
      NewValue = Cut(newValue),
      Reason = Guard.Text(reason, 1000, "Reason"),
      ChangedBy = changedBy,
      ChangedByName = Guard.Text(changedByName, 150, "Changed by"),
      ChangedAt = changedAt
    };
  }

  /// The personal-detail fields that differ between two versions, as (field, old, new).
  public static IReadOnlyList<(string Field, string? Old, string? New)> Diff(PersonalDetails before, PersonalDetails after)
  {
    var changes = new List<(string, string?, string?)>();
    void Compare(string field, string? a, string? b)
    {
      if (!string.Equals(a ?? "", b ?? "", StringComparison.Ordinal))
        changes.Add((field, a, b));
    }

    Compare(EmployeeFields.FirstName, before.FirstName, after.FirstName);
    Compare(EmployeeFields.MiddleName, before.MiddleName, after.MiddleName);
    Compare(EmployeeFields.LastName, before.LastName, after.LastName);
    Compare(EmployeeFields.Cnic, before.Cnic, after.Cnic);
    Compare(EmployeeFields.DateOfBirth, Day(before.DateOfBirth), Day(after.DateOfBirth));
    Compare(EmployeeFields.Gender, Words(before.Gender), Words(after.Gender));
    Compare(EmployeeFields.Nationality, before.Nationality, after.Nationality);
    Compare(EmployeeFields.MaritalStatus, Words(before.MaritalStatus), Words(after.MaritalStatus));
    Compare(EmployeeFields.BloodGroup, before.BloodGroup, after.BloodGroup);
    return changes;
  }

  public static string? Day(DateOnly? date) => date?.ToString("yyyy-MM-dd");

  public static string? Words(Enum? value) => value is null ? null : EnumText.Label(value);

  private static string? Cut(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Length <= ValueMaxLength ? value.Trim() : value.Trim()[..ValueMaxLength];
}
