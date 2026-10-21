/// Transfer approval controls, bound from the "Transfers" section.
public sealed class TransferOptions
{
  public const string SectionName = "Transfers";

  /// The person who started a transfer may not approve it (maker-checker). Off only on a developer machine, where one
  /// account walks a transfer through; on everywhere else (the same as HRM's Payroll:RequireSeparateApprover).
  public bool RequireSeparateApprover { get; set; } = true;
}
