/// Where an employee's salary is paid. Restricted to payroll staff. Payments snapshot the account when they are made,
/// so later edits never rewrite a past payment. At most one active primary account per employee.
public class EmployeeBankAccount : Aggregate<BankAccountId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public string BankName { get; private set; } = default!;
  public string? BranchName { get; private set; }
  public string? AccountNumber { get; private set; }
  public string? Iban { get; private set; }
  public bool IsPrimary { get; private set; }
  public RecordStatus Status { get; private set; }

  public bool IsUsable => Status == RecordStatus.Active;

  public static EmployeeBankAccount Create(BankAccountId id, EmployeeId employeeId, string bankName, string? branchName, string? accountNumber, string? iban)
  {
    ArgumentNullException.ThrowIfNull(employeeId);

    var account = new EmployeeBankAccount { Id = id, EmployeeId = employeeId, Status = RecordStatus.Active };
    account.Update(bankName, branchName, accountNumber, iban);
    return account;
  }

  public void Update(string bankName, string? branchName, string? accountNumber, string? iban)
  {
    BankName = Guard.RequiredText(bankName, 150, "Bank name");
    BranchName = Guard.Text(branchName, 150, "Branch");
    AccountNumber = NormalizeAccountNumber(accountNumber);
    Iban = global::Iban.NormalizeOptional(iban);

    if (AccountNumber is null && Iban is null)
      throw new DomainException("Enter the account number or the IBAN.");
  }

  /// Use EmployeeBankAccounts.MakePrimary so the employee's other accounts lose the flag in the same save.
  internal void SetPrimary(bool isPrimary)
  {
    if (isPrimary && !IsUsable)
      throw new DomainException("An inactive account cannot be the primary account.");

    IsPrimary = isPrimary;
  }

  public void Deactivate()
  {
    Status = RecordStatus.Inactive;
    IsPrimary = false;
  }

  public void Activate() => Status = RecordStatus.Active;

  private static string? NormalizeAccountNumber(string? value)
  {
    var text = Guard.Text(value, 34, "Account number");
    if (text is null)
      return null;

    text = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
    if (text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
      throw new DomainException("An account number may only contain letters, digits and '-'.");

    return text.ToUpperInvariant();
  }
}

/// The rule "one active primary account per employee", applied across the employee's accounts.
public static class EmployeeBankAccounts
{
  public static void MakePrimary(IReadOnlyCollection<EmployeeBankAccount> accountsOfEmployee, EmployeeBankAccount primary)
  {
    if (accountsOfEmployee.Any(a => a.EmployeeId != primary.EmployeeId))
      throw new DomainException("All accounts must belong to the same employee.");

    foreach (var other in accountsOfEmployee.Where(a => a.Id != primary.Id && a.IsPrimary))
      other.SetPrimary(false);

    primary.SetPrimary(true);
  }

  /// When the primary account is closed, the most recent other active account becomes primary.
  public static void PromoteNextPrimary(IReadOnlyCollection<EmployeeBankAccount> accountsOfEmployee)
  {
    if (accountsOfEmployee.Any(a => a.IsPrimary && a.IsUsable))
      return;

    var next = accountsOfEmployee.Where(a => a.IsUsable).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
    next?.SetPrimary(true);
  }
}
