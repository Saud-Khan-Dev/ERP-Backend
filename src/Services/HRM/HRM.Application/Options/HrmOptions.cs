/// Upload limits for employee papers and photos, bound from the "Documents" configuration section.
public sealed class DocumentOptions
{
  public const string SectionName = "Documents";

  public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

  /// Scans, office documents and images.
  public string[] AllowedExtensions { get; set; } =
  {
    ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".doc", ".docx", ".xls", ".xlsx"
  };

  public string[] PhotoExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".webp" };

  public long MaxPhotoSizeBytes { get; set; } = 5 * 1024 * 1024;
}

/// Service rules that differ between governments or change by notification, bound from the "Hrm" section.
public sealed class HrmOptions
{
  public const string SectionName = "Hrm";

  /// Employee numbers issued when HR leaves the number blank: EMP-001, EMP-002 ... (the Identity service's style).
  public string EmployeeNumberPrefix { get; set; } = "EMP";
  public string EmployeeNumberSeparator { get; set; } = "-";
  public int EmployeeNumberMinimumDigits { get; set; } = 3;

  /// Post codes issued when left blank: POST-0001 ...
  public string PostCodePrefix { get; set; } = "POST";
  public int PostCodeMinimumDigits { get; set; } = 4;

  /// Superannuation age in KP government service.
  public int RetirementAge { get; set; } = 60;

  /// The annual increment date (1 December in KP government service).
  public int AnnualIncrementMonth { get; set; } = 12;
  public int AnnualIncrementDay { get; set; } = 1;
}

/// Payroll controls, bound from the "Payroll" section.
public sealed class PayrollOptions
{
  public const string SectionName = "Payroll";

  /// The person who prepared a payroll run may not approve it (maker-checker). Off only on a developer machine.
  public bool RequireSeparateApprover { get; set; } = true;
}
