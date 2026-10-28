/// An order engaging a contingent employee: in an office, as a designation, for a period, at a wage - with its letter.
/// Contingent staff hold no sanctioned seat; they are paid only for the days their orders cover, so a gap between one
/// order and its renewal is unpaid. An order entered by mistake is cancelled (with the reason), never edited.
public class EngagementOrder : Aggregate<EngagementOrderId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public OrganizationUnitId OrgUnitId { get; private set; } = default!;
  public DesignationId DesignationId { get; private set; } = default!;
  public DateOnly StartDate { get; private set; }
  public DateOnly EndDate { get; private set; }
  public WageBasis WageBasis { get; private set; }
  public decimal Rate { get; private set; }
  /// The order letter, kept as one of the employee's documents.
  public EmployeeDocumentId LetterDocumentId { get; private set; } = default!;
  public RecordStatus Status { get; private set; }
  public string? CancelReason { get; private set; }

  public DateRange Range => new(StartDate, EndDate);
  public bool IsActive => Status == RecordStatus.Active;

  public static EngagementOrder Create(EngagementOrderId id, Employee employee, OrganizationUnitId orgUnitId, DesignationId designationId,
      DateOnly startDate, DateOnly endDate, WageBasis wageBasis, decimal rate, EmployeeDocumentId letterDocumentId)
  {
    if (employee.EmploymentBasis is EmploymentBasis.Regular or EmploymentBasis.Deputation)
      throw new DomainException($"{employee.DisplayName} is {EnumText.WithArticle(employee.EmploymentBasis)} employee; orders are for contingent staff, who hold no seat.");
    Guard.DateOrder(startDate, endDate, "the first day", "The last day");
    if (rate <= 0)
      throw new DomainException("Give the wage of the order.");

    return new EngagementOrder
    {
      Id = id, EmployeeId = employee.Id, OrgUnitId = orgUnitId, DesignationId = designationId, StartDate = startDate, EndDate = endDate,
      WageBasis = wageBasis, Rate = decimal.Round(rate, 2), LetterDocumentId = letterDocumentId, Status = RecordStatus.Active,
    };
  }

  public void Cancel(string reason)
  {
    if (!IsActive)
      throw new DomainException("The order is already cancelled.");
    CancelReason = Guard.RequiredText(reason, 500, "Reason");
    Status = RecordStatus.Inactive;
  }

  /// The wage for the payable days of the order inside a month of `periodDays` days.
  public static decimal Wage(WageBasis basis, decimal rate, decimal payableDays, int periodDays) =>
      payableDays <= 0 ? 0
      : decimal.Round(basis == WageBasis.Daily ? rate * payableDays : rate * payableDays / periodDays, 2, MidpointRounding.AwayFromZero);
}
