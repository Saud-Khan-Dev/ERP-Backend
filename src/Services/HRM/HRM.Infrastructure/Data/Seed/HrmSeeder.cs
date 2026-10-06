using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// Seeds the catalogues of docs/gda_hrms_schema.sql (section 4). Runs on every start and is safe to repeat:
/// - a catalogue is filled only while its table is empty (the first start), so values an administrator later renames,
///   deactivates or deletes are never brought back;
/// - the rows the code depends on (BPS 1-22, the BASIC / IT / GPF pay components, the service event types HR actions
///   record under) are ensured on every start.
/// The schema file's "Other" document type is left out on purpose: lists hold real values only.
public sealed class HrmSeeder(ApplicationDbContext context, ILogger<HrmSeeder> logger)
{
  public async Task SeedAsync(CancellationToken cancellationToken = default)
  {
    var added = 0;

    // ---- always: the rows the code depends on ----
    added += await EnsureGradesAsync(cancellationToken);
    added += await EnsureServiceEventTypesAsync(cancellationToken);
    added += await EnsureSystemComponentsAsync(cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    // ---- first start only: the schema's catalogues ----
    if (!await context.OrganizationUnitTypes.AnyAsync(cancellationToken))
    {
      foreach (var (name, code, level) in new[]
      {
        ("Authority", "AUTH", 0), ("Department", "DEPT", 1), ("Wing", "WING", 2), ("Division", "DIV", 3),
        ("Section", "SEC", 4), ("Office", "OFF", 5)
      })
        context.OrganizationUnitTypes.Add(OrganizationUnitType.Create(OrganizationUnitTypeId.New(), name, code, level));
      added += 6;
    }

    if (!await context.RecruitmentMethods.AnyAsync(cancellationToken))
    {
      foreach (var name in new[] { "Project Regularization", "Board of Authority", "Public Service Commission (CSS/PMS etc.)" })
        context.RecruitmentMethods.Add(RecruitmentMethod.Create(RecruitmentMethodId.New(), name));
      added += 3;
    }

    if (!await context.DocumentTypes.AnyAsync(cancellationToken))
    {
      foreach (var (name, requiresExpiry) in new[]
      {
        ("CNIC", true), ("Domicile", false), ("Appointment Order", false), ("Joining Report", false),
        ("Degree / Certificate", false), ("Service Book", false), ("Medical Certificate", true), ("NOC", false)
      })
        context.DocumentTypes.Add(DocumentType.Create(DocumentTypeId.New(), name, requiresExpiry));
      added += 8;
    }

    if (!await context.SalaryComponents.AnyAsync(c => !SystemCodes.Contains(c.ComponentCode), cancellationToken))
    {
      foreach (var (code, name, type, taxable) in new[]
      {
        ("GDA_AUTH", "GDA Authority Allowance", ComponentType.Earning, true),
        ("MEDICAL", "Medical Allowance", ComponentType.Earning, true),
        ("HOUSING", "Housing Subsidy", ComponentType.Earning, true),
        ("UTILITY", "Utility Allowance", ComponentType.Earning, true),
        ("ADHOC_2022", "Adhoc Relief Allowance 2022", ComponentType.Earning, true),
        ("ADHOC_2023", "Adhoc Relief Allowance 2023", ComponentType.Earning, true),
        ("ADHOC_2024", "Adhoc Relief Allowance 2024", ComponentType.Earning, true),
        ("ADHOC_2025", "Adhoc Relief Allowance 2025", ComponentType.Earning, true),
        ("ADHOC_2026", "Adhoc Relief Allowance 2026", ComponentType.Earning, true),
        ("BF", "Benevolent Fund (BF)", ComponentType.Deduction, false),
        ("MCA", "Motor Car Advance (MCA)", ComponentType.Deduction, false),
        ("GPF_ADV", "GP Fund Advance", ComponentType.Deduction, false),
        ("RBD", "Rent & Building Dues (RB&D)", ComponentType.Deduction, false)
      })
        context.SalaryComponents.Add(SalaryComponent.Create(SalaryComponentId.New(), code, name, type, taxable));
      added += 13;
      await context.SaveChangesAsync(cancellationToken);
    }

    if (!await context.LoanTypes.AnyAsync(cancellationToken))
    {
      var mca = await context.SalaryComponents.FirstOrDefaultAsync(c => c.ComponentCode == "MCA", cancellationToken);
      var gpfAdvance = await context.SalaryComponents.FirstOrDefaultAsync(c => c.ComponentCode == "GPF_ADV", cancellationToken);
      context.LoanTypes.Add(LoanType.Create(LoanTypeId.New(), "Motor Car Advance (MCA)", mca, isGpfAdvance: false, 0));
      context.LoanTypes.Add(LoanType.Create(LoanTypeId.New(), "GP Fund Advance", gpfAdvance, isGpfAdvance: true, 0));
      added += 2;
    }

    if (!await context.LeaveTypes.AnyAsync(cancellationToken))
    {
      context.LeaveTypes.Add(LeaveType.Create(LeaveTypeId.New(), "Casual Leave", true, 20, null, false, false));
      context.LeaveTypes.Add(LeaveType.Create(LeaveTypeId.New(), "Earned Leave", true, null, null, true, false));
      context.LeaveTypes.Add(LeaveType.Create(LeaveTypeId.New(), "Medical Leave", true, null, null, false, false));
      context.LeaveTypes.Add(LeaveType.Create(LeaveTypeId.New(), "Leave Without Pay (LWOP)", false, null, null, false, true));
      added += 4;
    }

    if (!await context.RequestTypes.AnyAsync(cancellationToken))
    {
      context.RequestTypes.Add(EmployeeRequestType.Create(EmployeeRequestTypeId.New(), "Transfer Request", "TRANSFER", false));
      context.RequestTypes.Add(EmployeeRequestType.Create(EmployeeRequestTypeId.New(), "Document Request", "DOCUMENT", false));
      context.RequestTypes.Add(EmployeeRequestType.Create(EmployeeRequestTypeId.New(), "Profile Correction Request", "PROFILE_FIX", true));
      added += 3;
    }

    await context.SaveChangesAsync(cancellationToken);

    if (added > 0)
      logger.LogInformation("Seeded {Count} HRM catalogue row(s).", added);
  }

  private static readonly string[] SystemCodes = [SystemComponents.BasicPay, SystemComponents.IncomeTax, SystemComponents.GpFund];

  private async Task<int> EnsureGradesAsync(CancellationToken cancellationToken)
  {
    var existing = await context.PayScaleGrades.Select(g => g.BpsNumber).ToListAsync(cancellationToken);
    var missing = Enumerable.Range(PayScaleGrade.MinBps, PayScaleGrade.MaxBps).Except(existing).ToList();
    foreach (var bps in missing)
      context.PayScaleGrades.Add(PayScaleGrade.Create(PayScaleGradeId.New(), bps, $"BPS-{bps}"));
    return missing.Count;
  }

  private async Task<int> EnsureServiceEventTypesAsync(CancellationToken cancellationToken)
  {
    var existing = await context.ServiceEventTypes.Select(t => t.Name).ToListAsync(cancellationToken);
    var missing = ServiceEventNames.All.Where(e => !existing.Contains(e.Name, StringComparer.OrdinalIgnoreCase)).ToList();
    foreach (var (name, category) in missing)
      context.ServiceEventTypes.Add(ServiceEventType.Create(ServiceEventTypeId.New(), name, category));
    return missing.Count;
  }

  private async Task<int> EnsureSystemComponentsAsync(CancellationToken cancellationToken)
  {
    var existing = await context.SalaryComponents.Where(c => SystemCodes.Contains(c.ComponentCode)).Select(c => c.ComponentCode).ToListAsync(cancellationToken);
    var missing = new[]
      {
        (SystemComponents.BasicPay, "Basic Pay", ComponentType.Earning, true),
        (SystemComponents.GpFund, "GP Fund", ComponentType.Deduction, false),
        (SystemComponents.IncomeTax, "Income Tax (I.T.)", ComponentType.Deduction, false)
      }
      .Where(c => !existing.Contains(c.Item1))
      .ToList();

    foreach (var (code, name, type, taxable) in missing)
      context.SalaryComponents.Add(SalaryComponent.Create(SalaryComponentId.New(), code, name, type, taxable));
    return missing.Count;
  }
}
