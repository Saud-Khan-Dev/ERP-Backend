using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

/// What counts as an open legal matter. Shared by the register filters (hasOpenEncroachment / hasOpenCase)
/// and the cross-property legal-matters list, so both always agree.
///
///   encroachment — unresolved: no resolution date yet (the same rule the area summary uses)
///   court case   — pending: its status is none of DECIDED, APPEALED (the matter moved on to a higher-court case,
///                  which is a row of its own), WITHDRAWN, SETTLED or CLOSED. PENDING, OTHER and any status an
///                  admin adds for a case still running count as pending.
///   appeal       — Filed or UnderHearing (not Decided, not Withdrawn)
///   violation    — Open, Fined or Appealed (not Rectified, and not Cancelled: the violation that cancelled the
///                  agreement is disposed of)
public static class OpenMatters
{
  public static readonly IReadOnlyList<string> ClosedCaseStatusCodes = new[]
  {
    SystemMasterCodes.Decided, SystemMasterCodes.Appealed, "WITHDRAWN", "SETTLED", "CLOSED"
  };

  public static readonly Expression<Func<PropertyEncroachment, bool>> EncroachmentIsOpen = e => e.ResolutionDate == null;

  public static readonly Expression<Func<PropertyAppeal, bool>> AppealIsOpen =
      a => a.AppealStatus == AppealStatus.Filed || a.AppealStatus == AppealStatus.UnderHearing;

  public static readonly Expression<Func<AgreementViolation, bool>> ViolationIsOpen =
      v => v.ViolationStatus == ViolationStatus.Open || v.ViolationStatus == ViolationStatus.Fined || v.ViolationStatus == ViolationStatus.Appealed;

  // the same rules for rows already in memory
  public static readonly Func<PropertyEncroachment, bool> IsOpenEncroachment = EncroachmentIsOpen.Compile();
  public static readonly Func<PropertyAppeal, bool> IsOpenAppeal = AppealIsOpen.Compile();
  public static readonly Func<AgreementViolation, bool> IsOpenViolation = ViolationIsOpen.Compile();

  /// Ids of the litigation statuses that close a case (the status table is tiny, so it is read whole).
  public static async Task<List<MasterId>> ClosedCaseStatusIdsAsync(IApplicationDbContext context, CancellationToken cancellationToken) =>
      (await context.Set<LitigationStatus>().AsNoTracking().ToListAsync(cancellationToken))
        .Where(s => ClosedCaseStatusCodes.Contains(s.Code.Value))
        .Select(s => s.Id)
        .ToList();

  /// The pending cases, given the closing status ids.
  public static Expression<Func<PropertyLitigation, bool>> CaseIsOpen(IReadOnlyCollection<MasterId> closedStatusIds) =>
      l => !closedStatusIds.Contains(l.LitigationStatusId);

  /// The opposite filter (closed / resolved).
  public static Expression<Func<T, bool>> Not<T>(Expression<Func<T, bool>> predicate) =>
      Expression.Lambda<Func<T, bool>>(Expression.Not(predicate.Body), predicate.Parameters);
}
